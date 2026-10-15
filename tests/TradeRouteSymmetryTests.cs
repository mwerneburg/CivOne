// CivOne tests
//
// A trade route paid each city out of the OTHER city's economy.
//
// RouteBonus multiplied the partner's BaseTrade alone. Distance and both halvings (same
// continent, same civ) are symmetric, so that one term was the entire difference between the
// two ends of a route: the poorer partner collected a slice of the richer one's economy and
// gave back a slice of its own. Caravans from a large empire to a small one were foreign aid.
//
// Measured in a 1894 AD game, on the human's 109 foreign routes: 3,073 trade to the human and
// 5,494 to the rivals. St. Petersburg (size 18) to Belle Fourche (size 3) paid 9 one way and
// 171 the other. The Lakota, whose own tiles produced 259 trade across 38 cities, were running
// an economy of 3,282 almost entirely on routes with the player trying to out-earn them.
//
// Civ 1's rule is (distance + 10) x (trade of BOTH cities) / 24, so both ends read the same
// sum and are worth the same. The two old figures added up to the new one.
//
// Oct 2026: the sum became twice the GEOMETRIC MEAN. Still symmetric, and equal cities pay
// exactly what they did, but the sum let a village earn almost entirely from a metropolis:
// the Aztecs' 7 cities made 101 trade of their own and 1,330 from routes.

using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class TradeRouteSymmetryTests
	{
		// A rich city and a poor one, far apart and on different continents so neither halving
		// applies — the arrangement the report was about.
		private static (Game game, City rich, City poor) RichAndPoor(bool sameOwner = false)
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 20; y <= 28; y++)
			{
				// River, not grassland: trade comes from rivers, roads and specials, and a
				// grassland metropolis earns almost none — the first fixture had 5 trade against
				// 1, too narrow a gap to tell a sum from a substitution.
				for (int x = 8; x <= 16; x++)  Map.Instance.ChangeTileType(x, y, Terrain.River);
				for (int x = 60; x <= 68; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player a = g.HumanPlayer;
			Player b = sameOwner ? a : g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != a);
			a.Explore(12, 24, range: 40);
			b.Explore(64, 24, range: 40);

			City rich = g.AddCity(a, 0, 12, 24)!;
			City poor = g.AddCity(b, 1, 64, 24)!;
			rich.Size = 24;
			poor.Size = 2;
			rich.InvalidateCache();
			poor.InvalidateCache();
			Sim.ClearTasks();
			return (g, rich, poor);
		}

		private static int Bonus(City from, City to) =>
			(int)typeof(City).GetMethod("RouteBonus", BindingFlags.NonPublic | BindingFlags.Instance)!
				.Invoke(from, new object[] { to })!;

		private static int BaseTrade(City c) =>
			(int)typeof(City).GetProperty("BaseTrade", BindingFlags.NonPublic | BindingFlags.Instance)!
				.GetValue(c)!;

		// The fixture has to actually contain the inequality the rule is about, or every
		// assertion below is satisfied by two identical cities.
		[Fact]
		public void TheRichCityIsActuallyRicher()
		{
			(Game g, City rich, City poor) = RichAndPoor();

			Assert.True(BaseTrade(rich) > BaseTrade(poor) + 4,
				$"rich={BaseTrade(rich)} poor={BaseTrade(poor)}: no gap to measure");
		}

		// The report, stated directly: the same pair of cities is worth the same to each of
		// them.
		[Fact]
		public void BothEndsOfARouteAreWorthTheSame()
		{
			(Game g, City rich, City poor) = RichAndPoor();
			rich.AddTradeRoute(poor, "Silk");
			poor.AddTradeRoute(rich, "Silk");

			Assert.Equal(Bonus(poor, rich), Bonus(rich, poor));
		}

		// Twice the geometric mean of the two economies, each floored at 1.
		private static int Pair(City a, City b, int distance, float multiplier = 1f) =>
			(int)(multiplier * (distance + 10) * 2 * System.Math.Sqrt((double)System.Math.Max(1, BaseTrade(a)) * System.Math.Max(1, BaseTrade(b))) / 24);

		[Fact]
		public void TheRouteIsWorthTheGeometricMeanOfBothEconomies()
		{
			(Game g, City rich, City poor) = RichAndPoor();
			int distance = Common.TradeDistance(rich.X, rich.Y, poor.X, poor.Y);
			int expected = Pair(rich, poor, distance);

			Assert.Equal(expected, Bonus(rich, poor));
		}

		// The old rule, named so a regression reads as itself: the rich city must no longer be
		// paid the poor city's trade alone.
		[Fact]
		public void TheRichEndIsNoLongerPaidOnlyThePartnersTrade()
		{
			(Game g, City rich, City poor) = RichAndPoor();
			int distance = Common.DistanceToTile(rich.X, rich.Y, poor.X, poor.Y);
			int partnerOnly = (int)((float)(distance + 10) * BaseTrade(poor) / 24);

			Assert.True(Bonus(rich, poor) > partnerOnly,
				$"the rich end is still earning the partner's trade alone ({partnerOnly})");
		}

		// The point of the mean: the poor end no longer earns the sum of both economies.
		[Fact]
		public void ThePoorEndNoLongerLivesOnItsPartner()
		{
			(Game g, City rich, City poor) = RichAndPoor();
			int distance = Common.TradeDistance(rich.X, rich.Y, poor.X, poor.Y);
			int oldSum = (int)((float)(distance + 10) * (BaseTrade(rich) + BaseTrade(poor)) / 24);

			Assert.True(Bonus(poor, rich) < oldSum * 3 / 4,
				$"the poor end still earns {Bonus(poor, rich)} of the old sum's {oldSum}");
		}

		// Both halvings survive, and they are what keeps a domestic route from being the best
		// trade in the game now that both ends are paid the full sum.
		[Fact]
		public void ADomesticRouteIsStillHalved()
		{
			(Game g, City rich, City poor) = RichAndPoor(sameOwner: true);

			int distance = Common.TradeDistance(rich.X, rich.Y, poor.X, poor.Y);

			Assert.Equal(Pair(rich, poor, distance, 0.5f), Bonus(rich, poor));
		}

		// War still pays nothing, from either side.
		[Fact]
		public void AWarStillClosesTheRoute()
		{
			(Game g, City rich, City poor) = RichAndPoor();
			g.GetPlayer(rich.Owner).DeclareWar(g.GetPlayer(poor.Owner));
			Sim.ClearTasks();

			Assert.Equal(0, Bonus(rich, poor));
			Assert.Equal(0, Bonus(poor, rich));
		}
	}
}
