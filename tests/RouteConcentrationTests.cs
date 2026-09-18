// CivOne tests
//
// Diminishing returns on repeated routes to the SAME partner city.
//
// Reported from a finished game (CIVIL1, 1855 AD): 262 external trade routes anchored on
// eleven partner cities, five of them carrying 93% of the value — 51 caravans into Wuhan, 47
// into Sparta. RouteBonus pays mostly for distance, so one far-off city absorbed the whole
// empire's caravan output at ~120 gold a route for ever: a treasury of 32,788 a turn against
// 839 of base trade, tax permanently at 0%, and the Dome sequence bought in five turns.
//
// Routes were already unique per (home, partner) pair, so nobody could stack two routes on
// one pair — but nothing stopped sixty of your OWN cities each anchoring on the same foreign
// city. The nth route a civilization holds to one partner now pays value/n.
//
// A curve rather than a cap, so the ceiling moves behind diversification instead of dropping:
// the same caravans spread over distinct partners still pay full value, and no delivery is
// ever worth literally nothing. Scoped per (civilization, partner) so one civ's caravans can
// never evict another's.
//
// The fixture is deliberately SYMMETRIC — equidistant home cities on identical terrain — so
// every route to a given partner is worth exactly the same. That is what makes the harmonic
// sum exact rather than approximate, and it is the only arrangement that exercises the
// rank tiebreak: without a total ordering, two equal routes both take rank 1 and the pair
// pays double what the curve intends. An earlier asymmetric fixture left that line dead and
// let a test pass with the curve switched off.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class RouteConcentrationTests
	{
		// Four home cities of ours in a column on one continent, two foreign cities on another
		// continent far to the east. DistanceToTile is Chebyshev — max(dx, dy) — and dx is 54
		// for every pair here while dy never approaches it, so EVERY route below is distance
		// 54. City radii are spaced six apart so no two compete for a tile, which keeps their
		// BaseTrade identical too.
		private static (Game g, City[] mine, City[] theirs) TwoContinents()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 12; y <= 44; y++)
			{
				for (int x = 8; x <= 14; x++)  Map.Instance.ChangeTileType(x, y, Terrain.River);
				for (int x = 62; x <= 68; x++) Map.Instance.ChangeTileType(x, y, Terrain.River);
			}
			// Specials are placed by position and survive ChangeTileType, so two cities on
			// otherwise identical ground read different BaseTrade — measured at 88 against 85,
			// which is enough to break every equality below. Cleared so the symmetry is real.
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				((Tiles.BaseTile)Map.Instance[x, y]).Special = false;
			Map.Instance.RecalculateContinentsIfDirty();

			Player me = g.HumanPlayer;
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != me);
			me.Explore(11, 28, range: 60);
			them.Explore(65, 28, range: 60);

			// The capital FIRST, in the middle of the column. Corruption scales with distance
			// from the palace, so BaseTrade is not a property of the terrain alone — four
			// cities on identical ground measured 28, 26, 17 and 10. Home cities are therefore
			// placed in pairs equidistant from the capital, which is what makes mine[0] and
			// mine[1] genuinely interchangeable.
			City capital = g.AddCity(me, 9, 11, 28)!;
			var mine = new City[4];
			mine[0] = g.AddCity(me, 0, 11, 22)!;   // 6 from the capital
			mine[1] = g.AddCity(me, 1, 11, 34)!;   // 6 the other way
			mine[2] = g.AddCity(me, 2, 11, 16)!;   // 12
			mine[3] = g.AddCity(me, 3, 11, 40)!;   // 12
			var theirs = new City[2];
			theirs[0] = g.AddCity(them, 4, 65, 28)!;   // distance 26 from every one of ours
			theirs[1] = g.AddCity(them, 5, 65, 34)!;

			foreach (City c in mine.Concat(theirs).Concat(new[] { capital }))
			{ c.Size = 12; c.ResetResourceTiles(); }
			Sim.ClearTasks();
			return (g, mine, theirs);
		}

		// What one route is worth on its own, with nothing else anchored anywhere.
		private static int ValueAlone(City home, City partner)
		{
			home.AddTradeRoute(partner, "Gold");
			home.InvalidateCache();
			int v = home.TradeRouteBonus;
			home.RemoveTradeRoutesTo(partner);
			home.InvalidateCache();
			return v;
		}

		private static int Anchor(City[] homes, int count, City partner)
		{
			for (int i = 0; i < count; i++) homes[i].AddTradeRoute(partner, "Gold");
			foreach (City c in homes) c.InvalidateCache();
			return homes.Take(count).Sum(c => c.TradeRouteBonus);
		}

		// The report, stated directly: a second city piling onto the same partner earns half.
		//
		// Expectations are built from each route's own measured value rather than from a
		// symmetry assumption. Two cities on identical terrain, the same distance from the
		// capital and the same distance from the partner still are not interchangeable —
		// corruption and the governor's tile picks see to that, measured at 26 against 24
		// base trade — and an earlier fixture that assumed otherwise failed on the arithmetic
		// while the rule under test was working correctly.
		[Fact]
		public void TheSecondRouteToOnePartnerPaysHalf()
		{
			(_, City[] mine, City[] theirs) = TwoContinents();

			int v0 = ValueAlone(mine[0], theirs[0]);
			int v1 = ValueAlone(mine[1], theirs[0]);
			Assert.True(v0 > 0 && v1 > 0, "fixture: a lone route pays nothing at all");

			// The better route keeps full value, the other is ranked second and halved —
			// one and a half routes' worth between them, not two.
			Assert.Equal(System.Math.Max(v0, v1) + System.Math.Min(v0, v1) / 2,
				Anchor(mine, 2, theirs[0]));
		}

		// The curve is harmonic all the way down, never a cliff. The four cities are NOT
		// interchangeable — two sit further from the capital and carry more corruption — so
		// the expectation is built from each route's own measured value, ranked. That also
		// pins the ORDERING: the biggest route takes rank 1.
		[Fact]
		public void FourRoutesToOnePartnerPayTheHarmonicSum()
		{
			(_, City[] mine, City[] theirs) = TwoContinents();

			int[] alone = mine.Select(c => ValueAlone(c, theirs[0]))
			                  .OrderByDescending(v => v).ToArray();
			int expected = alone.Select((v, i) => v / (i + 1)).Sum();

			Assert.Equal(expected, Anchor(mine, 4, theirs[0]));
			Assert.True(expected < alone.Sum(), "fixture: the four routes were not diminished at all");
		}

		// The control, and the whole point of the curve: the same two caravans sent to
		// DIFFERENT partners both pay in full. The ceiling has moved, not dropped.
		[Fact]
		public void TwoRoutesToDifferentPartnersBothPayFull()
		{
			(_, City[] mine, City[] theirs) = TwoContinents();

			int v0 = ValueAlone(mine[0], theirs[0]);
			int v1 = ValueAlone(mine[1], theirs[1]);

			mine[0].AddTradeRoute(theirs[0], "Gold");
			mine[1].AddTradeRoute(theirs[1], "Silk");
			foreach (City c in mine) c.InvalidateCache();

			Assert.Equal(v0, mine[0].TradeRouteBonus);
			Assert.Equal(v1, mine[1].TradeRouteBonus);
		}

		// Scoped per CIVILIZATION. A rival anchoring on the same foreign city must not
		// diminish ours — that eviction-by-proxy is exactly what got Civ 1's cap removed.
		[Fact]
		public void ARivalsRouteToTheSamePartnerDoesNotDiminishOurs()
		{
			(Game g, City[] mine, City[] theirs) = TwoContinents();
			Player third = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                                 && p != g.HumanPlayer && p != theirs[0].Player);

			// Clear of our column — (11,40) is mine[3], and AddCity returns null on an
			// occupied tile, which read as a NullReferenceException rather than a failure.
			third.Explore(8, 14, range: 20);
			City rival = g.AddCity(third, 6, 8, 14)!;
			Assert.NotNull(rival);
			rival.Size = 12;
			rival.ResetResourceTiles();

			int ours = ValueAlone(mine[0], theirs[0]);
			int theirsAlone = ValueAlone(rival, theirs[0]);

			mine[0].AddTradeRoute(theirs[0], "Gold");
			rival.AddTradeRoute(theirs[0], "Gold");
			mine[0].InvalidateCache();
			rival.InvalidateCache();

			// BOTH keep full value. Asserting only ours would prove nothing: whichever route
			// is worth less is the one a wrongly-scoped rank would halve, and in this fixture
			// that is the rival's. Under civ-wide scoping one of these two must drop.
			Assert.Equal(ours, mine[0].TradeRouteBonus);
			Assert.Equal(theirsAlone, rival.TradeRouteBonus);
		}
	}
}
