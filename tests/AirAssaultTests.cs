// CivOne tests
//
// The air assault (the user, Oct 2026: "loading up a Diplomat and two of their best offensive
// units onto a Dirigible and heading off"). A target across water was never picked at all —
// PickAttackTarget kept to our own landmass — so an island civ had no war to fight: Fascist
// Japan was at war for a century and never chose a target.
//
// With Flight, the group gathers in the city nearest the target, boards a Dirigible, and is
// set down on the staging tile beside the walls, where the strike-group rules take over.

using System.Drawing;
using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class AirAssaultTests : System.IDisposable
	{
		private readonly bool _auto;
		public AirAssaultTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// Two islands: ours (cities at 24,25 and 28,25 — the muster) and theirs (target at 52,25).
		private static (Game g, Player us, City muster, City target, AI ai) AnIslandWar(bool flight = true, int east = 48)
		{
			Sim.NewGame(width: east + 32 > 80 ? 200 : 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= east + 22; x++)
			{
				bool land = y >= 20 && y <= 30 && ((x >= 20 && x <= 32) || (x >= east && x <= east + 12));
				Map.Instance.ChangeTileType(x, y, land ? Terrain.Grassland1 : Terrain.Ocean);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player us = ps[0], them = ps[1];
			foreach (Player p in new[] { us, them }) { p.Government = new CivOne.Governments.Monarchy(); p.Explore(40, 25, range: 25); p.Explore(east + 4, 25, range: 10); }
			us.AddAdvance(new CivOne.Advances.Automobile(), false);
			us.AddAdvance(new CivOne.Advances.Writing(), false);
			if (flight) us.AddAdvance(new CivOne.Advances.Flight(), false);
			City far = g.AddCity(us, 0, 24, 25)!;
			City muster = g.AddCity(us, 1, 28, 25)!;
			foreach (City c in new[] { far, muster }) { c.Size = 6; g.CreateUnit(UnitType.Musketeers, c.X, c.Y, g.PlayerNumber(us)); }
			City target = g.AddCity(them, 2, east + 4, 25)!;
			target.Size = 4;
			g.CreateUnit(UnitType.Phalanx, east + 4, 25, g.PlayerNumber(them));
			Assert.NotEqual(muster.Tile.ContinentId, target.Tile.ContinentId);
			Assert.True(Map.NamedContinent(target.Tile.ContinentId), "fixture: the target island has no continent");
			us.DeclareWar(them);
			Sim.ClearTasks();
			return (g, us, muster, target, AI.Instance(us)!);
		}

		private static IUnit Make(Game g, Player p, UnitType t, int x, int y) => g.CreateUnit(t, x, y, g.PlayerNumber(p))!;

		private static void Assign(AI ai, IUnit u) =>
			typeof(AI).GetMethod("AssignMission", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(ai, new object[] { u });

		private static City? Target(AI ai) =>
			(City?)typeof(AI).GetField("_attackTarget", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(ai);

		// ── the target ───────────────────────────────────────────────────────

		[Fact]
		public void WithFlightAnIslandPicksATargetOverseas()
		{
			(Game _, Player _, City _, City target, AI ai) = AnIslandWar();

			ai.ConsiderWar();

			Assert.Same(target, Target(ai));
		}

		[Fact]
		public void WithoutFlightItDoesNot()
		{
			(Game _, Player _, City _, City _, AI ai) = AnIslandWar(flight: false);

			ai.ConsiderWar();

			Assert.Null(Target(ai));
		}

		// Once chosen it is held, not re-picked each turn: an airship already in the air must
		// not be sent somewhere else because a weaker city came into view.
		[Fact]
		public void TheOverseasTargetIsHeld()
		{
			(Game g, Player _, City _, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			City weaker = g.AddCity(target.Player, 3, 56, 28)!;   // no defenders at all

			ai.ConsiderWar();

			Assert.Same(target, Target(ai));
			Assert.NotSame(weaker, Target(ai));
		}

		// Past four turns' flight it is not a target: the nearest of our cities is 66 away.
		[Fact]
		public void NotBeyondAirReach()
		{
			(Game _, Player _, City muster, City target, AI ai) = AnIslandWar(east: 90);
			Assert.True(Common.DistanceToTile(muster.X, muster.Y, target.X, target.Y) > AI.AirReach, "fixture: within reach");

			ai.ConsiderWar();

			Assert.Null(Target(ai));
		}

		// Nowhere dry beside it, nowhere to land: an Olvir city on open water is not a target.
		[Fact]
		public void ACityWithNoDryGroundBesideItIsNotATarget()
		{
			(Game _, Player _, City _, City target, AI ai) = AnIslandWar();
			foreach (ITile t in target.Tile.GetBorderTiles()) Map.Instance.ChangeTileType(t.X, t.Y, Terrain.Ocean);

			ai.ConsiderWar();

			Assert.Null(Target(ai));
		}

		// ── the muster ───────────────────────────────────────────────────────

		[Fact]
		public void TheMusterCityRaisesADirigibleADiplomatAndAttackers()
		{
			(Game _, Player _, City muster, City _, AI ai) = AnIslandWar();

			ai.ConsiderWar();

			var queue = muster.ProductionQueue.ToArray();
			Assert.Contains(queue, p => p is Dirigible);
			Assert.Contains(queue, p => p is Diplomat);
			Assert.Contains(queue, p => p is IUnit { Role: UnitRole.LandAttack });
		}

		[Fact]
		public void AnAttackerWalksToTheMusterAndBoards()
		{
			(Game g, Player us, City muster, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit walker = Make(g, us, UnitType.Armor, 24, 25);
			IUnit there = Make(g, us, UnitType.Armor, muster.X, muster.Y);

			Assign(ai, walker);
			Assign(ai, there);

			Assert.Equal(new Point(muster.X, muster.Y), walker.Goto);
			Assert.True(there.Sentry, "an attacker in the muster city did not board");
		}

		// The turn-start wake must not turn out a group already aboard, or the airship can
		// leave before they board again.
		[Fact]
		public void TheWakeLeavesPassengersAboard()
		{
			(Game g, Player us, City muster, City _, AI ai) = AnIslandWar();
			IUnit aboard = Make(g, us, UnitType.Armor, muster.X, muster.Y);
			aboard.Sentry = true;

			ai.ConsiderWar();

			Assert.True(aboard.Sentry, "a passenger was woken off the airship");
		}

		[Fact]
		public void ADiplomatBoardsAtTheMuster()
		{
			(Game g, Player us, City muster, City _, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit spy = Make(g, us, UnitType.Diplomat, muster.X, muster.Y);

			Assign(ai, spy);

			Assert.True(spy.Sentry, "the Diplomat did not board");
		}

		// ── the flight ───────────────────────────────────────────────────────

		// Aircraft are not cargo. A Bomber reads as LandAttack, and the ones in the muster city
		// were put to sleep "aboard" and counted toward the group.
		[Fact]
		public void ABomberIsNotPutAboard()
		{
			(Game g, Player us, City muster, City _, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit bomber = Make(g, us, UnitType.Bomber, muster.X, muster.Y);
			Assert.Equal(UnitRole.LandAttack, bomber.Role);

			Assign(ai, bomber);

			Assert.False(bomber.Sentry, "a Bomber was put aboard an airship");
		}

		// ...and three Bombers are not a group: the muster still raises attackers.
		[Fact]
		public void BombersDoNotCountTowardTheGroup()
		{
			(Game g, Player us, City muster, City _, AI ai) = AnIslandWar();
			for (int i = 0; i < 3; i++) Make(g, us, UnitType.Bomber, muster.X, muster.Y);

			ai.ConsiderWar();

			Assert.Contains(muster.ProductionQueue, p => p is IUnit { Role: UnitRole.LandAttack, Class: UnitClass.Land });
		}

		// ...nor counted as a group waiting to fly.
		[Fact]
		public void BombersInTheMusterDoNotFillTheAirship()
		{
			(Game g, Player us, City muster, City _, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit ship = Make(g, us, UnitType.Dirigible, muster.X, muster.Y);
			for (int i = 0; i < 3; i++) Make(g, us, UnitType.Bomber, muster.X, muster.Y).Sentry = true;

			Assign(ai, ship);

			Assert.True(ship.Goto.IsEmpty, $"flew off with no troops, for {ship.Goto}");
		}

		// Turned back short of the landing (blocked, or the turn ran out), a loaded airship
		// flies on to the target rather than carrying its troops home.
		[Fact]
		public void ALoadedAirshipAwayFromTheMusterFliesOn()
		{
			(Game g, Player us, City _, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit ship = Make(g, us, UnitType.Dirigible, 40, 25);   // over the sea, halfway
			Make(g, us, UnitType.Armor, 40, 25).Sentry = true;

			Assign(ai, ship);

			Assert.Equal(1, Common.DistanceToTile(ship.Goto.X, ship.Goto.Y, target.X, target.Y));
		}

		// An AI unit put to sleep never wakes, and Japan's airships were all asleep: eight
		// attackers aboard at the muster, and nothing to fly them (Oct 2026).
		[Fact]
		public void ASleepingAirshipIsWokenForTheLift()
		{
			(Game g, Player us, City _, City _, AI ai) = AnIslandWar();
			IUnit ship = Make(g, us, UnitType.Dirigible, 24, 25);
			ship.Sentry = true;

			ai.ConsiderWar();

			Assert.False(ship.Sentry, "the airship slept through the war");
		}

		// Two aboard is not a group: the airship waits.
		[Fact]
		public void TheDirigibleWaitsForAFullGroup()
		{
			(Game g, Player us, City muster, City _, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit ship = Make(g, us, UnitType.Dirigible, muster.X, muster.Y);
			for (int i = 0; i < 2; i++) Make(g, us, UnitType.Armor, muster.X, muster.Y).Sentry = true;

			Assign(ai, ship);

			Assert.True(ship.Goto.IsEmpty, $"left with two aboard, for {ship.Goto}");
		}

		// Beside the walls but not on the staging tile (it filled while the airship was in the
		// air): set them down here rather than hovering until they are shot out of the sky.
		[Fact]
		public void AnyDryTileBesideTheTargetWillDo()
		{
			(Game g, Player us, City muster, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			for (int i = 0; i < 4; i++) Make(g, us, UnitType.Armor, 51, 24);   // the staging tile
			IUnit ship = Make(g, us, UnitType.Dirigible, 51, 26);
			IUnit[] armor = Enumerable.Range(0, 3).Select(_ => Make(g, us, UnitType.Armor, 51, 26)).ToArray();
			foreach (IUnit a in armor) a.Sentry = true;

			Assign(ai, ship);

			Assert.All(armor, a => Assert.False(a.Sentry, "still aboard beside the walls"));
		}

		// The enemy on every tile beside the walls: set them down one ring out rather than
		// carry them home.
		[Fact]
		public void WithTheWallsSurroundedItLandsOneRingOut()
		{
			(Game g, Player us, City _, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			foreach (ITile t in target.Tile.GetBorderTiles())
				g.CreateUnit(UnitType.Phalanx, t.X, t.Y, target.Owner);
			IUnit ship = Make(g, us, UnitType.Dirigible, 40, 25);
			Make(g, us, UnitType.Armor, 40, 25).Sentry = true;

			Assign(ai, ship);

			Assert.Equal(2, Common.DistanceToTile(ship.Goto.X, ship.Goto.Y, target.X, target.Y));
			Assert.False(Map.Instance[ship.Goto.X, ship.Goto.Y].IsOcean);
		}

		// ...and once there, it sets them down.
		[Fact]
		public void AtTheOuterLandingItUnloads()
		{
			(Game g, Player us, City _, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			foreach (ITile t in target.Tile.GetBorderTiles())
				g.CreateUnit(UnitType.Phalanx, t.X, t.Y, target.Owner);
			IUnit ship = Make(g, us, UnitType.Dirigible, 40, 25);
			IUnit armor = Make(g, us, UnitType.Armor, 40, 25);
			armor.Sentry = true;
			Assign(ai, ship);
			Point at = ship.Goto;
			ship.X = armor.X = at.X; ship.Y = armor.Y = at.Y;
			ship.Goto = Point.Empty;

			Assign(ai, ship);

			Assert.False(armor.Sentry, "carried on past its landing");
		}

		// Three aboard: it flies them over and sets them down beside the target, ready to go in.
		[Fact]
		public void AFullGroupIsFlownOverAndSetDownBesideTheTarget()
		{
			(Game g, Player us, City muster, City target, AI ai) = AnIslandWar();
			ai.ConsiderWar();
			IUnit ship = Make(g, us, UnitType.Dirigible, muster.X, muster.Y);
			IUnit[] armor = Enumerable.Range(0, 3).Select(_ => Make(g, us, UnitType.Armor, muster.X, muster.Y)).ToArray();
			foreach (IUnit a in armor) a.Sentry = true;

			for (int turn = 0; turn < 6 && armor.Any(a => a.Sentry); turn++)
			{
				ship.MovesLeft = ship.Move;
				for (int step = 0; step < 40 && ship.MovesLeft > 0; step++) { ai.Move(ship); Sim.Settle(); }
			}

			Assert.All(armor, a => Assert.Equal(1, Common.DistanceToTile(a.X, a.Y, target.X, target.Y)));
			Assert.All(armor, a => Assert.False(a.Sentry, "still aboard"));
			Assert.Equal(target.Tile.ContinentId, armor[0].Tile.ContinentId);
			Assert.Equal(new Point(muster.X, muster.Y), ship.Goto);   // and home for the next lift
		}
	}
}
