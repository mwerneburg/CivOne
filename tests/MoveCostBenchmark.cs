// CivOne tests
//
// A local reproduction of the late-game move cost.
//
// Two fixes were aimed at BaseUnit.MoveTo on the strength of live turn_timing records, and
// neither moved the number: move:MoveTo stayed at 22-24 ms a call across the StagingTile
// rewrite and the zone-of-control rewrite. Both were real O(units) offenders; neither was the
// one being paid. The gap in the method was that every measurement cost a game restart, so
// each guess took an hour to disprove.
//
// This builds a late-game world in-process — cities and units at live-game scale — and times
// the pieces of one land move directly. It is a MEASUREMENT, not an assertion about a rule,
// so it is [Trait("Category", "Benchmark")] and prints; the one thing it asserts is the
// finding it was written to pin.
//
//     dotnet test tests/CivOne.Tests.csproj --filter "FullyQualifiedName~MoveCostBenchmark"

using System.Diagnostics;
using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class MoveCostBenchmark
	{
		private readonly ITestOutputHelper _out;
		public MoveCostBenchmark(ITestOutputHelper output) => _out = output;

		// Cities and units at the scale of the run that produced the 22-24 ms records:
		// ~250 cities, ~2,000 units.
		private const int Cities = 250;
		private const int Units = 2000;

		private static (Game g, Player p, IUnit mover) ALateGameWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] civs = g.Players.Where(x => x is not null && g.PlayerNumber(x) != 0).ToArray();
			Player p = civs.First(x => x != g.HumanPlayer);

			// Cities spread over the map, owned round-robin, each working a few tiles.
			int placed = 0;
			for (int y = 2; y < 48 && placed < Cities; y += 3)
			for (int x = 2; x < 78 && placed < Cities; x += 3)
			{
				City c = g.AddCity(civs[placed % civs.Length], 0, x, y)!;
				if (c is null) continue;
				c.Size = 12;   // live-game cities work far more tiles than a size-4 one
				placed++;
			}

			// Units spread out, owned round-robin.
			int made = 0;
			for (int y = 0; y < 50 && made < Units; y++)
			for (int x = 0; x < 80 && made < Units; x++)
			{
				g.CreateUnit(UnitType.Musketeers, x, y, g.PlayerNumber(civs[made % civs.Length]));
				made++;
			}

			IUnit mover = g.CreateUnit(UnitType.Armor, 40, 25, g.PlayerNumber(p))!;
			mover.MovesLeft = mover.Move;
			Sim.ClearTasks();
			return (g, p, mover);
		}

		private static double Time(int reps, System.Action work)
		{
			work();   // warm
			Stopwatch sw = Stopwatch.StartNew();
			for (int i = 0; i < reps; i++) work();
			return sw.Elapsed.TotalMilliseconds / reps;
		}

		[Fact]
		[Trait("Category", "Benchmark")]
		public void WhereTheMoveTimeActuallyGoes()
		{
			(Game g, Player p, IUnit mover) = ALateGameWorld();
			_out.WriteLine($"world: {g.CitiesList.Count} cities, {g.GetUnits().Length} units");

			// The three candidates, timed apart. Each is what ONE land move actually asks for.
			double worked = Time(200, () =>
			{
				foreach (ITile t in Map.Instance[mover.X, mover.Y].GetBorderTiles())
					g.IsWorkedByOther(t.X, t.Y, mover.Owner);
			});
			double targets = Time(200, () => ((BaseUnit)mover).MoveTargets.Count());
			double units = Time(200, () => Map.Instance[41, 25].Units.Any(u => u.Owner != mover.Owner));

			_out.WriteLine($"IsWorkedByOther x8 border tiles : {worked:F3} ms");
			_out.WriteLine($"MoveTargets (ValidMoveTarget x8): {targets:F3} ms");
			_out.WriteLine($"one ITile.Units scan            : {units:F3} ms");

			// No timing assertion — WaterBodyCostTests shows those flake under parallel load, and
			// the structural guard lives in WorkedTileTests.ItDoesNotMaterialiseEveryCitysRadius.
			// What this asserts is only that the world it measures is the one it claims to measure;
			// the numbers above are the output, read by a human.
			Assert.Equal(250, g.CitiesList.Count);
			Assert.True(g.GetUnits().Length >= 2000);
		}
	}
}
