// CivOne diagnostic (not an assertion)
//
// Repro for the turn-328 hang: a 13-civ game ran 327 turns in 80 seconds of measured turn
// time, with no degradation trend, then stopped dead. Restarting from the autosave hangs
// again, so it is deterministic and lives inside one turn rather than being a slow drift.
//
//   CIVONE_HANG_SAVE=/path/to.cos dotnet test --filter HangRepro -l "console;verbosity=detailed"
//
// Prints progress to stdout unbuffered so that when it stops, the last line names how far it
// got. Attach with `sample <pid>` while it is stuck to get the stack.

using System;
using System.Diagnostics;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class HangRepro
	{
		private readonly ITestOutputHelper _out;
		public HangRepro(ITestOutputHelper output) => _out = output;

		// What the stuck task actually IS. Task type alone says "Show"; the interesting part
		// is which screen it is holding and what, if anything, it is trying to tell the player.
		private static string Detail()
		{
			var list = (System.Collections.IList)typeof(GameTask)
				.GetField("_tasks", System.Reflection.BindingFlags.NonPublic
				                  | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
			var sb = new System.Text.StringBuilder();
			foreach (object? t in list)
			{
				if (t is null) continue;
				sb.Append(t.GetType().FullName).Append(" { ");
				foreach (var f in t.GetType().GetFields(System.Reflection.BindingFlags.NonPublic
					| System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
				{
					object? v = f.GetValue(t);
					sb.Append(f.Name).Append('=').Append(v?.GetType().Name ?? "null");
					if (v is bool or int or string) sb.Append('(').Append(v).Append(')');
					sb.Append(' ');
				}
				sb.Append("} ");
			}
			sb.Append(" || messages: ").Append(string.Join(" / ", Sim.PendingMessageLines()));
			return sb.ToString();
		}

		[Fact]
		public void AdvanceOneTurnFromTheHangSave()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_HANG_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_HANG_SAVE — skipped"); return; }

			// Unbuffered progress to a file: dotnet test buffers stdout until the test
			// returns, which is never when the point is that it hangs.
			string trace = path + ".trace";
			void T(string m)
			{
				System.IO.File.AppendAllText(trace, $"{DateTime.Now:HH:mm:ss.fff} {m}\n");
			}
			System.IO.File.WriteAllText(trace, "");

			Sim.EnsureRuntime();
			Sim.ResetState();
			var sw = Stopwatch.StartNew();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			T($"loaded in {sw.ElapsedMilliseconds}ms, turn {Game.Instance.GameTurn}");

			Game g = Game.Instance;
			uint start = g.GameTurn;
			int turnsDone = 0;

			// Drive raw EndTurn/GameTask steps rather than Sim.RunTurns, because RunTurns has
			// its own stuck-detector and budget: those would turn a genuine infinite loop into
			// a tidy failure and hide where it is.
			for (int step = 0; step < 2_000_000; step++)
			{
				if (g.GameTurn > start)
				{
					T($"TURN ADVANCED -> {g.GameTurn} in {sw.ElapsedMilliseconds}ms after {step} steps");
					start = g.GameTurn;
					turnsDone++;
					if (turnsDone >= 3) break;
				}
				if (step < 40 || step % 2000 == 0)
				{
					T($"step {step,8}  turn {g.GameTurn}  {sw.ElapsedMilliseconds,7}ms  "
						+ $"current={GameTask.CurrentName}  tasks[{string.Join(",", Sim.PendingTaskTypes())}]");
					if (step == 4000) T("  DETAIL: " + Detail());
				}
				GameTask.Update();
				if (!GameTask.Any()) g.EndTurn();
			}
			T($"finished at turn {g.GameTurn} in {sw.ElapsedMilliseconds}ms");
		}
	}
}
