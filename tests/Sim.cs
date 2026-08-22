// CivOne tests
//
// Headless harness for the simulation layer: a no-op IRuntime so Log()/Settings
// don't NRE, plus helpers to reset the Game/Map singletons between tests and spin
// up a fresh game. The shipped binary is untouched; this lives only in the test
// assembly.

using System;
using System.Diagnostics;
using System.Linq;
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
		//
		// CIVONE_HARNESS_STORAGE names the storage directory, which is what makes parallel
		// sweeps possible: everything the game writes — decisions.jsonl, autosave.cos, the
		// hall of fame — hangs off it, so two runs sharing one directory would interleave
		// their logs and fight over the same autosave. A run per directory keeps them
		// independent, and the sweep script collects the logs afterwards. Unset, it is a
		// throwaway temp directory as before.
		public static void EnsureRuntime()
		{
			if (_runtimeRegistered) return;
			if (RuntimeHandler.Runtime is null)
			{
				string dir = Environment.GetEnvironmentVariable("CIVONE_HARNESS_STORAGE") ?? "";
				if (string.IsNullOrEmpty(dir))
					dir = System.IO.Path.Combine(
						System.IO.Path.GetTempPath(),
						"civone-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
				System.IO.Directory.CreateDirectory(dir);
				RuntimeHandler.Register(new TestRuntime(dir));
			}
			_runtimeRegistered = true;
		}

		// The repo root, found by walking up to the project file — the same trick a dozen
		// tests use to read source files.
		public static string RepoRoot()
		{
			var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			if (dir is null) throw new System.IO.FileNotFoundException("could not find the repo root from " + AppContext.BaseDirectory);
			return dir.FullName;
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

		// Headless, the human never researches anything, and it poisons a whole sweep.
		//
		// Choosing an advance opens a screen. In the SDL runtime under Autopilot the autopilot
		// tick answers it with a synthetic Enter; here there is no renderer, so RunTurns drops
		// the task after 200 stuck updates and the human's CurrentResearch stays null forever.
		// Measured at turn 688 of a sweep game: the human held 63 cities — the largest empire in
		// that world — and TWO advances, against 44 to 84 for every AI.
		//
		// The damage is not to the human. A quarter of the world's cities produce no science,
		// build no Observatory, and never join the count of civilizations listening for the SETI
		// signal — so the signal comes late, the visitors come late, the exotic fuel comes late,
		// and the spaceship launches at turn 650 or never. In 11 of 14 sweep games nothing ever
		// launched, which handed Cultural Ascendancy a walkover against a rival that had been
		// crippled by the test rig rather than by play.
		//
		// Under Autopilot the AI drives the human's cities and units already; this is the same
		// exception applied to the one decision that was still waiting for a person.
		//
		// Choosing is only half of it. When research COMPLETES, Player.AddAdvance enqueues a
		// TechSelect screen and leaves CurrentResearch pointing at the advance just finished —
		// the screen is what moves the human on to the next one. Headless the screen never runs,
		// so the same technology completes turn after turn: measured at turn 682, the human held
		// 171 advances of which only 83 were distinct, every one of them banked twice, while
		// every AI held 83 of 83. Setting research in motion without this made the harness human
		// stronger than any civilization in the game rather than merely functional.
		public static void KeepHumanResearching()
		{
			Player human = Game.Instance?.HumanPlayer!;
			if (human is null) return;

			// Already known? Then the screen that should have moved us on never ran.
			if (human.CurrentResearch is not null
			    && human.Advances.Any(a => a.Id == human.CurrentResearch.Id))
				human.CurrentResearch = null;

			if (human.CurrentResearch is not null) return;
			AI.Instance(human).ChooseResearch();
		}

		// True once a victory has been recorded — DecisionLogger.EndGame is the one thing that
		// clears the active flag, and every victory path calls it. Without this a sweep run
		// keeps grinding turns after the game is decided: the win enqueues a GameOver task,
		// which cannot complete with no renderer, so the loop drops it and plays on to the turn
		// cap. On an epic map that is an hour of nothing.
		public static bool GameDecided()
		{
			var f = typeof(DecisionLogger).GetField("_active",
				BindingFlags.NonPublic | BindingFlags.Static);
			return f is not null && !(bool)f.GetValue(null)!;
		}

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
		// Headless, the human never researches anything, and it poisons a whole sweep.
		public static int RunTurns(int turns, Action<int>? onTurn = null, Func<bool>? stop = null)
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
				if (now != lastSeen)
				{
					lastSeen = now;
					onTurn?.Invoke(now);
					if (stop is not null && stop()) break;
				}
			}
			return (int)Game.Instance.GameTurn;
		}

		// Type names of everything currently queued. For tests that need to know WHICH
		// task an action produced — a refusal enqueues a Message, an accepted move
		// enqueues a MoveUnit — when the task's own effects run in a screen callback
		// that cannot complete without a renderer.
		public static string[] PendingTaskTypes()
		{
			var list = (System.Collections.IList)typeof(GameTask)
				.GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
			var names = new System.Collections.Generic.List<string>();
			foreach (object? t in list) if (t is not null) names.Add(t.GetType().Name);
			return names.ToArray();
		}

		// Every line of text sitting in the queue, from any Message-carried screen that
		// holds a string[] _message (Newspaper, AdvisorMessage, MessageBox...). Task TYPE
		// is not enough when the thing under test is which of several notices fired —
		// they are all `Message`. Headless there is no renderer to open them, so reading
		// the queue is the only way to see what the player would have been told.
		public static string[] PendingMessageLines()
		{
			var list = (System.Collections.IList)typeof(GameTask)
				.GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
			var lines = new System.Collections.Generic.List<string>();
			foreach (object? t in list)
			{
				if (t is null || t.GetType().Name != "Message") continue;
				object? screen = t.GetType()
					.GetField("_screen", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(t);
				if (screen?.GetType()
					.GetField("_message", BindingFlags.NonPublic | BindingFlags.Instance)
					?.GetValue(screen) is string[] msg) lines.AddRange(msg);
			}
			return lines.ToArray();
		}

		// Pump the task queue until it drains, dropping anything that parks.
		//
		// Needed by any test that asserts on an effect which runs in a screen task's Done
		// handler — a nuclear detonation, a city capture, a disaster. Headless there is no
		// renderer to close the screen, so the task sits at the head of the queue forever
		// and the effect never fires. Same trick RunTurns uses, exposed for tests that
		// drive a single action rather than whole turns.
		public static void Settle(int budget = 5000)
		{
			int stuck = 0;
			while (GameTask.Any() && budget-- > 0)
			{
				if (!GameTask.Update())
				{
					if (++stuck >= 50) { DropCurrentTask(); stuck = 0; }
				}
				else stuck = 0;
			}
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
		// which is otherwise seeded from the clock. Unpinned, every scenario test gets a
		// different continent — and tests that reason about who is near whom then pass or
		// fail by luck. Several did exactly that before this default was added. Pass a
		// different seed to vary the world deliberately (the autoplay harness does, for
		// A/B runs); pass 0 to opt out and take a clock-seeded world.
		public const short DefaultSeed = 1234;

		// `map` is "generated" (the default, and what every scenario test wants), or "earth-epic"
		// / "earth-standard" to play the real board.
		//
		// The distinction matters for sweeps and not much else. A generated map changes shape
		// with the seed, so a run at 13 civs and a run at 7 civs differ in continent layout as
		// well as in field size, and the two effects cannot be separated afterwards. Earth holds
		// the planet still: the seed then varies only the die rolls, so several runs of the same
		// world produce independent histories on identical ground. That is the control.
		public static void NewGame(int width = 80, int height = 50, int competition = 7,
		                           int difficulty = 0, short seed = DefaultSeed,
		                           string map = "generated", bool varyHuman = false)
		{
			EnsureRuntime();
			ResetState();
			if (seed != 0) Common.SetRandomSeed(seed);

			switch (map)
			{
				case "earth-epic":
					StageEarthBin("earth_epic.bin");
					if (!Map.Instance.LoadEarthEpic())
						throw new InvalidOperationException("LoadEarthEpic refused; the map was already loaded");
					break;
				case "earth-standard":
					StageEarthBin("earth_standard.bin");
					Map.Instance.LoadMap();
					break;
				case "generated":
					Map.Instance.Generate(width: width, height: height);
					break;
				default:
					throw new ArgumentException($"unknown map '{map}' — use generated, earth-epic or earth-standard");
			}
			Stopwatch sw = Stopwatch.StartNew();
			while (!Map.Instance.Ready)
			{
				if (sw.Elapsed > TimeSpan.FromSeconds(60))
					throw new TimeoutException("Map generation did not finish in 60s");
				System.Threading.Thread.Sleep(20);
			}

			// Which civilization the human plays. Every scenario test wants the same one every
			// time, so that stays the default.
			//
			// A SWEEP wants the opposite. Taking the first eligible civ gave the human Rome in
			// all twelve runs of a batch, from the same Earth start, so whatever that position
			// is worth was worth it twelve times over — and it is worth a lot: the harness human
			// reached 52 cities by turn 220 against the best AI's 21, which leaves a field of
			// small backward civs behind it. That shows up as culture-per-head margins of
			// 1.12-2.31 where real games run 1.03-1.15, and a batch of uncontested coronations.
			// Rotating with the seed spreads the human across the roster instead.
			var eligible = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(
				Common.Civilizations,
				c => c.PreferredPlayerNumber >= 1 && c.PreferredPlayerNumber <= competition));
			var tribe = (varyHuman && eligible.Length > 0)
				? eligible[Math.Abs((int)seed) % eligible.Length]
				: eligible[0];
			Game.CreateGame(difficulty, competition, tribe, "Tester", "Test", "Testers");
			System.IO.Directory.CreateDirectory(Settings.Instance.SavesDirectory);
		}

		// Put the Earth board where Map.ResolveEarthBin looks FIRST — the data directory —
		// because its other two candidates are relative to the executable, and a test binary
		// lives at tests/bin/<cfg>/net10.0 rather than the five-deep runtime path those
		// candidates were written for. Copying rather than changing the search order keeps
		// this entirely on the test side.
		private static void StageEarthBin(string fileName)
		{
			string target = System.IO.Path.Combine(Settings.Instance.DataDirectory, fileName);
			if (System.IO.File.Exists(target)) return;
			string source = System.IO.Path.Combine(RepoRoot(), "resources", fileName);
			if (!System.IO.File.Exists(source))
				throw new System.IO.FileNotFoundException($"no {fileName} in resources/", source);
			System.IO.Directory.CreateDirectory(Settings.Instance.DataDirectory);
			System.IO.File.Copy(source, target);
		}
	}
}
