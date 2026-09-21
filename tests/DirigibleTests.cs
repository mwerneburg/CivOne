// CivOne tests
//
// The dirigible is the answer to a trade route cut by somebody else's tube network.
//
// Sea tubes are claimed by whoever lays them first (Common.TubeBarred), so a rival line
// across your route is a wall a caravan has no right to cross — and a caravan that cannot
// reach a foreign city cannot trade with it. Flying over is the counterplay that does not
// require declaring war.
//
// It is UnitClass.Air but deliberately NOT a BaseUnitAir: that class disbands a unit that
// ends a turn away from a city or Carrier, which for a freighter drowns the cargo, and the
// long crossing is the entire point of the unit.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class DirigibleTests
	{
		private static (Game game, Player human, Dirigible ship) ADirigible()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.ChangeTileType(41, 25, Terrain.Ocean);
			Sim.ClearTasks();
			Player human = g.HumanPlayer;
			Dirigible d = (Dirigible)g.CreateUnit(UnitType.Dirigible, 40, 25, g.PlayerNumber(human))!;
			return (g, human, d);
		}

		[Fact]
		public void ItFliesAndCarries()
		{
			(_, _, Dirigible d) = ADirigible();

			Assert.Equal(UnitClass.Air, d.Class);
			Assert.Equal(UnitRole.Transport, d.Role);
			Assert.Equal(4, d.Cargo);
		}

		// The fuel rule is the reason it is not a BaseUnitAir. A freighter that vanishes at
		// the end of a long crossing takes its cargo with it.
		[Fact]
		public void ItCarriesNoFuelRule()
		{
			(_, _, Dirigible d) = ADirigible();

			Assert.False(d is BaseUnitAir, "a fuel rule would disband it mid-crossing");
		}

		// Every tile is a legal destination — that is what flying over a claimed tube means.
		[Fact]
		public void AClaimedSeaTubeDoesNotBarIt()
		{
			(Game g, _, Dirigible d) = ADirigible();
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ITile t = Map.Instance[41, 25];
			t.TransportTube = true;
			t.TubeOwner = g.PlayerNumber(ai);

			// The claim bars a LAND unit of ours...
			IUnit caravan = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(g.HumanPlayer))!;
			Assert.True(Common.TubeBarred(t, caravan.Owner));

			// ...and the airship goes over it.
			Assert.True(d.MoveTo(1, 0), "the dirigible was stopped by a tube it flies above");
		}

		// ── GoTo: a line the player can see ──────────────────────────────────────────

		// Walks GoTo to the goal by teleporting the unit along each step it is handed, so the
		// route is compared tile for tile without animations or move points in the way.
		private static (int x, int y)[] Route(IUnit unit, int gx, int gy)
		{
			var steps = new System.Collections.Generic.List<(int, int)>();
			for (int i = 0; i < 40 && !(unit.X == gx && unit.Y == gy); i++)
			{
				ITile? next = Common.GotoStep(unit, gx, gy);
				if (next is null) break;
				steps.Add((next.X, next.Y));
				unit.X = next.X; unit.Y = next.Y;
			}
			return steps.ToArray();
		}

		private static (Game g, Player human, Player ai, Dirigible d) AnOpenSky()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int x = 30; x <= 50; x++)
			for (int y = 15; y <= 35; y++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Sim.ClearTasks();
			Player human = g.HumanPlayer;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != human);
			Dirigible d = (Dirigible)g.CreateUnit(UnitType.Dirigible, 35, 20, g.PlayerNumber(human))!;
			return (g, human, ai, d);
		}

		// Reported from a game: dirigibles sailed round forests and hills, and one sent home to
		// a Colima-ish embarkation point tangled itself in Baja California. The planner costed
		// it like a ship — tile.Movement × 9 — and treated a rival's sea tube as a wall. It now
		// flies diagonally until level with the goal, then straight, whatever is underneath.
		[Fact]
		public void GoToFliesDiagonallyThenStraightOverAnything()
		{
			(Game g, _, Player ai, Dirigible d) = AnOpenSky();
			// Rough ground and a claimed sea tube, all on the line.
			Map.Instance.ChangeTileType(37, 22, Terrain.Mountains);
			Map.Instance.ChangeTileType(38, 23, Terrain.Hills);
			Map.Instance.ChangeTileType(39, 24, Terrain.Forest);
			Map.Instance.ChangeTileType(42, 24, Terrain.Mountains);
			Map.Instance.ChangeTileType(41, 24, Terrain.Ocean);
			Map.Instance[41, 24].TransportTube = true;
			Map.Instance[41, 24].TubeOwner = g.PlayerNumber(ai);

			var route = Route(d, 45, 24);

			Assert.Equal(new[] { (36, 21), (37, 22), (38, 23), (39, 24),
			                     (40, 24), (41, 24), (42, 24), (43, 24), (44, 24), (45, 24) }, route);
		}

		// It cannot fight, so it does not fly into a foreign stack: it steps round it and
		// rejoins the line.
		[Fact]
		public void GoToStepsRoundAForeignUnitAndRejoinsTheLine()
		{
			(Game g, _, Player ai, Dirigible d) = AnOpenSky();
			d.X = 35; d.Y = 25;
			g.CreateUnit(UnitType.Militia, 38, 25, g.PlayerNumber(ai));

			var route = Route(d, 42, 25);

			Assert.Equal(new[] { (36, 25), (37, 25), (38, 24), (39, 25), (40, 25), (41, 25), (42, 25) }, route);
		}

		// Unarmed. BaseUnit.Confront refused only unarmed LAND units, so a Dirigible moved by
		// hand onto a foreign unit fought it at strength 0 and lost its cargo with it.
		[Fact]
		public void ItWillNotAttack()
		{
			(Game g, _, Player ai, Dirigible d) = AnOpenSky();
			IUnit militia = g.CreateUnit(UnitType.Militia, 36, 20, g.PlayerNumber(ai))!;

			Assert.False(d.MoveTo(1, 0), "an unarmed airship flew into combat");
			Sim.Settle();

			Assert.Contains(d, g.GetUnits());
			Assert.Contains(militia, g.GetUnits());
		}

		// The rest of the air force had the planner's other half of the fault: charged for the
		// terrain under it, discounted for railways it cannot ride, and walled off by a rival's
		// sea tube. A Bomber on a straight run over a ridge of mountains flies the ridge...
		[Fact]
		public void AnAircraftFliesOverMountainsNotRoundThem()
		{
			(Game g, Player human, _, _) = AnOpenSky();
			for (int x = 36; x <= 44; x++) Map.Instance.ChangeTileType(x, 25, Terrain.Mountains);
			IUnit bomber = g.CreateUnit(UnitType.Bomber, 35, 25, g.PlayerNumber(human))!;

			var route = Route(bomber, 45, 25);

			Assert.Equal(Enumerable.Range(36, 10).Select(x => (x, 25)).ToArray(), route);
		}

		// ...and crosses a rival's tube line, which here runs the full height of the map, so
		// the only way round was the long way round the world.
		[Fact]
		public void AnAircraftIsNotWalledOffByARivalsTube()
		{
			(Game g, Player human, Player ai, _) = AnOpenSky();
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				Map.Instance.ChangeTileType(40, y, Terrain.Ocean);
				Map.Instance[40, y].TransportTube = true;
				Map.Instance[40, y].TubeOwner = g.PlayerNumber(ai);
			}
			IUnit bomber = g.CreateUnit(UnitType.Bomber, 35, 25, g.PlayerNumber(human))!;

			var route = Route(bomber, 45, 25);

			Assert.Equal(10, route.Length);
		}

		// ── Cargo: only what was put aboard ──────────────────────────────────────────

		// Reported from a game: dirigibles wandering round mountains picked up Settlers that
		// were building roads. On land, as in a city, a passenger boards by sentrying.
		[Fact]
		public void ItLeavesAWorkingSettlerWhereHeIs()
		{
			(Game g, Player human, Dirigible d) = ADirigible();
			IUnit settler = g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(human))!;
			Assert.False(settler.Sentry, "fixture: the settler is at work, not aboard");

			Assert.True(d.MoveTo(1, 0), "fixture: the move was refused");
			Sim.Settle();

			Assert.Equal(41, d.X);
			Assert.Equal((40, 25), (settler.X, settler.Y));
		}

		[Fact]
		public void ItCarriesASentriedPassengerFromDryLand()
		{
			(Game g, Player human, Dirigible d) = ADirigible();
			IUnit caravan = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(human))!;
			caravan.Sentry = true;

			Assert.True(d.MoveTo(1, 0), "fixture: the move was refused");
			Sim.Settle();

			Assert.Equal((41, 25), (caravan.X, caravan.Y));
		}

		// Over open water a land unit has nowhere else to be, sentried or not: leaving it
		// behind would drown it.
		[Fact]
		public void OverOpenWaterEveryPassengerComesAlong()
		{
			(Game g, Player human, Dirigible d) = ADirigible();
			Map.Instance.ChangeTileType(42, 25, Terrain.Ocean);
			d.X = 41; d.Y = 25;
			IUnit caravan = g.CreateUnit(UnitType.Caravan, 41, 25, g.PlayerNumber(human))!;
			caravan.Sentry = false;

			Assert.True(d.MoveTo(1, 0), "fixture: the move was refused");
			Sim.Settle();

			Assert.Equal((42, 25), (caravan.X, caravan.Y));
		}

		// ─── passengers do not answer alarms ─────────────────────────────────
		//
		// An enemy moving next to a human sentry wakes it (BaseUnit.MoveEnd). A passenger
		// shares its carrier's tile, so that rule reached into the hold — and waking a
		// passenger does not merely fail to help, it throws the unit overboard: Sentry is how
		// a passenger says it is aboard, so a woken one drops out of the manifest and is left
		// standing wherever the enemy happened to pass while the vessel carries on.
		//
		// Reported from a game as units falling out of dirigibles in funny but unhelpful
		// circumstances.
		private static (Game g, Player human, Dirigible d, IUnit cargo, IUnit enemy) AFlightPastAnEnemy()
		{
			(Game g, Player human, Dirigible d) = ADirigible();
			// Dry ground either side: the enemy is a land unit and has to be able to step.
			Map.Instance.ChangeTileType(41, 25, Terrain.Grassland1);
			Map.Instance.ChangeTileType(42, 25, Terrain.Grassland1);

			IUnit cargo = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(human))!;
			cargo.Sentry = true;

			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != human);
			IUnit enemy = g.CreateUnit(UnitType.Legion, 42, 25, g.PlayerNumber(ai))!;
			Assert.NotNull(enemy);
			Sim.ClearTasks();
			return (g, human, d, cargo, enemy);
		}

		[Fact]
		public void APassengerStaysAboardWhenTheVesselIsUnderOrders()
		{
			(_, _, Dirigible d, IUnit cargo, IUnit enemy) = AFlightPastAnEnemy();
			d.Goto = new System.Drawing.Point(20, 25);

			Assert.True(enemy.MoveTo(-1, 0), "fixture: the enemy's move was refused");
			Sim.Settle();

			Assert.Equal((41, 25), (enemy.X, enemy.Y));   // it really did arrive alongside
			Assert.True(cargo.Sentry, "the passenger was thrown overboard by a passing enemy");
		}

		// The control, and the reason the rule is narrow: a vessel with no orders is not going
		// anywhere the passenger would be stranded from, so the alarm still rings. Without
		// this the test above proves only that Sentry is a boolean nobody touched.
		[Fact]
		public void APassengerStillWakesWhenTheVesselHasNoOrders()
		{
			(_, _, Dirigible d, IUnit cargo, IUnit enemy) = AFlightPastAnEnemy();
			Assert.True(d.Goto.IsEmpty, "fixture: the vessel is meant to be idle");

			Assert.True(enemy.MoveTo(-1, 0), "fixture: the enemy's move was refused");
			Sim.Settle();

			Assert.False(cargo.Sentry, "an idle vessel's garrison slept through a raider");
		}

		// The rule lives on BaseUnit and keys off IBoardable, not off the airship: a sea
		// transport's cargo is dropped into the water by exactly the same wake.
		[Fact]
		public void ASeaTransportsCargoIsCoveredToo()
		{
			(Game g, Player human, _) = ADirigible();
			Map.Instance.ChangeTileType(41, 25, Terrain.Ocean);
			Map.Instance.ChangeTileType(42, 25, Terrain.Grassland1);
			Map.Instance.ChangeTileType(43, 25, Terrain.Grassland1);

			IUnit ship = g.CreateUnit(UnitType.Transport, 41, 25, g.PlayerNumber(human))!;
			Assert.True(ship is IBoardable, "fixture: the Transport is meant to carry");
			IUnit cargo = g.CreateUnit(UnitType.Caravan, 41, 25, g.PlayerNumber(human))!;
			cargo.Sentry = true;
			ship.Goto = new System.Drawing.Point(20, 25);

			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != human);
			// Starts two tiles off and walks to 42,25, which borders the ship at 41,25. It has
			// to actually ARRIVE alongside — an enemy whose move was refused wakes nobody, and
			// a test that tolerates a refused move is a test of nothing.
			IUnit enemy = g.CreateUnit(UnitType.Legion, 43, 25, g.PlayerNumber(ai))!;
			Sim.ClearTasks();

			Assert.True(enemy.MoveTo(-1, 0), "fixture: the enemy's move was refused");
			Sim.Settle();

			Assert.Equal((42, 25), (enemy.X, enemy.Y));
			Assert.True(cargo.Sentry, "the passenger was tipped into the sea by a passing enemy");
		}

		// Unloading over open water would put a land unit where it cannot stand.
		[Fact]
		public void ItWillNotUnloadOverOpenWater()
		{
			(Game g, Player human, Dirigible d) = ADirigible();
			byte me = g.PlayerNumber(human);
			IUnit cargo = g.CreateUnit(UnitType.Caravan, 41, 25, me)!;
			cargo.Sentry = true;
			d.X = 41; d.Y = 25;

			d.Unload();

			Assert.True(cargo.Sentry, "it was tipped into the sea");
		}
	}
}
