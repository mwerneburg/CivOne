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
