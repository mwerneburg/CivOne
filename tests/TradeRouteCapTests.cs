// CivOne tests
//
// The three-routes-per-city cap was Civ 1 counting bytes in 1991, and the churn it caused was
// perverse: an AI caravan arriving in your city silently evicted a route you had built, and
// three partner cities gave the entire world nine slots to compete for. The cap is gone.
//
// Removing it exposes something the cap had been hiding: AddTradeRoute never checked whether a
// route to that partner already existed. Under the cap a repeat caravan could stack at most
// three copies of the same route; uncapped it is an unbounded money printer. Routes are now
// unique per partner, so the first test here is the one that matters.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class TradeRouteCapTests
	{
		private static (Game game, Player player, City[] cities) AnEmpire(int cityCount)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player player = g.CurrentPlayer;
			player.Explore(40, 25, range: 20);

			var cities = new City[cityCount];
			for (int i = 0; i < cityCount; i++)
			{
				// Spread along a row, far enough apart that neither radius overlaps.
				cities[i] = g.AddCity(player, i, 20 + i * 5, 25);
				Assert.NotNull(cities[i]);
				cities[i].Size = 4;
			}
			Sim.ClearTasks();
			return (g, player, cities);
		}

		// The money printer, stated directly: the same partner twice must not pay twice.
		[Fact]
		public void ARepeatCaravanRefreshesRatherThanStacks()
		{
			(Game g, Player player, City[] cities) = AnEmpire(2);
			City home = cities[0], partner = cities[1];

			home.AddTradeRoute(partner, "Silk");
			int oneRoute = home.TradeRouteCount;
			int oneValue = home.TradeTotal;

			for (int i = 0; i < 5; i++) home.AddTradeRoute(partner, "Wine");

			Assert.Equal(oneRoute, home.TradeRouteCount);
			Assert.Equal(oneValue, home.TradeTotal);
		}

		// ...and past the old cap, a fourth distinct partner no longer evicts the first.
		[Fact]
		public void MoreThanThreeRoutesAreKept()
		{
			(Game g, Player player, City[] cities) = AnEmpire(6);
			City home = cities[0];

			for (int i = 1; i < 6; i++) home.AddTradeRoute(cities[i], "Salt");

			Assert.Equal(5, home.TradeRouteCount);
			Assert.All(cities.Skip(1), c => Assert.Contains(home.TradeRoutes, r => r.Partner == c));
		}

		// A route that has stopped paying is dropped, and both ends go together — the pair is
		// worth different amounts from each side, so a one-sided prune leaves a phantom.
		[Fact]
		public void AWorthlessRouteIsDroppedFromBothEnds()
		{
			(Game g, Player player, City[] cities) = AnEmpire(2);
			City home = cities[0], partner = cities[1];
			home.AddTradeRoute(partner, "Silk");
			partner.AddTradeRoute(home, "Silk");
			Assert.Equal(1, home.TradeRouteCount);

			// A city with no citizens produces no trade, so the route to it is worth nothing.
			partner.Size = 0;
			partner.InvalidateCache();
			home.InvalidateCache();
			home.PruneWorthlessRoutes();

			Assert.Equal(0, home.TradeRouteCount);
			Assert.Equal(0, partner.TradeRouteCount);
		}

		// A route still paying is left alone — the prune must not be a general cull.
		//
		// The partner is FOREIGN and far away, which is the only kind of route worth anything:
		// RouteBonus halves for a shared continent and halves again for a shared owner, so a
		// short domestic route between two ordinary cities computes to 0.31 and floors to
		// zero. That is not new — such routes always paid nothing — but they used to sit in a
		// slot forever, and now they are swept the following turn.
		[Fact]
		public void APayingRouteSurvivesThePrune()
		{
			(Game g, Player player, City[] cities) = AnEmpire(1);
			City home = cities[0];

			// Rivers, because grassland pays no trade at all and a partner with zero trade
			// makes a zero-value route however far away it is.
			Player other = g.Players.First(p => p != player && g.PlayerNumber(p) != 0);
			int fx = home.X + 35, fy = home.Y;
			for (int y = fy - 2; y <= fy + 2; y++)
			for (int x = fx - 2; x <= fx + 2; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();
			other.Explore(fx, fy, range: 5);

			City foreign = g.AddCity(other, 7, fx, fy);
			Assert.NotNull(foreign);
			foreign.Size = 6;
			foreign.InvalidateCache();

			home.AddTradeRoute(foreign, "Silk");
			Assert.True(home.TradeRoutes.First().Value > 0,
				$"fixture built a worthless route: value=0, partner trade={foreign.TradeTotal}");

			home.PruneWorthlessRoutes();

			Assert.Equal(1, home.TradeRouteCount);
		}
	}
}
