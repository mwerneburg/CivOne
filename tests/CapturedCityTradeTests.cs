// CivOne tests
//
// Reported from a Guarani game aimed at Pax Mercatoria: the Aztecs flipped a city, the player
// pushed them back and took the Aztec city he had been trading with — and the trade never came
// back, not even after he handed the city over again.
//
// City.Owner's setter destroyed every route at both ends on ANY change of hands. Winning the
// fight cost the player the commerce he was fighting for, and giving the city back could not
// undo it: only a fresh caravan per route could, at 50 shields and a long walk each, with
// Pax Mercatoria counting only the best five EXTERNAL routes per city.
//
// War was given the opposite treatment (WarTradeSuspensionTests) and it is the right one:
// nothing is deleted, RouteBonus simply pays 0 while the owners fight, and peace restores it.
// The clearing line predates that decision — it dates from the original April implementation
// (d4731d15) and was never revisited. Ownership is a suspension now, not an amputation.
//
// Razing is the case that really does end a route, and Game.DestroyCity now says so directly
// instead of inheriting the cleanup from Owner = 0.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CapturedCityTradeTests
	{
		// Two civilizations, a city each on its own continent, and a route between them. The
		// continents are separate so the same-continent halving never enters the arithmetic —
		// the only multiplier that moves in these tests is the internal/external one.
		private static (Game game, Player us, Player them, City ours, City theirs) TwoTraders()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 18; y <= 30; y++)
			{
				for (int x = 8; x <= 18; x++)  Map.Instance.ChangeTileType(x, y, Terrain.River);
				for (int x = 58; x <= 68; x++) Map.Instance.ChangeTileType(x, y, Terrain.River);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != us);
			us.Explore(12, 24, range: 40);
			them.Explore(64, 24, range: 40);

			City ours = g.AddCity(us, 0, 12, 24)!;
			City theirs = g.AddCity(them, 1, 64, 24)!;
			ours.Size = 16;
			theirs.Size = 16;
			ours.InvalidateCache();
			theirs.InvalidateCache();
			ours.AddTradeRoute(theirs, "Silk");
			theirs.AddTradeRoute(ours, "Silk");
			Sim.ClearTasks();
			return (g, us, them, ours, theirs);
		}

		// The fixture must pay something, or every assertion below is vacuous.
		[Fact]
		public void TheRouteIsWorthSomethingToBeginWith()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();

			Assert.Equal(1, ours.TradeRouteCount);
			Assert.True(ours.TradeRoutes.Single().Value > 0, "the fixture route pays nothing");
			Assert.True(ours.ScoringTrade > ours.TradeRoutes.Single().Value,
				"the fixture city has no trade of its own to add the route to");
		}

		// The report, stated directly: taking the city no longer deletes the trade.
		[Fact]
		public void CapturingTheCityDoesNotDeleteTheRoute()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();

			theirs.Owner = g.PlayerNumber(us);

			Assert.Equal(1, ours.TradeRouteCount);
			Assert.Same(theirs, ours.TradeRoutes.Single().Partner);
			Assert.Equal(1, theirs.TradeRouteCount);
		}

		// Holding both ends is not the same as trading with somebody. The route keeps paying,
		// at the internal half rate, and stops counting toward Pax Mercatoria entirely.
		[Fact]
		public void HoldingBothEndsMakesTheRouteInternal()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			int external = ours.TradeRoutes.Single().Value;
			int scoredBefore = ours.ScoringTrade;

			theirs.Owner = g.PlayerNumber(us);
			ours.InvalidateCache();

			int internalValue = ours.TradeRoutes.Single().Value;
			Assert.True(internalValue > 0, "an internal route still pays");
			Assert.True(internalValue < external, "an internal route pays less than a foreign one");
			// Down by exactly the route: an internal route scores nothing at all, rather than
			// scoring its reduced value.
			Assert.Equal(scoredBefore - external, ours.ScoringTrade);
		}

		// The half the player actually asked for: give the city back and the commerce returns
		// on its own, with no caravan sent.
		[Fact]
		public void HandingTheCityBackRestoresTheTrade()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			int before = ours.TradeRoutes.Single().Value;
			int scoredBefore = ours.ScoringTrade;

			theirs.Owner = g.PlayerNumber(us);
			ours.InvalidateCache();
			theirs.Owner = g.PlayerNumber(them);
			ours.InvalidateCache();

			Assert.Equal(before, ours.TradeRoutes.Single().Value);
			Assert.Equal(scoredBefore, ours.ScoringTrade);
		}

		// Razing is the amputation, and it must stay one: there is nothing left to trade with.
		// This cleanup used to come free with Owner = 0 inside DestroyCity.
		[Fact]
		public void RazingTheCityStillEndsTheRoute()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();

			theirs.Size = 0;

			Assert.Equal(0, ours.TradeRouteCount);
		}

		// A captured city's routes to its FORMER owner are suspended by the war, not inherited
		// as income — the existing war rule doing its job on a route that now changes sides.
		[Fact]
		public void RoutesBackToTheFormerOwnerPayNothingWhileTheWarLasts()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			City second = g.AddCity(them, 2, 66, 26)!;
			second.Size = 12;
			theirs.AddTradeRoute(second, "Wine");
			second.AddTradeRoute(theirs, "Wine");
			us.DeclareWar(them);

			theirs.Owner = g.PlayerNumber(us);
			theirs.InvalidateCache();

			Assert.Equal(0, theirs.TradeRoutes.Single(r => r.Partner == second).Value);
		}
	}
}
