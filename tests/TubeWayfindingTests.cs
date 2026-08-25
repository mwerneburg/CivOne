// CivOne tests
//
// A transport tube joins two continents, and GoTo would not use it.
//
// Reported after a tube was laid from Iberia to New Brunswick: a unit ordered across it walks
// to the city at the Iberian end and stops. Put the same unit on the first tube tile by hand
// and the identical order works all the way to Lake Superior.
//
// The cause is the continent short-circuit in GotoStepInner. It exists to keep A* from
// flooding the map on an impossible crossing, and it answers "impossible" from one byte: two
// NAMED continents that differ have no land path between them. A tube is exactly the thing
// that makes that false. Standing on the tube, the source tile is ocean and carries no named
// continent, so the short-circuit never fires and the search runs — which is why the second
// half of the crossing always worked.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class TubeWayfindingTests
	{
		// Two landmasses in an otherwise empty ocean, joined along y=20 by a tube. West is
		// x 10-20, east x 40-50; the tube runs x 21-39.
		private static (Game game, byte num) TwoShores(bool laytube = true)
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 10; y <= 30; y++)
			{
				for (int x = 10; x <= 20; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				for (int x = 40; x <= 50; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			if (laytube)
				for (int x = 21; x <= 39; x++) Map.Instance[x, 20].TransportTube = true;

			Player p = g.HumanPlayer;
			p.Explore(30, 20, range: 40);

			// Clear the other civs off the board. Their start positions are random, and a
			// rival that lands beside the tube mouth throws zone of control across it — which
			// blocks the crossing for a reason that has nothing to do with what is under test.
			// (It cost an hour: the first fixture had a Sumerian at 21,19.)
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner != g.PlayerNumber(p)).ToArray())
				g.DisbandUnit(u);

			Sim.ClearTasks();
			return (g, g.PlayerNumber(p));
		}

		// The two shores really are different named continents — the whole short-circuit turns
		// on that, and a fixture where they came out equal would pass every test below while
		// proving nothing.
		[Fact]
		public void TheTwoShoresAreDifferentContinents()
		{
			(Game g, byte num) = TwoShores();

			byte west = Map.Instance[15, 20].ContinentId, east = Map.Instance[45, 20].ContinentId;
			Assert.True(Map.NamedContinent(west) && Map.NamedContinent(east),
				$"west={west} east={east}: the fixture is not exercising the short-circuit");
			Assert.NotEqual(west, east);
		}

		// The report. A unit on the west shore, ordered to the east shore, gets a first step.
		[Fact]
		public void AUnitOnOneShoreIsRoutedAcrossTheTube()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;

			ITile? step = Common.GotoStep(unit, 45, 20);

			Assert.NotNull(step);
		}

		// ...and the route it plans actually reaches the far shore rather than wandering the
		// home coast. Walked here in full, because a single first step proves very little.
		[Fact]
		public void TheRouteArrivesOnTheFarShore()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;

			for (int i = 0; i < 200 && (unit.X != 45 || unit.Y != 20); i++)
			{
				ITile? step = Common.GotoStep(unit, 45, 20);
				Assert.NotNull(step);
				unit.X = step!.X;
				unit.Y = step.Y;
			}

			Assert.Equal((45, 20), (unit.X, unit.Y));
		}

		// The half that always worked, kept so a fix that breaks it is caught here.
		[Fact]
		public void AUnitAlreadyOnTheTubeIsStillRouted()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 25, 20, num, false)!;

			Assert.NotNull(Common.GotoStep(unit, 45, 20));
		}

		// Without a tube there is no crossing, and the short-circuit must still say so — this
		// is the futile-search guard the optimisation was written for. Deleting it outright
		// would pass every other test in this file.
		[Fact]
		public void WithoutATubeTheCrossingIsStillRefused()
		{
			(Game g, byte num) = TwoShores(laytube: false);
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;

			Assert.Null(Common.GotoStep(unit, 45, 20));
		}

		// A tube that stops short of the far shore links nothing, and must not switch the
		// guard off for the whole map.
		[Fact]
		public void AnUnfinishedTubeLinksNothing()
		{
			(Game g, byte num) = TwoShores(laytube: false);
			for (int x = 21; x <= 30; x++) Map.Instance[x, 20].TransportTube = true;
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;

			Assert.Null(Common.GotoStep(unit, 45, 20));
		}

		// Pillaging the tube takes the link away again: the answer has to follow the map. The
		// same unit is used deliberately — it is holding a committed plan across the cut, and
		// that plan must not go on handing it a step into open ocean.
		[Fact]
		public void CuttingTheTubeClosesTheRouteAgain()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;
			Assert.NotNull(Common.GotoStep(unit, 45, 20));

			Map.Instance[30, 20].TransportTube = false;

			bool arrived = false;
			for (int i = 0; i < 200; i++)
			{
				ITile? step = Common.GotoStep(unit, 45, 20);
				if (step is null) break;
				unit.X = step.X;
				unit.Y = step.Y;
				if (unit.X == 45 && unit.Y == 20) { arrived = true; break; }
			}

			Assert.False(arrived, "the unit walked across a tube that had been cut");
			Assert.True(Map.Instance[unit.X, unit.Y].TransportTube || !Map.Instance[unit.X, unit.Y].IsOcean,
				$"it stopped at {unit.X},{unit.Y}, which is open water");
		}

		// The answer is cached, so the tile that completes the crossing has to invalidate it.
		// Asking first is the point of this test: that builds the cache while the tube is one
		// tile short, and a fix that only recomputes on continent renumbering would answer
		// "no route" for the rest of the game.
		[Fact]
		public void LayingTheLastTileOpensTheRouteAtOnce()
		{
			(Game g, byte num) = TwoShores(laytube: false);
			for (int x = 21; x <= 38; x++) Map.Instance[x, 20].TransportTube = true;
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;
			Assert.Null(Common.GotoStep(unit, 45, 20));

			Map.Instance[39, 20].TransportTube = true;

			Assert.NotNull(Common.GotoStep(unit, 45, 20));
		}

		// A floating city is a stepping stone like any tube tile — the Hydro Engineer's own
		// pedia page says the two form one corridor — so founding one in a gap completes the
		// crossing, and the cache has to notice that too.
		[Fact]
		public void AFloatingCityCompletesTheChain()
		{
			(Game g, byte num) = TwoShores(laytube: false);
			for (int x = 21; x <= 30; x++) Map.Instance[x, 20].TransportTube = true;
			for (int x = 32; x <= 39; x++) Map.Instance[x, 20].TransportTube = true;
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;
			Assert.Null(Common.GotoStep(unit, 45, 20));

			g.AddCity(g.HumanPlayer, 0, 31, 20);

			Assert.NotNull(Common.GotoStep(unit, 45, 20));
		}

		// A ship is not affected either way. The short-circuit has a separate ocean oracle and
		// the tube is not a canal.
		[Fact]
		public void ShipsAreUnaffected()
		{
			(Game g, byte num) = TwoShores();
			IUnit ship = g.CreateUnit(UnitType.Trireme, 25, 21, num, false)!;

			Assert.NotNull(Common.GotoStep(ship, 30, 25));
		}
	}
}
