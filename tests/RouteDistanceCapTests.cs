// CivOne tests
//
// A trade route pays for at most City.RouteDistanceCap (40) tiles of distance: Civ 1's
// world was 80 wide, and on a 320x200 map Dirigible routes ~80 long paid four times what
// the formula was tuned for (measured Sep 2026: 46.0% of world output from airship trade
// alone; 34.8% capped).

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class RouteDistanceCapTests
	{
		// Our city at x=20 and a foreign partner `distance` tiles east, on one long strip of
		// land (one continent, so both route values carry the same 0.5).
		private static (City home, City partner) ARouteOf(int distance)
		{
			Sim.NewGame(width: 160, height: 50);
			Game g = Game.Instance;
			for (int x = 10; x <= 120; x++)
			for (int y = 23; y <= 27; y++)
				Map.Instance.ChangeTileType(x, y, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != us);
			// A city works only tiles its owner has seen (ScoringTradeTests found the same).
			us.Explore(20, 25, range: 6);
			them.Explore(20 + distance, 25, range: 6);
			City home = g.AddCity(us, 0, 20, 25)!;
			City partner = g.AddCity(them, 1, 20 + distance, 25)!;
			home.Size = 6; partner.Size = 6;
			home.AddTradeRoute(partner, "Silk");
			Assert.True(home.BaseTrade + partner.BaseTrade > 0, "fixture: no trade, so every route is worth 0");
			return (home, partner);
		}

		// The formula at a given distance, same continent, different owners.
		private static int Expected(City home, City partner, int distance) =>
			(int)(0.5f * (distance + 10) * (home.BaseTrade + partner.BaseTrade) / 24);

		[Fact]
		public void ALongRoutePaysForFortyTilesAndNoMore()
		{
			(City home, City partner) = ARouteOf(70);

			Assert.Equal(Expected(home, partner, City.RouteDistanceCap), home.TradeRoutes.Single().Value);
		}

		// Control: a route inside the old world's reach pays exactly what it always did.
		[Fact]
		public void AShortRouteIsUnchanged()
		{
			(City home, City partner) = ARouteOf(25);

			Assert.Equal(Expected(home, partner, 25), home.TradeRoutes.Single().Value);
		}
	}
}
