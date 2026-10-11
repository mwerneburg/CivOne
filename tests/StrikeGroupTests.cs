// CivOne tests
//
// Strike groups (the user, Oct 2026: "I never see an NPC put three of them in a little group
// and target a city — this is what I do when attacking them"). Measured on the d721724c save,
// the Fascist Lakota had 38 attackers at war and one ever reached its target: the rest stood
// fortified 28 tiles away for thirty turns, their cities building Granaries.
//
// Four parts: fortified attackers are woken for the target; a short group is raised at the
// front of the nearest cities' queues; attackers go in together from the staging tile; and a
// Diplomat runs ahead of them.

using System.Drawing;
using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class StrikeGroupTests : System.IDisposable
	{
		private readonly bool _auto;
		public StrikeGroupTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// The aggressor's cities at 30..34,25; the victim's city at 50,25 with one defender.
		private static (Game g, Player us, Player them, City target, AI ai) AWar()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player us = ps[0], them = ps[1];
			foreach (Player p in new[] { us, them }) { p.Government = new CivOne.Governments.Monarchy(); p.Explore(40, 25, range: 25); }
			us.AddAdvance(new CivOne.Advances.Automobile(), false);
			us.AddAdvance(new CivOne.Advances.Writing(), false);
			int id = 0;
			foreach (int x in new[] { 30, 34 })
			{
				City c = g.AddCity(us, id++, x, 25)!;
				c.Size = 6;
				g.CreateUnit(UnitType.Musketeers, x, 25, g.PlayerNumber(us));
				g.CreateUnit(UnitType.Musketeers, x, 25, g.PlayerNumber(us));
			}
			City target = g.AddCity(them, id++, 50, 25)!;
			target.Size = 4;
			g.CreateUnit(UnitType.Phalanx, 50, 25, g.PlayerNumber(them));
			us.DeclareWar(them);
			Sim.ClearTasks();
			return (g, us, them, target, AI.Instance(us)!);
		}

		private static IUnit Armor(Game g, Player p, int x, int y) => g.CreateUnit(UnitType.Armor, x, y, g.PlayerNumber(p))!;

		private static void Assign(AI ai, IUnit u) =>
			typeof(AI).GetMethod("AssignMission", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(ai, new object[] { u });

		private static void SetTarget(AI ai, City c) =>
			typeof(AI).GetField("_attackTarget", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ai, c);

		// ── wake ─────────────────────────────────────────────────────────────

		[Fact]
		public void FortifiedAttackersAreWokenForTheTarget()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			IUnit[] armor = Enumerable.Range(0, 3).Select(_ => Armor(g, us, 36, 25)).ToArray();
			foreach (IUnit a in armor) a.Fortify = true;
			Assert.All(armor, a => Assert.True(a.Fortify, "fixture: not fortified"));

			ai.ConsiderWar();

			Assert.All(armor, a => Assert.False(a.Fortify, "an attacker stayed fortified with a target to hit"));
		}

		// ── raise ────────────────────────────────────────────────────────────

		[Fact]
		public void AShortGroupIsRaisedInTheNearestCityWithoutLosingItsShields()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			City nearest = us.Cities.OrderBy(c => Common.DistanceToTile(c.X, c.Y, target.X, target.Y)).First();
			nearest.SetProduction(new CivOne.Buildings.Granary());
			nearest.Shields = 20;

			ai.ConsiderWar();

			Assert.IsType<CivOne.Buildings.Granary>(nearest.CurrentProduction);
			Assert.Equal(20, nearest.Shields);
			Assert.True(nearest.ProductionQueue.FirstOrDefault() is IUnit { Role: UnitRole.LandAttack },
				$"next up is {nearest.ProductionQueue.FirstOrDefault()?.GetType().Name ?? "nothing"}");
		}

		// ...and not when the group is already there.
		[Fact]
		public void AFullGroupRaisesNothing()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			for (int i = 0; i < 4; i++) Armor(g, us, 36, 25);
			City nearest = us.Cities.OrderBy(c => Common.DistanceToTile(c.X, c.Y, target.X, target.Y)).First();
			nearest.SetProduction(new CivOne.Buildings.Granary());

			ai.ConsiderWar();

			Assert.False(nearest.ProductionQueue.FirstOrDefault() is IUnit { Role: UnitRole.LandAttack });
		}

		// ── go in together ───────────────────────────────────────────────────

		// Three already massed beside the city: a fourth, far off, still gathers with them
		// rather than marching straight at the walls on its own.
		[Fact]
		public void ALateArrivalGathersRatherThanAttackingAlone()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			for (int i = 0; i < 3; i++) Armor(g, us, 49, 25);
			IUnit late = Armor(g, us, 36, 25);
			SetTarget(ai, target);

			Assign(ai, late);

			Assert.Equal(new Point(49, 25), late.Goto);
		}

		// Two beside the city are not yet a group of three: they wait.
		[Fact]
		public void TwoDoNotGoInAlone()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			IUnit a = Armor(g, us, 49, 25);
			Armor(g, us, 49, 25);
			SetTarget(ai, target);

			Assign(ai, a);

			Assert.Equal(new Point(49, 25), a.Goto);
		}

		// Three do.
		[Fact]
		public void ThreeGoIn()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			IUnit a = Armor(g, us, 49, 25);
			Armor(g, us, 49, 25);
			Armor(g, us, 49, 25);
			SetTarget(ai, target);
			Assign(ai, a);

			Assert.Equal(new Point(50, 25), a.Goto);
		}

		// ── the raid ─────────────────────────────────────────────────────────

		// Two attackers cannot take it, and no more are on the way yet: they burn its fields
		// rather than stand about (the user, Oct 2026: "sabotage its surroundings").
		[Fact]
		public void TooFewToTakeItRaidItsFields()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			Map.Instance[49, 24].Irrigation = true;
			IUnit a = Armor(g, us, 49, 24);
			Armor(g, us, 49, 25);
			SetTarget(ai, target);

			Assign(ai, a);

			Assert.False(Map.Instance[49, 24].Irrigation, "the field beside the walls was left standing");
		}

		// ...walking to the nearest improved tile in its radius when not standing on one...
		[Fact]
		public void ARaiderWalksToTheNearestImprovedField()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			Map.Instance[51, 27].Mine = true;
			IUnit a = Armor(g, us, 49, 25);
			SetTarget(ai, target);

			Assign(ai, a);

			Assert.Equal(new Point(51, 27), a.Goto);
		}

		// ...but never our own land.
		[Fact]
		public void ARaiderSparesOurOwnFields()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			g.AddCity(us, 7, 47, 27);
			Map.Instance[48, 26].Irrigation = true;   // within 2 of the target AND of our new city
			IUnit a = Armor(g, us, 48, 26);
			SetTarget(ai, target);

			Assign(ai, a);

			Assert.True(Map.Instance[48, 26].Irrigation, "pillaged our own city's land");
		}

		// Enough to take it: no raiding, the group goes in.
		[Fact]
		public void AFullGroupDoesNotStopToRaid()
		{
			(Game g, Player us, Player _, City target, AI ai) = AWar();
			Map.Instance[49, 25].Irrigation = true;
			IUnit a = Armor(g, us, 49, 25);
			Armor(g, us, 49, 25);
			Armor(g, us, 49, 25);
			SetTarget(ai, target);

			Assign(ai, a);

			Assert.True(Map.Instance[49, 25].Irrigation);
			Assert.Equal(new Point(50, 25), a.Goto);
		}

		// ── measured ─────────────────────────────────────────────────────────

		// Captures reach the decisions log, so a run can say whether an army ever took anything.
		[Fact]
		public void ACaptureIsWrittenToTheDecisionsLog()
		{
			(Game g, Player us, Player them, City _, AI _) = AWar();
			int before = Sim.DecisionLineCount("\"conquest\"");

			g.RecordConquest(us, them);

			string line = Sim.AwaitDecisionLine("\"conquest\"", after: before);
			Assert.Contains($"\"taker\":\"{us.Civilization.NamePlural}\"", line);
			Assert.Contains($"\"victim\":\"{them.Civilization.NamePlural}\"", line);
		}

		// ── the vanguard ─────────────────────────────────────────────────────

		// A Diplomat heads for the attack target, not merely the nearest foreign city.
		[Fact]
		public void ADiplomatRunsAheadToTheTarget()
		{
			(Game g, Player us, Player them, City target, AI ai) = AWar();
			Player bystander = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer && p != us && p != them);
			bystander.Explore(40, 25, range: 5);
			g.AddCity(bystander, 9, 40, 25);   // nearer, and at peace
			us.Explore(40, 25, range: 25);
			IUnit spy = g.CreateUnit(UnitType.Diplomat, 34, 25, g.PlayerNumber(us))!;
			SetTarget(ai, target);

			Assign(ai, spy);

			Assert.Equal(new Point(50, 25), spy.Goto);
		}
	}
}
