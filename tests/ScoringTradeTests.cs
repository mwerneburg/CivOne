// CivOne tests
//
// Money and points are different questions.
//
// Trade routes are uncapped and each one is worth more the more trade sits at either end, so
// the economic victory was a counting exercise: found cities, wire every pair of them
// together, and a civilization of n cities can lay n(n-1) routes and score off its own
// internal traffic. A civilization already knows where its own cities are and what they make
// — the caravan exists to reach somebody who does not.
//
// So the scoreboard is now narrower than the ledger. Every route still PAYS in full
// (TradeTotal is untouched); what counts toward Pax Mercatoria and the Economic Output graph
// is BaseTrade plus the best City.ScoringRoutes EXTERNAL routes.
//
// This is a partial reinstatement of Civ 1's three-per-city cap, kept for the reason the
// original had it and not for the reason it was removed: the old cap let an arriving foreign
// caravan silently evict a route the player had built. Routes stay uncapped, so nothing is
// evicted — it is the scoreboard that is capped.

using System.Linq;
using System.Reflection;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class ScoringTradeTests
	{
		// One home city on a river continent, and a shoal of partner cities on a second
		// continent far away, so no halving applies to the external routes.
		private static (Game game, City home, City[] foreign, City[] domestic) ATradingHub()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 14; y <= 34; y++)
			{
				for (int x = 6; x <= 18; x++)  Map.Instance.ChangeTileType(x, y, Terrain.River);
				for (int x = 58; x <= 70; x++) Map.Instance.ChangeTileType(x, y, Terrain.River);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player me = g.HumanPlayer;
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != me);
			me.Explore(12, 24, range: 40);
			them.Explore(64, 24, range: 40);

			City home = g.AddCity(me, 0, 12, 24)!;
			home.Size = 16;

			// Eight of each, so both the cap (5) and the internal/external split have room to
			// show. Sizes vary so the "best five" is a real ordering, not a tie.
			var foreign = new City[8];
			var domestic = new City[8];
			for (int i = 0; i < 8; i++)
			{
				foreign[i] = g.AddCity(them, (byte)(i + 1), 60 + (i % 4), 18 + 2 * (i / 4))!;
				foreign[i].Size = (byte)(4 + 2 * i);
				domestic[i] = g.AddCity(me, (byte)(i + 10), 8 + (i % 4), 16 + 2 * (i / 4))!;
				domestic[i].Size = (byte)(4 + 2 * i);
			}
			foreach (City c in foreign.Concat(domestic).Append(home)) c.InvalidateCache();
			Sim.ClearTasks();
			return (g, home, foreign, domestic);
		}

		private static int BaseTrade(City c) =>
			(int)typeof(City).GetProperty("BaseTrade", BindingFlags.NonPublic | BindingFlags.Instance)!
				.GetValue(c)!;

		private static int Bonus(City from, City to) =>
			(int)typeof(City).GetMethod("RouteBonus", BindingFlags.NonPublic | BindingFlags.Instance)!
				.Invoke(from, new object[] { to })!;

		// The fixture has to produce routes that are actually worth something, or every
		// assertion below passes on a pile of zeroes.
		[Fact]
		public void TheFixtureRoutesAreWorthSomething()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();

			Assert.True(Bonus(home, foreign[7]) > 0, "the external routes are worthless");
			Assert.True(Bonus(home, domestic[7]) > 0, "the internal routes are worthless");
		}

		// Internal routes pay. Nothing here is about taking income away.
		[Fact]
		public void InternalRoutesStillPayTheCity()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			int before = home.TradeTotal;

			foreach (City c in domestic) home.AddTradeRoute(c, "Grain");

			Assert.True(home.TradeTotal > before,
				"an internal route stopped paying — this change was only ever about scoring");
		}

		// ...and they do not score.
		[Fact]
		public void InternalRoutesDoNotScore()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			int before = home.ScoringTrade;

			foreach (City c in domestic) home.AddTradeRoute(c, "Grain");

			Assert.Equal(before, home.ScoringTrade);
		}

		// The cap: past the fifth external route, more routes are more money and no more
		// points.
		[Fact]
		public void OnlyTheBestFiveExternalRoutesScore()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			foreach (City c in foreign) home.AddTradeRoute(c, "Silk");

			int expected = home.TradeRoutes
				.Select(r => Bonus(home, r.Partner))
				.OrderByDescending(v => v)
				.Take(City.ScoringRoutes)
				.Sum();

			Assert.Equal(8, home.TradeRouteCount);
			Assert.Equal(expected, home.ScoringTrade - BaseTrade(home));
		}

		// It is the BEST five, not the first five to arrive. Adding a better partner has to
		// raise the score even when the city is already at the cap.
		[Fact]
		public void ABetterPartnerDisplacesAWorseOneOnTheScoreboard()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();

			// Ranked by what the route is actually WORTH, not by partner size: value carries
			// distance and the partner's corruption too, and the first draft of this test
			// assumed size order and picked a "worst" partner that was not the worst.
			City[] ranked = foreign.OrderBy(c => Bonus(home, c)).ToArray();
			foreach (City c in ranked.Take(5)) home.AddTradeRoute(c, "Silk");   // the five worst
			int atCap = home.ScoringTrade;

			home.AddTradeRoute(ranked.Last(), "Spice");   // the best partner, added last

			Assert.True(home.ScoringTrade > atCap,
				"the sixth route was ignored on merit order rather than value");
		}

		// ...and a WORSE one past the cap changes nothing on the scoreboard while still
		// raising the takings.
		[Fact]
		public void AWorsePartnerPastTheCapPaysButDoesNotScore()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			City[] ranked = foreign.OrderByDescending(c => Bonus(home, c)).ToArray();
			foreach (City c in ranked.Take(5)) home.AddTradeRoute(c, "Silk");   // the five best
			int score = home.ScoringTrade;
			int paid = home.TradeTotal;

			home.AddTradeRoute(ranked.Last(), "Reeds");   // the worst partner of the eight

			Assert.Equal(score, home.ScoringTrade);
			Assert.True(home.TradeTotal > paid, "the sixth route stopped paying");
		}

		// The whole point, end to end: the wide civilization that wires its own cities
		// together gains no ground on the measure the victory reads.
		[Fact]
		public void AnInternalOnlyNetworkDoesNotMoveEconomicOutput()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			int before = home.EconomicOutput;
			int[] partnersBefore = domestic.Select(c => c.EconomicOutput).ToArray();

			foreach (City c in domestic)
			{
				home.AddTradeRoute(c, "Grain");
				c.AddTradeRoute(home, "Grain");
			}

			// Neither end gains: the hub, and every city it wired itself to.
			Assert.Equal(before, home.EconomicOutput);
			for (int i = 0; i < domestic.Length; i++)
				Assert.Equal(partnersBefore[i], domestic[i].EconomicOutput);
		}

		// One external route does move it, so the rule is "internal earns nothing", not
		// "routes earn nothing".
		[Fact]
		public void AnExternalRouteDoesMoveEconomicOutput()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			int before = home.EconomicOutput;

			home.AddTradeRoute(foreign[7], "Silk");

			Assert.True(home.EconomicOutput > before);
		}

		// The city screen's income box. It is a Picture built from live values, so it is pinned
		// at the source: a rendered 144x83 bitmap cannot be read back for its text.
		[Fact]
		public void TheCityScreenBoxIsLabelledOutputAndShowsCulture()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "CityManagerPanels", "CityInfo.cs"));

			Assert.Contains("Output:", src);
			Assert.Contains("Culture:", src);
			Assert.Contains("_city.EconomicOutput", src);
			Assert.Contains("_city.CultureRate", src);
			Assert.DoesNotContain("$\"Trade:", src);
		}

		// The cache has to move with the routes, or the first read of a city freezes its
		// score for the rest of the game.
		[Fact]
		public void TheScoreFollowsTheRoutes()
		{
			(Game g, City home, City[] foreign, City[] domestic) = ATradingHub();
			home.AddTradeRoute(foreign[7], "Silk");
			int with = home.ScoringTrade;

			home.RemoveTradeRoutesTo(foreign[7]);

			Assert.True(home.ScoringTrade < with, "the scoring cache did not notice the route going away");
		}
	}
}
