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
		// `westTerminal` / `eastTerminal` put a city on the shore at each mouth of the line.
		// They default ON because a tube without one is now unusable in that direction: a land
		// unit boards and leaves the undersea section at a city and nowhere else. Turning one
		// off is how the tests below check that the rule is really being enforced.
		private static (Game game, byte num) TwoShores(bool laytube = true,
			bool westTerminal = true, bool eastTerminal = true)
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

			if (westTerminal) g.AddCity(p, 5, 20, 20);
			if (eastTerminal) g.AddCity(p, 6, 40, 20);

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

		// A foreign unit parked in open water beside the line does not close it.
		//
		// Reported at turn 441 of a live game: a trans-Atlantic tube anchored on Acahay that
		// GoTo would not use, while walking the identical route by hand worked all the way to
		// Iberia. One Olvir HydroEngineer sat at (143,83) — open ocean, no tube — adjacent to
		// BOTH (142,83) and (143,82), two consecutive tiles of the line. MoveTo skips the
		// zone-of-control test outright when either end of a step is ocean; the planner's copy
		// had no such exemption, saw ZOC to ZOC, and refused. A sea tube is a one-tile corridor
		// with impassable water either side, so that one step took the whole crossing with it.
		//
		// Note what the fixture above had to do to avoid this: it disbands every rival unit,
		// because "a rival that lands beside the tube mouth throws zone of control across it".
		// That was this bug, worked around in the test rather than in the code.
		[Fact]
		public void AForeignUnitBesideTheTubeDoesNotCloseIt()
		{
			(Game g, byte num) = TwoShores();
			Player other = g.Players.First(p => p is not null && g.PlayerNumber(p) != num
			                                                  && g.PlayerNumber(p) != 0);
			// In the water beside the line, not on it: adjacent to (29,20), (30,20) and
			// (31,20), so two consecutive tube tiles are both under its zone of control.
			g.CreateUnit(UnitType.HydroEngineer, 30, 19, g.PlayerNumber(other), false);
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

		// ...and the planner agrees with the mover, which is the actual invariant. The bug was
		// not that ZOC is wrong, it is that two copies of one rule disagreed: whatever MoveTo
		// permits here, GotoStep must be willing to plan.
		[Fact]
		public void TheMoverAllowsTheStepThePlannerPlans()
		{
			(Game g, byte num) = TwoShores();
			Player other = g.Players.First(p => p is not null && g.PlayerNumber(p) != num
			                                                  && g.PlayerNumber(p) != 0);
			g.CreateUnit(UnitType.HydroEngineer, 30, 19, g.PlayerNumber(other), false);
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 29, 20, num, false)!;
			unit.MovesLeft = unit.Move;

			Assert.True(unit.MoveTo(1, 0), "MoveTo refused a step along the tube");
			ITile? step = Common.GotoStep(unit, 45, 20);
			Assert.NotNull(step);
		}

		// ── the tunnel has two ends ──────────────────────────────────────────
		// You board the undersea line at a city and you leave it at a city. Ingress was
		// already gated; egress was free, so a unit could enter at a terminal and climb out
		// onto any shore the line passed — which made the boarding rule decorative in one
		// direction and a sea tube a causeway after all.
		//
		// The shores here carry no city, so the tube in this fixture has no terminal at
		// either end: nothing may get on it from land, and nothing already on it may get off.
		// (20,19) is bare west shore, diagonally adjacent to the first tube tile — and the
		// terminal city at (20,20) is adjacent too, so this is not "nowhere to go": it is the
		// unit declining to surface anywhere but the station.
		[Fact]
		public void AUnitOnTheLineMayNotStepAshoreInOpenCountry()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 21, 20, num, false)!;
			unit.MovesLeft = unit.Move;

			Assert.False(unit.MoveTo(-1, -1), "the unit climbed out of the tunnel onto open shore");
			Assert.Equal((21, 20), (unit.X, unit.Y));
		}

		// ...while the terminal one tile further on is exactly where it may surface.
		[Fact]
		public void ACityOnTheShoreIsATerminal()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 21, 20, num, false)!;
			unit.MovesLeft = unit.Move;

			// The return value, not the position: an ALLOWED move starts a MoveUnit animation
			// and the unit's coordinates only change when that completes, which headless it
			// never does. A refusal is synchronous, which is why the tests above can check
			// that the unit stayed put and this one cannot.
			Assert.True(unit.MoveTo(-1, 0), "a unit could not step off the line into a terminal");
		}

		// And the planner has to know it too, or GoTo plans a landing the mover then refuses
		// and the unit sits being handed the same illegal step every turn. West mouth has its
		// terminal, east mouth does not: the line is boardable and goes nowhere.
		[Fact]
		public void ThePlannerWillNotRouteALandingInOpenCountry()
		{
			(Game g, byte num) = TwoShores(eastTerminal: false);
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 20, num, false)!;

			Assert.Null(Common.GotoStep(unit, 45, 20));
		}

		// The same crossing is planned the moment the far mouth has a terminal on it.
		[Fact]
		public void ATerminalOpensTheCrossingToThePlanner()
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

		// The mover and the planner must agree about boarding as well: a unit standing on bare
		// coast beside the mouth cannot get on, and GoTo must not pretend otherwise.
		[Fact]
		public void AUnitOnBareCoastCannotBoardTheLine()
		{
			(Game g, byte num) = TwoShores();
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 20, 19, num, false)!;
			unit.MovesLeft = unit.Move;

			Assert.False(unit.MoveTo(1, 1), "the unit boarded the line from open coast");
			Assert.Equal((20, 19), (unit.X, unit.Y));
		}

		// ── zone of control at a foreign city ────────────────────────────────
		// MoveTo exempts a step with ANY city at either end; the planner exempted only the
		// mover's own. The difference is narrow — a foreign city is usually a route's GOAL,
		// and the goal tile is exempt from every one of these tests anyway — so it takes a
		// corridor that runs THROUGH the city to see it at all. Which is the case that bit:
		// with the old clause the planner refused a step the mover would have taken, and a
		// one-tile corridor has no detour to offer.
		//
		// Land only at y=20 (x 18-26) plus the single tile the foreign soldier stands on, so
		// there is exactly one way past and the search cannot quietly go round.
		[Fact]
		public void ThePlannerApproachesAForeignCityLikeTheMoverDoes()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int x = 18; x <= 26; x++) Map.Instance.ChangeTileType(x, 20, Terrain.Grassland1);
			Map.Instance.ChangeTileType(23, 19, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player me = g.HumanPlayer;
			byte num = g.PlayerNumber(me);
			Player other = g.Players.First(p => p is not null && g.PlayerNumber(p) != num
			                                                  && g.PlayerNumber(p) != 0);
			me.Explore(22, 20, range: 12);
			g.AddCity(other, 0, 23, 20);
			// In the open beside the city: a garrison projects no zone of control, so the
			// soldier has to stand OUTSIDE for both tiles of the step to be covered.
			g.CreateUnit(UnitType.Musketeers, 23, 19, g.PlayerNumber(other), false);
			IUnit unit = g.CreateUnit(UnitType.Musketeers, 22, 20, num, false)!;
			// Every other rival unit goes, the blocker excepted — the starting settlers land
			// on this corridor and one of them sitting on the goal tile makes the search fail
			// for a reason that has nothing to do with zone of control. (It cost an hour.)
			// Repeated, because the rival start positions are not all placed by the time the
			// first pass runs, and this world is ten tiles of land — every civ in the game
			// lands on this corridor. One of them standing on the goal tile fails the search
			// for a reason that has nothing to do with zone of control. (It cost an hour.)
			for (int pass = 0; pass < 3; pass++)
				foreach (IUnit u in g.GetUnits().Where(u => u.Owner != num
				                                        && !(u.X == 23 && u.Y == 19)).ToArray())
					g.DisbandUnit(u);
			Sim.ClearTasks();

			ITile? step = Common.GotoStep(unit, 26, 20);

			Assert.NotNull(step);
			Assert.Equal((23, 20), (step!.X, step.Y));
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
