// CivOne tests
//
// The colonist grant: a stuck AI civ is handed one unsupported Settlers.
//
// From the post-mortem of a finished 492-turn game. The Aztecs founded exactly one city, on
// turn 0, on thirteen mountain tiles, and never founded another — five centuries on three
// units, no settler, 25 advances against the leaders' 83. Not losing a war: a size-3 city on
// mountains cannot spare the shields OR the population point a settler costs, so the civ that
// most needs to move is the one that can never afford to.
//
// AI.cs already has the rung below this — `lastChance`, which lets a civ with ZERO cities
// found one regardless of the usual gates, so it does not become a permanent zombie. This is
// the same idea one step earlier.
//
// The population-pump guard is the test that matters most: a settler joining a city adds a
// citizen, so a grant that fired while the civ already had one would hand out free population
// every turn.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ColonistGrantTests
	{
		// An AI civ with one city, no settler, and the clock past the grace period.
		private static (Game game, Player ai, byte num) AStuckCiv(uint turn = 150)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			byte num = g.PlayerNumber(ai);

			// City FIRST, then clear the units. Taking a civ down to no cities destroys it, and
			// before 0 AD a destroyed civ's slot is refilled by the buddy-respawn rule with a
			// NEW Player object and a fresh set of starting units — which handed this fixture a
			// phantom settler and left `ai` pointing at a player no longer in the game.
			ai.Explore(40, 25, range: 8);
			g.AddCity(ai, 0, 40, 25)!.Size = 3;
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == num).ToArray())
				g.DisbandUnit(u);
			SetTurn(g, turn);
			Sim.ClearTasks();
			return (g, ai, num);
		}

		// The turn is a private ushort and there is no production seam for setting it; reflection
		// here beats adding one to the engine for a test's convenience.
		private static void SetTurn(Game g, uint turn) =>
			typeof(Game).GetField("_gameTurn", System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Instance)!.SetValue(g, (ushort)turn);

		private static void Grant(Game g) =>
			typeof(Game).GetMethod("ProcessColonistGrants",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(g, null);

		private static int SettlersOf(Game g, byte num) =>
			g.GetUnits().Count(u => u.Owner == num && u is Settlers);

		[Fact]
		public void AStuckCivIsGivenAColonist()
		{
			(Game g, Player ai, byte num) = AStuckCiv();

			Grant(g);

			Assert.Equal(1, SettlersOf(g, num));
		}

		// Unsupported, as asked: a size-2 city cannot carry the upkeep of the thing meant to
		// rescue it, and the unit panel prints NONE for a unit with no home.
		//
		// A contract rather than a mutation test. Game.CreateUnit does not assign a home city
		// to anything it makes, so the SetHome(null) in the grant is belt-and-braces and
		// deleting it changes nothing today — this fires if that ever stops being true, or if
		// somebody decides the colonist should be supported after all. The upkeep assertion is
		// the half with teeth: a homed settler would cost the city a shield.
		[Fact]
		public void TheColonistIsUnsupported()
		{
			(Game g, Player ai, byte num) = AStuckCiv();
			City home = g.GetCities().First(c => c.Owner == num);
			int shieldsBefore = home.ShieldIncome;

			Grant(g);

			IUnit colonist = g.GetUnits().First(u => u.Owner == num && u is Settlers);
			Assert.Null(colonist.Home);
			home.InvalidateCache();
			Assert.Equal(shieldsBefore, home.ShieldIncome);
		}

		// The population pump: a settler joining a city adds a citizen, so a civ that already
		// has one must not be handed another.
		[Fact]
		public void NoSecondColonistWhileTheFirstLives()
		{
			(Game g, Player ai, byte num) = AStuckCiv();
			Grant(g);
			Assert.Equal(1, SettlersOf(g, num));

			SetTurn(g, 400);   // long past the cooldown
			Grant(g);

			Assert.Equal(1, SettlersOf(g, num));
		}

		// ...and once it is gone, the cooldown still holds for a while.
		[Fact]
		public void TheCooldownHoldsAfterTheColonistIsGone()
		{
			(Game g, Player ai, byte num) = AStuckCiv();
			Grant(g);
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == num && u is Settlers).ToArray())
				g.DisbandUnit(u);

			SetTurn(g, 170);   // 20 turns on, cooldown is 50
			Grant(g);
			Assert.Equal(0, SettlersOf(g, num));

			SetTurn(g, 210);
			Grant(g);
			Assert.Equal(1, SettlersOf(g, num));
		}

		// A civ doing fine is not a charity case.
		[Fact]
		public void AThrivingCivGetsNothing()
		{
			(Game g, Player ai, byte num) = AStuckCiv();
			ai.Explore(50, 25, range: 8);
			g.AddCity(ai, 1, 44, 25)!.Size = 4;
			g.AddCity(ai, 2, 36, 22)!.Size = 4;   // three cities — above the cap

			Grant(g);

			Assert.Equal(0, SettlersOf(g, num));
		}

		// Never the human: this is a floor under the AI, not a difficulty setting.
		[Fact]
		public void TheHumanIsNeverGrantedOne()
		{
			(Game g, Player ai, byte num) = AStuckCiv();
			Player human = g.HumanPlayer;
			byte hnum = g.PlayerNumber(human);
			human.Explore(60, 28, range: 8);
			g.AddCity(human, 3, 60, 28)!.Size = 3;
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == hnum).ToArray())
				g.DisbandUnit(u);

			Grant(g);

			Assert.Equal(0, SettlersOf(g, hnum));
		}

		// Not before the grace period — a slow start should be allowed to be a slow start.
		[Fact]
		public void NothingIsGrantedEarly()
		{
			(Game g, Player ai, byte num) = AStuckCiv(turn: 40);

			Grant(g);

			Assert.Equal(0, SettlersOf(g, num));
		}
	}
}
