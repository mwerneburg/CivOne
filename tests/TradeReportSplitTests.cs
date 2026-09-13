// CivOne tests
//
// What the Trade Advisor adds up.
//
// The summary read "Total Income: {sum of city Taxes}" and nothing else. At a 0% tax rate that
// printed a flat 0 beside city lines showing hundreds of trade — true, and useless: the trade
// was going to science and luxuries and the screen would not say so.
//
// The fix reports the GROSS and then where it went, which only works because of the identity
// below. Route income is INSIDE the tax figure, never beside it: TradeTotal = BaseTrade +
// TradeRouteBonus, and tax is levied on the whole of it. Adding routes to the tax total — the
// obvious-looking change — would have counted them twice and told the player they were earning
// more than the treasury receives.
//
// These test the arithmetic the screen displays rather than the drawing, which needs a
// renderer. The identity is the part that can silently rot.

using System.Linq;
using CivOne;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class TradeReportSplitTests
	{
		// Two cities of the same civ, far enough apart to be worth a route, on trade-bearing
		// ground so BaseTrade is not zero.
		private static (Game game, City home, City partner) TwoTradingCities()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player me = g.HumanPlayer;
			me.Explore(40, 25, range: 40);
			City home = g.AddCity(me, 0, 20, 25)!;
			City partner = g.AddCity(me, 1, 60, 25)!;
			home.Size = 8;
			partner.Size = 8;
			home.ResetResourceTiles();
			partner.ResetResourceTiles();
			Sim.ClearTasks();
			Assert.True(home.BaseTrade > 0, "fixture: the home city generates no trade at all");
			return (g, home, partner);
		}

		// The identity the summary is built on.
		[Fact]
		public void TheSplitSumsToTheTotal()
		{
			var (g, home, partner) = TwoTradingCities();
			home.AddTradeRoute(partner, "Silk");

			Assert.Equal(home.TradeTotal, home.BaseTrade + home.TradeRouteBonus);
			Assert.True(home.TradeRouteBonus > 0, "fixture: the route is worth nothing");
		}

		// The reason the old summary could not simply add trade to the tax line: route income
		// is already taxed, so the two are not independent quantities.
		[Fact]
		public void RouteIncomeIsInsideTheTaxFigure()
		{
			var (g, home, partner) = TwoTradingCities();
			g.HumanPlayer.TaxesRate = 10;
			int taxBefore = home.Taxes;

			home.AddTradeRoute(partner, "Silk");

			Assert.True(home.Taxes > taxBefore,
				"a trade route did not move the tax take, so the two really are separate");
		}

		// ── the columns ──────────────────────────────────────────────────────
		//
		// The layout was fixed pixel offsets inside a 320-wide band and it collided twice on
		// real data: in a 41-city Zulu game three-digit science overprinted the route split,
		// and "Intombe 94/0/1097" ran through "49 Cathedral, 270" in the next column. The
		// geometry is now computed and this is the property it has to have — for any canvas
		// and any content, no column reaches into the one beside it.
		[Theory]
		[InlineData(320)]   // the classic canvas
		[InlineData(400)]
		[InlineData(640)]
		[InlineData(1024)]
		public void TheColumnsNeverOverlap(int width)
		{
			// Adversarial content: a long maintenance line, four-digit trade, wide split.
			foreach (int maint in new[] { 40, 90, 140 })
			foreach (int stats in new[] { 20, 60, 110 })
			foreach (int split in new[] { 12, 30, 70 })
			{
				var col = Screens.Reports.TradeReport.Layout(width, maint, stats, split);

				Assert.True(col.StatsRight <= col.SplitRight - split,
					$"stats ({col.StatsRight}) reach into the split column at {width}px");
				Assert.True(col.SplitRight <= col.MaintX,
					$"the split ({col.SplitRight}) reaches into maintenance ({col.MaintX})");
				Assert.True(col.NameRoom >= 0, "negative room for the city name");
				Assert.True(col.MaintX <= width, "the maintenance column starts off the canvas");
			}
		}

		// A wider canvas must actually be USED — the report was pinned inside a 320-wide band
		// centred on the window, so widening the window bought nothing.
		[Fact]
		public void AWiderCanvasGivesTheCityListMoreRoom()
		{
			var narrow = Screens.Reports.TradeReport.Layout(320, 90, 60, 30);
			var wide   = Screens.Reports.TradeReport.Layout(640, 90, 60, 30);

			Assert.True(wide.NameRoom > narrow.NameRoom,
				$"a 640px canvas gave the names {wide.NameRoom}px against {narrow.NameRoom}px at 320");
		}

		// The reported situation, exactly: no tax allocated, so no income — while the city
		// goes on generating hundreds of trade that the old summary never mentioned.
		[Fact]
		public void AtZeroTaxThereIsNoIncomeAndPlentyOfTrade()
		{
			var (g, home, partner) = TwoTradingCities();
			home.AddTradeRoute(partner, "Silk");
			g.HumanPlayer.TaxesRate = 0;

			Assert.Equal(0, home.Taxes);
			Assert.True(home.TradeTotal > 0,
				"the trade the advisor now reports as gross has gone missing");
		}
	}
}
