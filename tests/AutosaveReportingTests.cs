// CivOne tests
//
// An autosave that fails must say so.
//
// PerformAutoSave caught every exception and handed the message to IRuntime.Log — which is
// literally `public void Log(...) { }` in Release builds. A competition-17 game threw here on
// every turn (the per-player arrays were a slot short of the roster), autosaved nothing for
// 526 turns, and the only evidence was a file timestamp four hours stale. Disk space, cloud
// sync and permissions all had to be ruled out before "it was never written at all" was even
// on the table.
//
// The failure now goes to the decision log, which works in Release and is what gets read
// after a run, and the player is told once — not once per turn, and again if it recurs after
// a recovery.

using System.IO;
using System.Linq;

namespace CivOne.Tests
{
	public class AutosaveReportingTests
	{
		// Force SaveCos to throw without breaking anything else: put a DIRECTORY where the
		// autosave file belongs. Portable, and it exercises the real write path rather than a
		// test hook — adding one of those to production code to observe a test is the wrong
		// trade, and this needs no such thing.
		private static Game AGameThatCannotAutosave()
		{
			Sim.NewGame(width: 80, height: 50);
			// The tests share one temp saves directory and run in an order xunit chooses, so
			// a previous test's real autosave file may be sitting on this path.
			string path = Settings.Instance.AutoSavePath;
			if (File.Exists(path)) File.Delete(path);
			if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			Sim.ClearTasks();
			return Game.Instance;
		}

		// Counted by task type. Sim.PendingMessageLines cannot read an ADVISOR message — it
		// reflects into _screen._message, which advisor screens hold differently — so the
		// content is pinned at the source (TheFailureIsRoutedToTheDecisionLog) and the
		// behaviour that matters, "told once and not again", is counted here.
		private static int QueuedMessages() => Sim.PendingTaskTypes().Count(t => t == "Message");

		private static bool ReportedLatch(Game g) =>
			(bool)typeof(Game).GetField("_autosaveFailureReported",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.GetValue(g)!;

		// The whole point: a failure is no longer silent.
		[Fact]
		public void AFailedAutosaveTellsThePlayer()
		{
			Game g = AGameThatCannotAutosave();

			g.PerformAutoSave();

			Assert.True(ReportedLatch(g), "a failed autosave did not report");
			Assert.Equal(1, QueuedMessages());
		}

		// ...but it does not nag. 526 turns of the same warning is its own kind of silence.
		[Fact]
		public void APersistentFailureWarnsOnlyOnce()
		{
			Game g = AGameThatCannotAutosave();
			g.PerformAutoSave();
			Sim.ClearTasks();

			g.PerformAutoSave();
			g.PerformAutoSave();

			Assert.Equal(0, QueuedMessages());
		}

		// A recovery re-arms the warning, so a second outage is not swallowed by the first.
		[Fact]
		public void ARecoveryReArmsTheWarning()
		{
			Game g = AGameThatCannotAutosave();
			g.PerformAutoSave();
			Assert.True(ReportedLatch(g));

			Directory.Delete(Settings.Instance.AutoSavePath);   // writable again
			g.PerformAutoSave();

			Assert.False(ReportedLatch(g), "a successful autosave did not clear the warning latch");
		}

		// And the happy path still writes the file — the guard must not cost the feature.
		[Fact]
		public void AWorkingAutosaveWritesTheFileAndSaysNothing()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			if (Directory.Exists(Settings.Instance.AutoSavePath))
				Directory.Delete(Settings.Instance.AutoSavePath);
			if (File.Exists(Settings.Instance.AutoSavePath))
				File.Delete(Settings.Instance.AutoSavePath);
			Sim.ClearTasks();

			g.PerformAutoSave();

			Assert.True(File.Exists(Settings.Instance.AutoSavePath), "the autosave was not written");
			Assert.False(ReportedLatch(g));
		}

		// The failure reaches the decision log, which unlike IRuntime.Log survives a Release
		// build. Pinned at the source: the logger is inactive in tests, so the call cannot be
		// observed by reading the file.
		[Fact]
		public void TheFailureIsRoutedToTheDecisionLog()
		{
			var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			string src = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Game.cs"));

			int at = src.IndexOf("catch (Exception ex)", src.IndexOf("internal void PerformAutoSave"));
			Assert.True(at > 0, "PerformAutoSave has moved or been rewritten");
			string block = src.Substring(at, 1400);

			Assert.Contains("DecisionLogger.LogAutosaveFailure", block);
		}
	}
}
