// CivOne tests
//
// The Greys — The Portal's cursed outcome (Game.OpenPortal, Game.ProcessGreys).
//
// They had no behavioural coverage at all, and two promises were made to the player in
// September 2026 that nothing enforced: the Civilopedia page now tells you a turn of
// negative food evicts them, and the city screen now names them as the reason a city's
// corruption is high. Both are written down where a player will act on them, so both need
// to stay true.
//
// Written after a real game at 1834 AD where the answer to "which city are they in?" had
// to be read out of the save file, because nothing on screen said.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class GreysTests
	{
		private static (Game game, Player us, City city) AnInfestableCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 40; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				// Roads are what put trade on grassland, and the skim is RawTrade/5 — INTEGER
				// division. Bare grassland gave the fixture a raw trade of 1 (SizeTradeBonus
				// alone at size 6), so the skim was 1/5 = 0 and the test passed with the skim
				// deleted. Roads plus a size-12 city put it comfortably above the rounding.
				Map.Instance[x, y].Road = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			us.Government = new Monarchy();
			us.Explore(30, 25, range: 20);

			City c = g.AddCity(us, 0, 30, 25)!;
			Assert.NotNull(c);
			c.Size = 12;
			Sim.ClearTasks();
			return (g, us, c);
		}

		// One full ROUND, not one call to Sim.RunTurns.
		//
		// ProcessGreys lives in EndTurn's phase B, which runs only when _currentPlayer wraps
		// (Game.cs:1819), so the clock has to come all the way round for it to fire at all.
		// RunTurns could not get there: the hand-made Settlers below sit in the city waiting
		// for orders the harness has no way to give, so it burned its budget and returned
		// with GameTurn still 0 — and a test that asserts "they are gone" after a turn that
		// never happened would have been green for the wrong reason.
		//
		// Driving EndTurn directly is deterministic and takes 8 calls here. The assertion
		// that the turn actually moved is the point: without it this helper can go back to
		// proving nothing the moment the phase structure changes.
		private static void AdvanceOneRound(Game g)
		{
			uint turn = g.GameTurn;
			for (int i = 0; i < 64 && g.GameTurn == turn; i++) g.EndTurn();
			Assert.True(g.GameTurn > turn, "the round never wrapped, so phase B never ran");
		}

		// What the CORRUPTION field on the city screen now claims: the Greys are in THIS
		// number, to the tune of a fifth of the city's raw trade. If the skim ever moves
		// somewhere else — a separate penalty, a trade multiplier — the screen is lying about
		// where the cost went, and pointing the player at the wrong lever.
		[Fact]
		public void TheGreysSkimAFifthOfTheCitysTradeIntoCorruption()
		{
			(Game g, _, City c) = AnInfestableCity();

			int clean = c.Corruption;
			int rawTrade = c.RawTradeForAi;
			// Above the rounding, not merely above zero: RawTrade/5 is integer division, so a
			// fixture with 4 trade skims nothing and proves nothing.
			Assert.True(rawTrade >= 10, $"fixture raw trade is {rawTrade}; the skim would round to {rawTrade / 5}");

			g.GreyCities.Add((c.X, c.Y));
			Game.InvalidateCitiesAt(c.X, c.Y);

			Assert.Equal(clean + rawTrade / 5, c.Corruption);
		}

		// ...and a city they are NOT in must not pay it. Without this the test above passes
		// against an implementation that charges every city in the empire.
		[Fact]
		public void ACityTheyAreNotInPaysNothing()
		{
			(Game g, Player us, City c) = AnInfestableCity();

			City other = g.AddCity(us, 1, 36, 25)!;
			Assert.NotNull(other);
			other.Size = 12;
			int cleanOther = other.Corruption;

			g.GreyCities.Add((c.X, c.Y));
			Game.InvalidateCitiesAt(c.X, c.Y);
			Game.InvalidateCitiesAt(other.X, other.Y);

			Assert.Equal(cleanOther, other.Corruption);
		}

		// The counterplay the Civilopedia page now promises in as many words: one turn of
		// negative food income and they go. This is the only way a player can be rid of them,
		// so it is the one rule on that page that must not rot.
		[Fact]
		public void OneTurnOfNegativeFoodEvictsThem()
		{
			(Game g, Player us, City c) = AnInfestableCity();

			g.GreyCities.Add((c.X, c.Y));
			Assert.Contains((c.X, c.Y), g.GreyCities);

			// Starve it by supporting settlers out of it: FoodCosts adds SettlerFoodCost per
			// homed Settlers unit (City.cs:319), which is 2 under Monarchy. Cheaper and more
			// deterministic than rearranging worked tiles, where the governor may put them
			// back before the turn is processed.
			byte num = g.PlayerNumber(us);
			while (c.FoodIncome >= 0)
			{
				CivOne.Units.IUnit s = g.CreateUnit(UnitType.Settlers, c.X, c.Y, num)!;
				Assert.NotNull(s);
				s.SetHome(c);
				Game.InvalidateCitiesAt(c.X, c.Y);
			}
			Assert.True(c.FoodIncome < 0, "the fixture never reached a food deficit");

			AdvanceOneRound(g);

			Assert.DoesNotContain((c.X, c.Y), g.GreyCities);
		}

		// A city in the black keeps them — otherwise the test above proves only that a turn
		// passed, which is exactly how this suite has been fooled before.
		[Fact]
		public void AWellFedCityKeepsThem()
		{
			(Game g, _, City c) = AnInfestableCity();

			g.GreyCities.Add((c.X, c.Y));
			Assert.True(c.FoodIncome >= 0, "the fixture city is already starving");

			AdvanceOneRound(g);

			Assert.Contains((c.X, c.Y), g.GreyCities);
		}

		// The city screen needs a live render, so this one is pinned at the source. It is the
		// only place in the game that says WHICH city they are in — the two advisor messages
		// fire once each and then the information is gone.
		[Fact]
		public void TheCityScreenNamesThemOnTheCorruptionField()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "Screens", "CityManager.cs"));

			Assert.Contains("GreyCities.Contains((_city.X, _city.Y))", src);
			Assert.Contains("GREYS", src);
		}
	}
}
