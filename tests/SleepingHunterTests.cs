// CivOne tests
//
// The hunt (MonsterHuntTests) was correct and did nothing, because the units it was written
// for were never asked. Game.cs's unit-selection loop skips anything with Sentry or Fortify
// set, so a fortified unit never reaches AI.Move, never reaches AssignMission, and never
// reaches HuntQuarry — and the AI fortifies attackers routinely, on a failed advance and
// whenever an idle one is parked in an under-defended city.
//
// Measured in the 1872 AD run: 337 Armor in the world (attack 10 against a defence-6
// harvester, every one eligible), 39% of the harvest inside the hunt radius of a city, and
// not one engagement. A sleeping unit cannot decide anything.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SleepingHunterTests
	{
		private static (Game game, Player ai) AWorldWithACity()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Explore(40, 25, range: 20);
			g.AddCity(ai, 0, 40, 25);
			Sim.ClearTasks();
			return (g, ai);
		}

		// The defect, stated directly.
		[Fact]
		public void AFortifiedAttackerIsWokenWhenThereIsSomethingToHunt()
		{
			var (g, ai) = AWorldWithACity();
			g.CreateUnit(UnitType.Harvester, 44, 25, 0);
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;
			cannon.Fortify = true;

			AI.Instance(ai).WakeHunters();

			Assert.False(cannon.Fortify, "a fortified attacker is never offered to AI.Move at all");
		}

		// A stale target is the second gate: AssignMission runs only when Goto is empty, so a
		// woken unit still holding an old destination would walk off to it instead of hunting.
		[Fact]
		public void WakingAlsoDropsTheStaleTarget()
		{
			var (g, ai) = AWorldWithACity();
			g.CreateUnit(UnitType.Harvester, 44, 25, 0);
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;
			cannon.Fortify = true;
			cannon.Goto = new System.Drawing.Point(25, 18);

			AI.Instance(ai).WakeHunters();

			Assert.True(cannon.Goto.IsEmpty);
		}

		// Narrow on purpose. A garrison that cannot beat the thing must stay asleep, or waking
		// becomes a way to feed the army to a wall — and defenders must never be woken at all,
		// because the city is their job.
		[Fact]
		public void AGarrisonThatCannotWinStaysAsleep()
		{
			var (g, ai) = AWorldWithACity();
			g.CreateUnit(UnitType.Harvester, 44, 25, 0);
			IUnit legion = g.CreateUnit(UnitType.Legion, 41, 25, g.PlayerNumber(ai))!;      // attack 4 vs defence 6
			IUnit riflemen = g.CreateUnit(UnitType.Riflemen, 41, 25, g.PlayerNumber(ai))!;  // Defense role
			legion.Fortify = true;
			riflemen.Fortify = true;

			AI.Instance(ai).WakeHunters();

			Assert.True(legion.Fortify, "an attacker that would lose stays put");
			Assert.True(riflemen.Fortify, "defenders are not hunters");
		}

		// Nothing to hunt, nothing to wake: the ordinary state of the game for hundreds of
		// turns, and it must not disturb a garrison.
		[Fact]
		public void AnEmptyWorldWakesNobody()
		{
			var (g, ai) = AWorldWithACity();
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;
			cannon.Fortify = true;

			AI.Instance(ai).WakeHunters();

			Assert.True(cannon.Fortify);
		}

		// Distance still applies after waking — the wake-up reuses HuntQuarry rather than
		// inventing a second, looser rule about what is worth getting up for.
		[Fact]
		public void AMonsterOnTheFarSideOfTheWorldDoesNotWakeAnyone()
		{
			var (g, ai) = AWorldWithACity();
			g.CreateUnit(UnitType.Harvester, 58, 34, 0);   // >8 tiles from the city at (40,25)
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;
			cannon.Fortify = true;

			AI.Instance(ai).WakeHunters();

			Assert.True(cannon.Fortify);
		}
	}
}
