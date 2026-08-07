// CivOne tests
//
// The AI's offensive apparatus targets CITIES — PickAttackTarget returns a City — and its
// only response to barbarians was defensive: a hostile within 3 tiles of a city earns a
// second defender. Nothing anywhere said "there is a thing standing in our fields, go kill
// it". So barbarian-owned megafauna were untouchable by anybody except a human player.
//
// Observed in the 1892 AD run: six Scavenger harvesters drank a world dry across 120 turns
// of extraction and not one AI civ moved on them — the departure event fired, which is only
// reachable when craft are still alive at the end of the clock. The same immunity had always
// covered the kaiju and the Henge Guardian.
//
// The rule these tests pin down: hunt what is near our cities AND what we can plausibly
// beat. Both halves matter. Without the first it is a crusade; without the second it is how
// an AI feeds its whole army to a wall one unit at a time.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class MonsterHuntTests
	{
		// An AI civ with one city in the middle of a walkable continent.
		private static (Game game, Player ai, City city) AWorldWithACity()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Explore(40, 25, range: 20);
			City city = g.AddCity(ai, 0, 40, 25)!;
			Sim.ClearTasks();
			return (g, ai, city);
		}

		private static IUnit Beast(Game g, UnitType type, int x, int y)
			=> g.CreateUnit(type, x, y, 0)!;   // owner 0 — the barbarian slot

		// A Cannon (attack 8) against a Harvester (defence 6), four tiles from our city.
		[Fact]
		public void AnAttackerHuntsAHarvesterStandingInOurFields()
		{
			var (g, ai, _) = AWorldWithACity();
			IUnit harvester = Beast(g, UnitType.Harvester, 44, 25);
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;

			Assert.Same(harvester, AI.Instance(ai).HuntQuarry(cannon));
		}

		// The half that stops the AI emptying its army into a wall: a Legion (attack 4) does
		// not walk out at a defence-6 harvester. Ancient civilizations cannot simply switch
		// the machinery off, which is the Harvester's whole design note.
		[Fact]
		public void AWeakAttackerLeavesTheHarvesterAlone()
		{
			var (g, ai, _) = AWorldWithACity();
			Beast(g, UnitType.Harvester, 44, 25);
			IUnit legion = g.CreateUnit(UnitType.Legion, 41, 25, g.PlayerNumber(ai))!;

			Assert.Null(AI.Instance(ai).HuntQuarry(legion));
		}

		// Gozira's defence is 24 and nothing in the game attacks that hard. It is a
		// catastrophe, not a boss fight, and no civ should march its stacks into it.
		[Fact]
		public void NobodyHuntsGozira()
		{
			var (g, ai, _) = AWorldWithACity();
			Beast(g, UnitType.Gozira, 44, 25);
			IUnit armor = g.CreateUnit(UnitType.Armor, 41, 25, g.PlayerNumber(ai))!;

			Assert.Null(AI.Instance(ai).HuntQuarry(armor));
		}

		// Our ground, not a crusade. Something standing on the far side of the world is
		// somebody else's harvest.
		[Fact]
		public void AMonsterFarFromOurCitiesIsNotOurProblem()
		{
			var (g, ai, _) = AWorldWithACity();
			Beast(g, UnitType.Harvester, 58, 34);   // >8 tiles from the city at (40,25)
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;

			Assert.Null(AI.Instance(ai).HuntQuarry(cannon));
		}

		// An ordinary barbarian raider is NOT megafauna: raids move on, and the existing
		// local defensive response is the right answer to them. Widening the hunt to every
		// loose hostile is a separate decision with its own knock-on effects on the war AI.
		[Fact]
		public void AnOrdinaryBarbarianRaiderIsNotHunted()
		{
			var (g, ai, _) = AWorldWithACity();
			Beast(g, UnitType.Legion, 44, 25);
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;

			Assert.Null(AI.Instance(ai).HuntQuarry(cannon));
		}

		// The hunt has to survive contact with the movement code: a LandAttack unit ordered
		// onto a barbarian-held tile must actually go. The blanket "foreign units on the next
		// tile" refusal applies only to Civilian and Settler roles, and the odds check backs
		// off only when Attack < Defense — which HuntQuarry has already excluded.
		[Fact]
		public void TheHunterIsOrderedOntoTheQuarry()
		{
			var (g, ai, _) = AWorldWithACity();
			IUnit harvester = Beast(g, UnitType.Harvester, 44, 25);
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 41, 25, g.PlayerNumber(ai))!;
			cannon.MovesLeft = cannon.Move;

			AI.Instance(ai).Move(cannon);

			Assert.Equal((harvester.X, harvester.Y), (cannon.Goto.X, cannon.Goto.Y));
		}
	}
}
