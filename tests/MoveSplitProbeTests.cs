// CivOne tests
//
// Guards the AI.Move breakdown probe (TurnMetrics.AddBucket). Temporary, and it
// goes when the probe does — but it earns its place now: the probe's whole value
// is that it emits during a five-hour unattended run, and a probe that silently
// records nothing wastes the entire run before anyone finds out.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class MoveSplitProbeTests
	{
		private static (Player owner, IUnit unit) AUnitOnGrass()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 22; y <= 28; y++)
			for (int x = 38; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = Game.Instance.Players.First(x => Game.Instance.PlayerNumber(x) != 0);
			p.Explore(42, 25, range: 6);
			Game.Instance.AddCity(p, 0, 41, 25);
			// ON the city tile deliberately: no improvement is legal there, so the settler
			// cannot finish its turn with local work and must run a site scan.
			IUnit u = Game.Instance.CreateUnit(UnitType.Settlers, 41, 25,
				Game.Instance.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (p, u);
		}

		[Fact]
		public void MovingAUnit_RecordsItUnderItsOwnType()
		{
			var (owner, unit) = AUnitOnGrass();
			TurnMetrics.Reset();

			AI.Instance(owner).Move(unit);

			Assert.Contains(TurnMetrics.Buckets(), b => b.Key == "unit:Settlers" && b.Calls == 1);
		}

		// The site probes hang off the settler path, so at least one must fire — this is
		// what catches a probe that got wrapped around a method nothing calls.
		[Fact]
		public void MovingASettler_RecordsAtLeastOneSiteScan()
		{
			var (owner, unit) = AUnitOnGrass();
			TurnMetrics.Reset();

			AI.Instance(owner).Move(unit);

			Assert.Contains(TurnMetrics.Buckets(), b => b.Key.StartsWith("site:") && b.Calls > 0);
		}

		// Diplomats were the top move cost in the 2026-08-02 run (3.19s/turn). Both halves
		// of the split must fire, or the next run answers nothing: dip:Target without
		// dip:FirstStep would mean the A* probes never ran, and neither firing would mean
		// IdleRetryTurn turned the unit away before the targeting block.
		[Fact]
		public void MovingADiplomat_RecordsBothHalvesOfTheTargetingSplit()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 22; y <= 28; y++)
			for (int x = 38; x <= 48; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = Game.Instance.Players
				.Where(p => p is not null && Game.Instance.PlayerNumber(p) != 0
				         && p != Game.Instance.HumanPlayer).ToArray();
			Player mine = ps[0], theirs = ps[1];
			mine.Explore(42, 25, range: 10);
			Game.Instance.AddCity(mine, 0, 40, 25);
			Game.Instance.AddCity(theirs, 1, 45, 25);
			// Adjacent to the foreign city: IdleRetryTurn declines to defer a unit standing
			// beside one, so the move reaches the targeting block whatever the turn number.
			IUnit dip = Game.Instance.CreateUnit(UnitType.Diplomat, 44, 25,
				Game.Instance.PlayerNumber(mine))!;
			Sim.ClearTasks();
			TurnMetrics.Reset();

			AI.Instance(mine).Move(dip);

			Assert.Contains(TurnMetrics.Buckets(), b => b.Key == "dip:Target" && b.Calls == 1);
			Assert.Contains(TurnMetrics.Buckets(), b => b.Key == "dip:FirstStep" && b.Calls > 0);
		}

		// The global-tick probes must actually fire, or a 50-turn run reports nothing and
		// the `other_ms` question stays open for another evening.
		[Fact]
		public void TheWarmingScanProbe_Emits()
		{
			Sim.NewGame(width: 80, height: 50);
			TurnMetrics.Reset();

			// The most important of the tick probes, and the one testable without fighting
			// the per-turn Reset: every read of WarmingIndicator is a full-map scan, and
			// SideBar.DrawDemographics does one on roughly every other tick all game.
			int _ = Game.Instance.WarmingIndicator;

			Assert.Contains(TurnMetrics.Buckets(),
				b => b.Key == "tick:WarmingIndicatorScan" && b.Calls == 1);
		}

		// Reset runs at every turn wrap; a bucket that kept counting across turns would
		// make every reading cumulative and the whole log useless.
		[Fact]
		public void Reset_ClearsTheCounts()
		{
			var (owner, unit) = AUnitOnGrass();
			AI.Instance(owner).Move(unit);
			Assert.Contains(TurnMetrics.Buckets(), b => b.Calls > 0);

			TurnMetrics.Reset();

			Assert.All(TurnMetrics.Buckets(), b => Assert.Equal(0, b.Calls));
		}
	}
}
