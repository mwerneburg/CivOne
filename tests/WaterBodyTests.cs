// CivOne tests
//
// Sea units had no reachability oracle. Land units short-circuit an impossible route from
// ContinentId, but UnitClass.Water fell through to a full A* — and an unreachable sea
// target makes A* expand the ENTIRE connected ocean before conceding. Measured at 28us for
// a route that exists against 28.9ms for one that does not, which is why a Frigate move
// averaged 178ms.
//
// The failure mode to guard is UNDER-merging: a body that is too small refuses a legal
// route. Over-merging is safe (the short-circuit only ever answers an early NO), so these
// lean on the permissive side deliberately.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class WaterBodyTests
	{
		private static (Game, Player) DrownedWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				if (!Map.Instance[x, y].IsOcean) Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			return (g, p);
		}

		private static void Land(int x0, int y0, int w, int h)
		{
			for (int y = y0; y < y0 + h; y++)
			for (int x = x0; x < x0 + w; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
		}

		// A walled-off lake is its own body, and a ship outside cannot reach it.
		[Fact]
		public void AnEnclosedLake_IsADifferentWaterBodyFromTheOpenSea()
		{
			(Game g, Player p) = DrownedWorld();
			Land(60, 30, 11, 11);                      // solid block
			Land(65, 35, 1, 1);                        // ...then hollow one tile out
			Map.Instance.ChangeTileType(65, 35, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			byte openSea = Map.Instance[20, 25].OceanId;
			byte lake    = Map.Instance[65, 35].OceanId;

			Assert.True(Map.NamedOcean(openSea));
			Assert.True(Map.NamedOcean(lake));
			Assert.NotEqual(openSea, lake);
		}

		// The payoff: the planner refuses the impossible route without searching for it.
		[Fact]
		public void AShipCannotPathIntoAnEnclosedLake()
		{
			(Game g, Player p) = DrownedWorld();
			Land(60, 30, 11, 11);
			Map.Instance.ChangeTileType(65, 35, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			p.Explore(65, 35, range: 20);

			IUnit ship = g.CreateUnit(UnitType.Trireme, 20, 25, g.PlayerNumber(p))!;
			Assert.Null(Common.GotoStep(ship, 65, 35));
		}

		// ...and still routes a journey that IS possible. This is the half that breaks if
		// the fill is too aggressive.
		[Fact]
		public void AShipStillPathsAcrossTheOpenSea()
		{
			(Game g, Player p) = DrownedWorld();
			p.Explore(40, 25, range: 30);
			IUnit ship = g.CreateUnit(UnitType.Trireme, 20, 25, g.PlayerNumber(p))!;
			Assert.NotNull(Common.GotoStep(ship, 40, 25));
		}

		// Ships step diagonally, so two seas meeting at a corner are ONE body — the same
		// 4-vs-8-connected trap the land fill fell into.
		[Fact]
		public void SeasMeetingDiagonally_AreOneWaterBody()
		{
			(Game g, Player p) = DrownedWorld();
			// Two land blocks touching at a corner leave a diagonal gap between the seas.
			Land(30, 20, 10, 10);
			Land(40, 30, 10, 10);
			Map.Instance.RecalculateContinentsIfDirty();

			// (39,29) and (40,30) are land corners; the water at (40,29) and (39,30) meets
			// diagonally through that pinch.
			Assert.Equal(Map.Instance[40, 29].OceanId, Map.Instance[39, 30].OceanId);
		}

		// A coastal city is a canal: founding one that touches two seas must MERGE them,
		// or the planner refuses a route that just became legal. This is the under-merge
		// case and the only one that can produce a wrong answer.
		[Fact]
		public void ACoastalCityFoundedBetweenTwoSeas_JoinsThem()
		{
			(Game g, Player p) = DrownedWorld();
			// TWO isthmuses: the map wraps in X, so one wall leaves the seas joined round
			// the back. x=0 and x=40 cut the cylinder into a west basin and an east one.
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				Map.Instance.ChangeTileType(0, y, Terrain.Grassland1);
				Map.Instance.ChangeTileType(40, y, Terrain.Grassland1);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			byte west = Map.Instance[20, 25].OceanId;
			byte east = Map.Instance[60, 25].OceanId;
			Assert.NotEqual(west, east);            // sealed: two seas

			p.Explore(40, 25, range: 5);
			g.AddCity(p, 0, 40, 25);                // the canal

			Assert.Equal(Map.Instance[20, 25].OceanId, Map.Instance[60, 25].OceanId);
		}

		// Land units must be unaffected — they read ContinentId, not OceanId.
		[Fact]
		public void TheWaterFillDoesNotDisturbLandRouting()
		{
			(Game g, Player p) = DrownedWorld();
			Land(20, 20, 12, 12);
			Map.Instance.RecalculateContinentsIfDirty();
			p.Explore(25, 25, range: 12);

			IUnit u = g.CreateUnit(UnitType.Militia, 22, 22, g.PlayerNumber(p))!;
			Assert.NotNull(Common.GotoStep(u, 29, 29));
		}
	}
}
