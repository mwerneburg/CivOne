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
		public static void ResetState()
		{
			SetStaticField(typeof(Game), "_instance", null);
			SetStaticField(typeof(Map), "_instance", null);

			// Drain the task queue too. It is a static list that nothing clears between
			// games, so tasks enqueued by one test (a newspaper, an advisor, a settler
			// order) were still pending in the next one. A test that pumps GameTask.Update()
			// then spends its iterations on another test's leftovers — which is exactly how
			// the swamp-drain test passed alone and failed in the suite.
			((System.Collections.IList)typeof(GameTask)
				.GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static)!
				.GetValue(null)!).Clear();
			SetStaticField(typeof(GameTask), "_currentTask", null);
		}

		private static void SetStaticField(Type type, string name, object value)
			=> type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

		// Generate a fresh map of the requested size and start a game on it.
		public static void NewGame(int width = 80, int height = 50, int competition = 7, int difficulty = 0)
		{
			EnsureRuntime();
			ResetState();

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
