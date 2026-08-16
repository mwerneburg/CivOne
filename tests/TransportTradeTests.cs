// CivOne tests
//
// Transport tiers and what they are worth.
//
// Two defects, one root cause. Road, RailRoad and TransportTube mask each other so the
// renderer draws only the highest tier present — and every RULE that asked "is there a road
// here" inherited that masking. A tubed grassland reported Road = false AND RailRoad = false,
// so Grassland.Trade returned 0, the government road bonus was skipped, and the rail
// multiplier did not apply: the best transport in the game took a worked tile from 2 trade to
// none. 694 tiles carried tubes in a finished 2200 AD game.
//
// Separately, the multiplier ran on tile.Trade alone and rounded DOWN, so on a roaded
// grassland — most worked land in an empire — floor(1 * 1.5) = 1 and a national rail network
// bought nothing at all.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class TransportTradeTests
	{
		// One city on grassland, one worked tile at a known offset we can improve.
		private static (Game g, Player p, City c, ITile t) AWorkedTile(bool democracy)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0);
			p.Government = democracy ? new Governments.Democracy() : new Governments.Monarchy();
			p.Explore(45, 25, range: 30);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 4;
			Sim.ClearTasks();
			return (g, p, c, Map.Instance[42, 25]);
		}

		private static int TradeOf(City c, ITile t) => c.TradeValue(t);

		// The headline: a railway on ordinary land must be worth something. floor(1 * 1.5) = 1
		// meant it was worth nothing at all, under any government.
		[Theory]
		[InlineData(false)]   // Monarchy: no government road bonus
		[InlineData(true)]    // Democracy: +1 per roaded tile
		public void ARailwayIsWorthMoreThanARoadOnOrdinaryLand(bool democracy)
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy);

			t.Road = true;
			int roaded = TradeOf(c, t);
			t.RailRoad = true;
			int railed = TradeOf(c, t);

			Assert.True(railed > roaded,
				$"rail bought nothing: road {roaded}, rail {railed} (democracy={democracy})");
		}

		// A tube must never be worth LESS than the tier it replaces. Not "more": tubes are
		// water-only in practice (all 694 in a finished game were on ocean, none on land) and
		// ocean is already the richest trade terrain, so they take the same 1.5 as rail rather
		// than a tier of their own.
		[Fact]
		public void ATubeIsNeverWorthLessThanTheRailwayItReplaces()
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy: true);

			t.Road = true;
			t.RailRoad = true;
			int railed = TradeOf(c, t);
			t.TransportTube = true;
			int tubed = TradeOf(c, t);

			Assert.True(tubed >= railed, $"tube {tubed} was worth less than rail {railed}");
		}

		// The case that actually occurs: a tubed OCEAN tile, which is what all 694 of them are.
		[Fact]
		public void ATubedOceanTileCountsItsTransportLink()
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy: true);
			Map.Instance.ChangeTileType(41, 24, Terrain.Ocean);
			ITile sea = Map.Instance[41, 24];

			int bare = TradeOf(c, sea);
			sea.TransportTube = true;
			int tubed = TradeOf(c, sea);

			Assert.True(tubed > bare, $"a tubed ocean tile ({tubed}) should beat an empty one ({bare})");
		}

		// The bug in its starkest form: upgrading must never REDUCE a tile's trade to zero.
		[Fact]
		public void UpgradingToATubeNeverDestroysTheTilesTrade()
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy: true);

			t.Road = true;
			int roaded = TradeOf(c, t);
			t.TransportTube = true;
			int tubed = TradeOf(c, t);

			Assert.True(roaded > 0, "fixture: a roaded grassland should yield trade");
			Assert.True(tubed >= roaded, $"tube {tubed} was worth LESS than plain road {roaded}");
		}

		// The masking, tested directly. This is the property every rule actually wanted.
		[Fact]
		public void ATubedTileStillReportsATransportLink()
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy: true);
			t.Road = true;
			t.RailRoad = true;
			t.TransportTube = true;

			Assert.False(t.Road, "fixture: Road is masked by the higher tiers, by design");
			Assert.False(t.RailRoad, "fixture: RailRoad is masked by TransportTube, by design");
			Assert.True(t.HasTransportLink, "a tubed tile has a transport link");
		}

		// Food and shields lost the multiplier to the same masking. Rounding there is
		// deliberately unchanged — only the tube blindness was a bug.
		[Fact]
		public void ATubeKeepsTheFoodAndShieldMultiplierARailwayHad()
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy: true);
			t.Road = true;
			t.RailRoad = true;
			int railFood = c.FoodValue(t), railShield = c.ShieldValue(t);
			t.TransportTube = true;

			Assert.Equal(railFood, c.FoodValue(t));
			Assert.Equal(railShield, c.ShieldValue(t));
		}

		// Nothing may conjure trade from bare ground: the multiplier only compounds an
		// existing yield, so an unimproved grassland stays at zero however good the transport
		// technology gets.
		[Fact]
		public void TransportDoesNotCreateTradeWhereThereIsNone()
		{
			(Game g, Player p, City c, ITile t) = AWorkedTile(democracy: true);
			Map.Instance.ChangeTileType(t.X, t.Y, Terrain.Forest);   // forest has no base trade
			ITile forest = Map.Instance[t.X, t.Y];
			forest.RailRoad = true;

			Assert.Equal(0, TradeOf(c, forest));
		}
	}
}
