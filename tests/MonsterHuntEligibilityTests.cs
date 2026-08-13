// CivOne tests
//
// Who is allowed to hunt a harvester.
//
// The hunt was gated on Role == UnitRole.LandAttack, which is a job title rather than a
// capability, and a developed peaceful AI builds no attackers at all. Measured in the 1804 AD
// Scavenger game: 46 harvesters draining the world, 19 of them parked within 8 tiles of
// somebody's city, and every AI civ reporting zero eligible hunters. The Lakota held 8 MechInf
// — attack 6, exactly enough for a defence-6 harvester — with ten cities inside the hunt
// radius of one, and none was ever considered, because MechInf is filed Role.Defense. The
// Haida's entire 61-unit army (Riflemen, Militia, Musketeers) topped out at attack 3.
//
// So eligibility is now capability. The attack >= defence test always refused anything that
// could not win, so the role test only ever removed units that could.
//
// The cost of widening: garrisons became eligible, so the sole defender of a city must be
// held back explicitly — losing the city to the next raider is a worse trade than the tiles.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class MonsterHuntEligibilityTests
	{
		// A civ with one city and a harvester standing three tiles away.
		private static (Game g, Player p, IUnit harvester) AHarvesterAtTheGates()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			p.Explore(40, 25, range: 10);
			g.AddCity(p, 0, 40, 25)!.Size = 4;

			IUnit harvester = g.CreateUnit(UnitType.Harvester, 43, 25, 0)!;
			Sim.ClearTasks();
			return (g, p, harvester);
		}

		// The Lakota case: a defence-role unit that can win is a hunter.
		[Fact]
		public void ADefenderThatCanWinIsOfferedTheQuarry()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			// Out in the field, not garrisoning: the sole-garrison rule is its own test below.
			IUnit mech = g.CreateUnit(UnitType.MechInf, 41, 25, g.PlayerNumber(p))!;
			Assert.Equal(UnitRole.Defense, mech.Role);
			Assert.True(mech.Attack >= harvester.Defense, "fixture: MechInf should out-attack a harvester");

			Assert.Same(harvester, AI.Instance(p).HuntQuarry(mech));
		}

		// ...and one that cannot win is still refused. Feeding Musketeers to a defence-6
		// harvester one at a time is how an AI empties its army into a wall.
		[Fact]
		public void ADefenderThatCannotWinIsStillRefused()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			IUnit musketeers = g.CreateUnit(UnitType.Musketeers, 41, 25, g.PlayerNumber(p))!;
			Assert.True(musketeers.Attack < harvester.Defense, "fixture: Musketeers should lose");

			Assert.Null(AI.Instance(p).HuntQuarry(musketeers));
		}

		// The garrison guard. This unit could win, but it is the only thing holding the city.
		[Fact]
		public void TheSoleGarrisonIsNotSentOut()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			IUnit garrison = g.CreateUnit(UnitType.MechInf, 40, 25, g.PlayerNumber(p))!;

			Assert.Null(AI.Instance(p).HuntQuarry(garrison));
		}

		// ...but a city with two defenders can spare one.
		[Fact]
		public void ACityWithASpareDefenderMaySendOne()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			IUnit garrison = g.CreateUnit(UnitType.MechInf, 40, 25, g.PlayerNumber(p))!;
			g.CreateUnit(UnitType.Musketeers, 40, 25, g.PlayerNumber(p));

			Assert.Same(harvester, AI.Instance(p).HuntQuarry(garrison));
		}

		// A fortified unit is never offered to AI.Move at all, so the wake-up has to reach
		// defenders too or the widening changes nothing in play.
		[Fact]
		public void WakeHuntersWakesACapableDefender()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			IUnit mech = g.CreateUnit(UnitType.MechInf, 41, 25, g.PlayerNumber(p))!;
			mech.Fortify = true;
			Assert.True(mech.Fortify, "fixture: should start fortified");

			AI.Instance(p).WakeHunters();

			Assert.False(mech.Fortify, "a capable defender slept through the harvest");
		}

		// HuntQuarry only says a unit COULD hunt; AssignMission is what actually sends it, and
		// that call sat inside the `if (unit.Role == UnitRole.LandAttack)` branch. A defender
		// vetted by every test above would still never be given the order. Driven by
		// reflection because AssignMission is private — the alternative is widening its
		// accessibility for a test, which is worse.
		[Fact]
		public void AssignMissionActuallySendsACapableDefender()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			IUnit mech = g.CreateUnit(UnitType.MechInf, 41, 25, g.PlayerNumber(p))!;
			AI ai = AI.Instance(p);

			typeof(AI).GetMethod("AssignMission",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(ai, new object[] { mech });

			Assert.False(mech.Goto.IsEmpty, "the defender was vetted as a hunter but never ordered out");
			Assert.Equal(harvester.X, mech.Goto.X);
			Assert.Equal(harvester.Y, mech.Goto.Y);
		}

		// The civilian exemption survives the widening: a Settlers or Caravan has attack 0 and
		// must never be walked into a harvester.
		[Fact]
		public void CiviliansAreNeverHunters()
		{
			(Game g, Player p, IUnit harvester) = AHarvesterAtTheGates();
			IUnit settlers = g.CreateUnit(UnitType.Settlers, 41, 25, g.PlayerNumber(p))!;

			Assert.Null(AI.Instance(p).HuntQuarry(settlers));
		}
	}
}
