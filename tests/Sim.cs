// CivOne tests
//
// Headless harness for the simulation layer: a no-op IRuntime so Log()/Settings
// don't NRE, plus helpers to reset the Game/Map singletons between tests and spin
// up a fresh game. The shipped binary is untouched; this lives only in the test
// assembly.

using System;
using System.Diagnostics;
using System.Reflection;
using CivOne;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;

namespace CivOne.Tests
{
	// Minimal IRuntime. Logging is dropped, settings return null (so every game
	// option falls back to its default), and StorageDirectory points at a throwaway
	// temp folder so saves/data land somewhere harmless.
	internal sealed class TestRuntime : IRuntime
	{
#pragma warning disable 67 // events are required by the interface but never raised in tests
		public event EventHandler Initialize;
		public event EventHandler Draw;
		public event UpdateEventHandler Update;
		public event KeyboardEventHandler KeyboardUp;
		public event KeyboardEventHandler KeyboardDown;
		public event ScreenEventHandler MouseUp;
		public event ScreenEventHandler MouseDown;
		public event ScreenEventHandler MouseMove;
		public event ScreenEventHandler MouseWheel;
#pragma warning restore 67

		public TestRuntime(string storageDirectory) => StorageDirectory = storageDirectory;

		public Platform CurrentPlatform => default;
		public string StorageDirectory { get; }
		public string GetSetting(string key) => null;
		public void SetSetting(string key, string value) { }
		public RuntimeSettings Settings { get; } = new RuntimeSettings();
		public MouseCursor CurrentCursor { set { } }
		public Bytemap[] Layers { get; set; }
		public Palette Palette { get; set; }
		public IBitmap Cursor { set { } }
		public int CanvasWidth => 320;
		public int CanvasHeight => 200;
		public void Log(string text, params object[] parameters) { }
		public string BrowseFolder(string caption = "") => "";
		public string WindowTitle { set { } }
		public void Quit() { }
	}

	internal static class Sim
	{
		private static bool _runtimeRegistered;

		// Register the stub runtime once per process. RuntimeHandler.Register throws
		// on a second call, so guard on whether a runtime is already present.
		public static void EnsureRuntime()
		{
			if (_runtimeRegistered) return;
			if (RuntimeHandler.Runtime is null)
			{
				string dir = System.IO.Path.Combine(
					System.IO.Path.GetTempPath(),
					"civone-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
				System.IO.Directory.CreateDirectory(dir);
				RuntimeHandler.Register(new TestRuntime(dir));
			}
			_runtimeRegistered = true;
		}

		// Null the Game and Map singletons so the next CreateGame/LoadCos starts clean
		// (both refuse to run while an instance already exists). The fields are private
		// statics, so reflection is the only handle — kept here, out of production code.
		// Empty the task queue without touching anything else. Setting a scenario up
		// (founding a city, building a unit) enqueues UI tasks that never complete
		// headlessly — they park at the head of the queue and block everything behind
		// them, including the order a test is actually trying to observe. Call this
		// immediately before the action under test.
		public static void ClearTasks()
		{
			((System.Collections.IList)typeof(GameTask)
				.GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static)!
				.GetValue(null)!).Clear();
			SetStaticField(typeof(GameTask), "_currentTask", null);
		}

		public static void ResetState()
		{
			SetStaticField(typeof(Game), "_instance", null);
			SetStaticField(typeof(Map), "_instance", null);

			// Drain the task queue too. It is a static list that nothing clears between
			// games, so tasks enqueued by one test (a newspaper, an advisor, a settler
			// order) were still pending in the next one. A test that pumps GameTask.Update()
			// then spends its iterations on another test's leftovers — which is exactly how
			// the swamp-drain test passed alone and failed in the suite.
			ClearTasks();
		}

		private static void SetStaticField(Type type, string name, object value)
			=> type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

		// Drive N game turns headlessly.
		//
		// The turn machinery runs through GameTask: EndTurn advances the current player and
		// queues Turn tasks for its units, cities and player pass, and those tasks are what
		// call AI.Move and City.NewTurn. Pumping the queue therefore plays the game.
		//
		// The catch is UI tasks. A screen task parks at the head of the queue waiting for a
		// Closed event that never comes without a renderer, and Update() then returns false
		// forever. So a task that fails to progress `StuckLimit` times in a row is dropped
		// and the queue moves on — that is the whole trick that makes headless autoplay work.
		//
		// Returns the turn actually reached (may be short of the target if the loop stalls).
		public static int RunTurns(int turns, Action<int>? onTurn = null)
		{
			EnsureRuntime();
			const int StuckLimit = 200;
			int startTurn = (int)Game.Instance.GameTurn;
			int target = startTurn + turns;
			int lastSeen = startTurn;
			int stuck = 0;
			// Generous ceiling: a turn is many task steps, and a stalled loop must still end.
			long budget = (long)turns * 20000 + 200000;

			while (Game.Instance.GameTurn < target && budget-- > 0)
			{
				if (GameTask.Any())
				{
					if (!GameTask.Update())
					{
						if (++stuck >= StuckLimit) { DropCurrentTask(); stuck = 0; }
					}
					else stuck = 0;
				}
				else
				{
					// Game.Update() is the real driver: it takes the active unit and queues
					// Turn.Move(unit) for it, then Turn.End() — which calls EndTurn — once the
					// player has no units left to move. Calling EndTurn directly here skipped
					// unit movement altogether, so nothing was ever founded or built.
					Game.Instance.Update();
					stuck = 0;
				}

				int now = (int)Game.Instance.GameTurn;
				if (now != lastSeen) { lastSeen = now; onTurn?.Invoke(now); }
			}
			return (int)Game.Instance.GameTurn;
		}

		// Discard the task at the head of the queue — see RunTurns.
		private static void DropCurrentTask()
		{
			var list = (System.Collections.IList)typeof(GameTask)
				.GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
			object? current = typeof(GameTask)
				.GetField("_currentTask", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
			if (current is not null && list.Contains(current)) list.Remove(current);
			else if (list.Count > 0) list.RemoveAt(0);
			SetStaticField(typeof(GameTask), "_currentTask", null);
		}

		// Generate a fresh map of the requested size and start a game on it.
		// `seed` pins the world: map generation and every AI die roll run off Common.Random,
		// so without it two harness runs get different continents and the comparison is
		// noise. Pass the same seed to both sides of an A/B and the only difference is the
		// code. Omit it (0) for the old behaviour of a fresh random world each run.
		public static void NewGame(int width = 80, int height = 50, int competition = 7,
		                           int difficulty = 0, short seed = 0)
		{
			EnsureRuntime();
			ResetState();
			if (seed != 0) Common.SetRandomSeed(seed);

			Map.Instance.Generate(width: width, height: height);
			Stopwatch sw = Stopwatch.StartNew();
			while (!Map.Instance.Ready)
			{
				if (sw.Elapsed > TimeSpan.FromSeconds(60))
					throw new TimeoutException("Map generation did not finish in 60s");
				System.Threading.Thread.Sleep(20);
			}

			var tribe = System.Linq.Enumerable.First(
				Common.Civilizations,
				c => c.PreferredPlayerNumber >= 1 && c.PreferredPlayerNumber <= competition);
			Game.CreateGame(difficulty, competition, tribe, "Tester", "Test", "Testers");
			System.IO.Directory.CreateDirectory(Settings.Instance.SavesDirectory);
		}
	}
}
