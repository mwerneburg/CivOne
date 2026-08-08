// CivOne tests
//
// The strategic-resource layer (Game.ResourceAt) has always read IRON off mountain
// specials and OIL off desert specials, but nothing the player could see agreed:
// the map overlay and the Civilopedia called them Gold and Oasis, and the tiles paid
// like Gold and an Oasis — 5 trade on the mountain, 2 food in the desert. A player
// building cannon at +50% shields had no way to connect the penalty to the ground.
//
// So the deposits now pay for what they are, in shields, and the desert lattice is
// sieved to half — at the full 1-in-16 an Epic Earth Sahara comes out as continuous
// derricks rather than an oilfield.

using CivOne;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class IronAndOilTests
	{
		// Ore pays in shields. 1 base + 2 seam, and nothing in trade — the gold seam's
		// 5 trade is gone.
		[Fact]
		public void MountainIronPaysInShieldsNotTrade()
		{
			var seam = new Mountains(4, 4, special: true);
			var bare = new Mountains(4, 4, special: false);

			Assert.Equal(3, seam.Shield);
			Assert.Equal(0, seam.Trade);
			Assert.Equal(1, bare.Shield);
			Assert.Equal(0, bare.Trade);
		}

		// Desert oil pays what wetland oil pays — 4 shields — and no food at all. The
		// oasis's 2 food went with the oasis.
		[Fact]
		public void DesertOilPaysFourShieldsAndNoFood()
		{
			var well = new Desert(0, 0, special: true);      // (0,0): kept by the sieve
			var swampOil = new Swamp(0, 0, special: true);

			Assert.True(well.Special, "scenario: (0,0) must survive the desert sieve");
			Assert.Equal(4, well.Shield);
			Assert.Equal(0, well.Food);
			Assert.Equal(swampOil.Shield, well.Shield);
		}

		// Irrigation still works on an oil tile — the desert's own food, not the
		// special's. Without this the "deserts have rivers to irrigate from" argument
		// for removing the oasis food doesn't hold.
		[Fact]
		public void AnIrrigatedOilFieldStillGrowsFood()
		{
			var well = new Desert(0, 0, special: true) { Irrigation = true };

			Assert.Equal(1, well.Food);
		}

		// Exactly half the lattice blocks keep their deposit, and evenly — a
		// checkerboard, not a clump. 16 blocks in, 8 out.
		[Fact]
		public void HalfTheDesertLatticeCarriesOil()
		{
			int kept = 0;
			for (int by = 0; by < 4; by++)
			for (int bx = 0; bx < 4; bx++)
				if (new Desert(bx * 4, by * 4, special: true).Special) kept++;

			Assert.Equal(8, kept);
		}

		// Mountains keep the same lattice as before: the halving is desert-only,
		// because it is the Sahara that reads wrong, not the Andes.
		[Fact]
		public void TheMountainLatticeIsUntouched()
		{
			int kept = 0;
			for (int by = 0; by < 4; by++)
			for (int bx = 0; bx < 4; bx++)
				if (new Mountains(bx * 4, by * 4, special: true).Special) kept++;

			Assert.Equal(16, kept);
		}

		// The +50% for a missing material was silent: a Cannon read 60 shields instead
		// of 40 and nothing anywhere said why. City.MissingResource is what the two
		// production screens print, and it has to be the same test the price uses or the
		// flag and the number drift apart.
		[Fact]
		public void ACityWithNoIronIsToldWhichMaterialItLacks()
		{
			var (g, human, city) = AnIronlessTown();

			Assert.Equal(StrategicResource.Iron, city.MissingResource(new Cannon()));
			Assert.Equal(60, city.ProductionCost(new Cannon()));   // 40 base, +50%
		}

		// ...and the flag clears the moment the empire holds the seam, by camp or by
		// worked tile. Same predicate, so the price drops in the same step.
		[Fact]
		public void HoldingTheSeamClearsTheFlagAndThePrice()
		{
			var (g, human, city) = AnIronlessTown();
			Map.Instance.ChangeTileType(50, 25, Terrain.Mountains);
			((BaseTile)Map.Instance[50, 25]).Special = true;
			g.ResourceCamps[(50, 25)] = g.PlayerNumber(human);

			Assert.Equal(StrategicResource.None, city.MissingResource(new Cannon()));
			Assert.Equal(40, city.ProductionCost(new Cannon()));
		}

		// The control: ancient production is deliberately ungated, and must never be
		// flagged — a start with no iron in reach is not a spoiled start.
		[Fact]
		public void AnUngatedUnitIsNeverFlagged()
		{
			var (_, _, city) = AnIronlessTown();

			Assert.Equal(StrategicResource.None, city.MissingResource(new Militia()));
		}

		private static (Game game, Player human, City city) AnIronlessTown()
		{
			Sim.NewGame(width: 80, height: 50, difficulty: 2);
			Settings.Instance.Autopilot = false;
			// No mountains and no hills anywhere near the town, so the empire holds
			// nothing — the generated map otherwise decides the test's outcome.
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Explore(40, 25, range: 20);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, human, c);
		}

		// City.TradeValue gave the mountain special a government trade bonus on top of
		// its base trade. With the base now zero that bonus was the old gold seam still
		// collecting rent, so it is gone — while jungle gems, which is what the rule was
		// really for, still pays.
		[Fact]
		public void AnIronSeamEarnsNoTradeWhileGemsStillDo()
		{
			Sim.NewGame(width: 80, height: 50, difficulty: 2);
			Settings.Instance.Autopilot = false;
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Government = new Monarchy();      // SpecialResourceTradeBonus = 1
			human.Explore(40, 25, range: 10);
			City city = g.AddCity(human, 0, 40, 25)!;

			Map.Instance.ChangeTileType(41, 25, Terrain.Mountains);
			Map.Instance.ChangeTileType(39, 25, Terrain.Jungle);
			// The lattice decides which tiles are special and these two are not on it,
			// so set the flag directly — the point under test is the trade rule, not
			// where deposits land.
			((BaseTile)Map.Instance[41, 25]).Special = true;
			((BaseTile)Map.Instance[39, 25]).Special = true;

			Assert.Equal(0, city.TradeValue(Map.Instance[41, 25]));
			Assert.True(city.TradeValue(Map.Instance[39, 25]) > 0,
				"scenario: gems must still pay a government trade bonus");
		}
	}
}
