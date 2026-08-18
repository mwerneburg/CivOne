// CivOne diagnostic (not an assertion)
//
// A 17-civ run stopped advancing at turn 487 (1937 AD) with the process at 100% CPU and the
// window unable to repaint — reported as a hang, but the managed stacks say otherwise: they
// move between Turn.Step -> AI.MoveInner -> AI.AssignMission, GamePlay rendering, and
// Game.ActiveUnit. Busy, and not progressing. A livelock, not a deadlock.
//
// Turn timings from the same run rule out simple slowness: turns 470-490 averaged 5.7s
// (against 6.3s for the previous 17-civ game at the same turns), so a turn that has not
// finished in minutes is stuck rather than slow.
//
// This loads the autosave and drives the real turn loop with a budget, reporting whether the
// turn completes and what the task queue is chewing on when it does not.
//
//   CIVONE_ENDGAME_SAVE=/path/autosave.cos dotnet test --filter TurnLivelock -l "console;verbosity=detailed"

using System;
using System.Collections.Generic;
using System.Linq;
using CivOne.Tiles;
using CivOne.Units;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class TurnLivelockRepro
	{
		private readonly ITestOutputHelper _out;
		public TurnLivelockRepro(ITestOutputHelper output) => _out = output;

		private string? _dump;
		private readonly List<string> _path = new();

		private static string Describe(Game g, IUnit u)
		{
			Player owner = g.GetPlayer(u.Owner);
			var here = u.Tile.Units;
			string neighbours = string.Join(" ", u.Tile.GetBorderTiles()
				.Where(t => t.Units.Length > 0)
				.Select(t => $"({t.X},{t.Y}){(t.City is null ? "" : "city:" + t.City.Name)}"
					+ string.Join(",", t.Units.Select(x => x.GetType().Name
						+ "/" + g.GetPlayer(x.Owner).TribeName))));
			return $"REPEATER {u.GetType().Name} ({u.X},{u.Y}) {owner.TribeNamePlural}\n"
			     + $"  ML={u.MovesLeft} PM={u.PartMoves} moving={u.Moving} "
			     + $"goto={(u.Goto.IsEmpty ? "-" : $"{u.Goto.X},{u.Goto.Y}")} "
			     + $"sentry={u.Sentry} fortify={u.Fortify}\n"
			     + $"  tile={u.Tile.GetType().Name} city={(u.Tile.City is null ? "-" : u.Tile.City.Name)} "
			     + $"units-here={here.Length} home={(u.Home is null ? "NONE" : u.Home.Name)}\n"
			     + $"  neighbours with units: {neighbours}";
		}

		// Run the AI's own move for this unit once and report what it decided and what the step
		// it chose actually looks like. The refusal is somewhere between "AssignMission set a
		// Goto" and "the unit still has all its moves", and only the target tile can say which.
		private static string Trace(Game g, IUnit u)
		{
			int x0 = u.X, y0 = u.Y, ml0 = u.MovesLeft, pm0 = u.PartMoves;
			AI ai = AI.Instance(g.GetPlayer(u.Owner));
			typeof(AI).GetMethod("Move", System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!
				.Invoke(ai, new object[] { u });

			string s = $"  after one AI.Move: pos ({x0},{y0})->({u.X},{u.Y}) "
			         + $"ML {ml0}->{u.MovesLeft} PM {pm0}->{u.PartMoves} "
			         + $"goto={(u.Goto.IsEmpty ? "-" : $"{u.Goto.X},{u.Goto.Y}")} "
			         + $"movement={(u.Movement is null ? "none" : "ACTIVE")}";
			if (u.Goto.IsEmpty) return s;

			// Pump the queue: the movement is a task, so what it does to the unit only shows
			// after it has run. This is where "the move never lands" becomes visible.
			for (int i = 0; i < 200 && u.Movement is not null; i++)
				if (GameTask.Any()) GameTask.Update(); else g.Update();
			s += $"\n  after pumping the queue: pos ({u.X},{u.Y}) ML={u.MovesLeft} PM={u.PartMoves} "
			   + $"goto={(u.Goto.IsEmpty ? "-" : $"{u.Goto.X},{u.Goto.Y}")} "
			   + $"movement={(u.Movement is null ? "none" : "STILL ACTIVE")}";

			// The move cost nothing. MovementDone waives the cost when BOTH tiles carry rail
			// or a transport tube, so this is the pair of tiles to look at.
			string Rails(ITile t) => $"({t.X},{t.Y}) road={t.Road} rail={t.RailRoad} tube={t.TransportTube}"
			                       + $" city={(t.City is null ? "-" : t.City.Name)}";
			s += $"\n  from {Rails(Map.Instance[x0, y0])}\n  to   {Rails(u.Tile)}";

			ITile? step = Common.GotoStep(u);
			if (step is null) return s + "\n  GotoStep: null (no path)";
			bool inTargets = u.MoveTargets.Any(t => t.X == step.X && t.Y == step.Y);
			return s + $"\n  GotoStep -> ({step.X},{step.Y}) {step.GetType().Name} "
			         + $"city={(step.City is null ? "-" : step.City.Name + "/" + g.GetPlayer(step.City.Owner).TribeName)} "
			         + $"units={string.Join(",", step.Units.Select(x => x.GetType().Name + "/" + g.GetPlayer(x.Owner).TribeName))} "
			         + $"inMoveTargets={inTargets} ocean={step.IsOcean}";
		}

		[Fact]
		public void DoesTheTurnEverEnd()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Settings.Instance.Autopilot = true;
			Game g = Game.Instance;
			int start = (int)g.GameTurn;
			_out.WriteLine($"loaded at turn {start} ({Common.YearString(g.GameTurn)})");

			// Same driver Sim.RunTurns uses, with the accounting this question needs: which
			// unit the AI keeps handing a mission to, and how often the same one comes back.
			var missions = new Dictionary<string, int>();
			var tasks = new Dictionary<string, int>();
			int steps = 0;
			var clock = System.Diagnostics.Stopwatch.StartNew();

			while ((int)g.GameTurn == start && steps < 400000 && clock.Elapsed.TotalSeconds < 90)
			{
				steps++;
				IUnit? active = g.ActiveUnit;
				if (active is not null)
				{
					// Keyed on IDENTITY, not position. Keying on coordinates cannot tell one
					// unit oscillating between two tiles from two units trading places, and
					// those want different fixes.
					string key = $"{active.GetType().Name}@{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active):X} "
					           + $"{g.GetPlayer(active.Owner).TribeNamePlural}";
					if (_path.Count < 40 && (_path.Count == 0 || _path[^1] != $"{key}({active.X},{active.Y})"))
						_path.Add($"{key}({active.X},{active.Y})");
					missions.TryGetValue(key, out int n);
					missions[key] = n + 1;

					// Catch the repeater in the act, while its state is still the state that
					// keeps it active. Reading it after the loop finds a different unit at
					// those coordinates, which is how the first version of this misled me.
					if (n == 20000 && _dump is null) _dump = Describe(g, active) + "\n" + Trace(g, active);
				}
				string? task = GameTask.CurrentName;
				if (task is not null)
				{
					tasks.TryGetValue(task, out int m);
					tasks[task] = m + 1;
				}

				if (GameTask.Any()) GameTask.Update();
				else g.Update();
			}

			clock.Stop();
			_out.WriteLine((int)g.GameTurn != start
				? $"turn ADVANCED to {g.GameTurn} after {steps} steps, {clock.Elapsed.TotalSeconds:F1}s"
				: $"turn DID NOT ADVANCE in {steps} steps, {clock.Elapsed.TotalSeconds:F1}s");

			_out.WriteLine(_dump ?? "no repeater caught");
			_out.WriteLine("first distinct active-unit states:");
			foreach (string p2 in _path) _out.WriteLine("  " + p2);
			_out.WriteLine("most-visited active units:");
			foreach (var kv in missions.OrderByDescending(k => k.Value).Take(10))
				_out.WriteLine($"  {kv.Value,7} x  {kv.Key}");
			_out.WriteLine("task queue heads:");
			foreach (var kv in tasks.OrderByDescending(k => k.Value).Take(10))
				_out.WriteLine($"  {kv.Value,7} x  {kv.Key}");
		}
	}
}
