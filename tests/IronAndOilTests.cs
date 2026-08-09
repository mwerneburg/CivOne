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
using System.Linq;

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

		// Placer gold, on the standard 1-in-16 map lattice. It cannot use Special —
		// that flag is the river shield, and BaseTile.AlternateSpecial sets it on HALF
		// of every river, which would have minted a gold deposit under most of the
		// world's capitals.
		[Fact]
		public void RiverGoldIsTheLatticeAndNotTheRiverShield()
		{
			Sim.NewGame(width: 80, height: 50);
			int rivers = 0, gold = 0, shield = 0;
			foreach (ITile t in Map.Instance.AllTiles())
			{
				if (t is not Tiles.River r) continue;
				rivers++;
				if (r.Gold) gold++;
				if (r.Special) shield++;
			}

			Assert.True(rivers > 100, $"scenario: only {rivers} river tiles to sample");
			// The shield flag is half of them; the lattice is a small fraction. If gold
			// were ever hung on Special these two would be the same number.
			Assert.True(shield > rivers / 3, $"river shield on {shield} of {rivers}");
			Assert.True(gold < rivers / 5, $"gold on {gold} of {rivers} — too common");
			Assert.True(gold > 0, "scenario: no gold generated at all");
		}

		// A gold river reads 4 trade — jungle gems — not the mountain seam's old 5,
		// because it brings the river's 2 food along with it. Everything else about the
		// tile is unchanged.
		[Fact]
		public void AGoldRiverPaysFourTradeAndKeepsItsFood()
		{
			Sim.NewGame(width: 80, height: 50);
			Tiles.River? seam = null, plain = null;
			foreach (ITile t in Map.Instance.AllTiles())
			{
				if (t is not Tiles.River r) continue;
				if (r.Gold) seam ??= r; else plain ??= r;
			}

			Assert.NotNull(seam);
			Assert.NotNull(plain);
			Assert.Equal(4, seam!.Trade);
			Assert.Equal(1, plain!.Trade);
			Assert.Equal(2, seam.Food);
		}

		// Derived from position, never stored: both loaders and ChangeTileType rebuild
		// tiles from coordinates alone, so a remembered seam would move on reload.
		[Fact]
		public void AGoldSeamSurvivesBeingRebuilt()
		{
			Sim.NewGame(width: 80, height: 50);
			Tiles.River seam = Map.Instance.AllTiles().OfType<Tiles.River>().First(r => r.Gold);
			int x = seam.X, y = seam.Y;

			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.ChangeTileType(x, y, Terrain.River);

			Assert.True(((Tiles.River)Map.Instance[x, y]).Gold);
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

		// Every gated item says so on its OWN page. The "Strategic Resources" concept
		// page has always carried the full list, but nobody reads a concept page before
		// building a Cannon — they read the Cannon page, which talked about Metallurgy
		// and never mentioned iron.
		//
		// Driven off Game.RequiredResource rather than a hand-written list of thirteen,
		// so the next unit added to the gate cannot ship without its line.
		[Fact]
		public void EveryGatedItemNamesItsMaterialOnItsOwnPage()
		{
			var missing = new System.Collections.Generic.List<string>();
			int checkedItems = 0;
			foreach (IProduction item in Reflect.GetProduction())
			{
				StrategicResource need = Game.RequiredResource(item);
				if (need == StrategicResource.None) continue;
				checkedItems++;

				string material = need.ToString().ToUpper();
				var pedia = (ICivilopedia)item;
				// GetPageText lives on BaseUnit/BaseBuilding, not on ICivilopedia, so it
				// has to be reached by reflection rather than through the interface.
				var text = item.GetType().GetMethod("GetPageText");
				bool named = false;
				for (byte page = 1; page <= pedia.PageCount && !named; page++)
					named = (text?.Invoke(item, new object[] { page }) as string[])
						?.Any(l => l.Contains(material)) == true;

				if (!named) missing.Add($"{pedia.Name} (needs {material})");
			}

			Assert.Equal(13, checkedItems);   // scenario: the gate still covers thirteen
			Assert.Empty(missing);
		}

		// The Civilopedia's terrain table had drifted from the code. Checked every cell
		// against City.FoodValue/ShieldValue/TradeValue; six were wrong, and these pin
		// them. Baseline is MONARCHY, which is what the page's two footnotes imply —
		// `*` is "-1 under Despotism/Anarchy" and `%` is "+1 under Republic/Democracy",
		// so the printed number is the one government that is neither.
		//
		// The footnotes themselves turned out to be accurate. What was wrong was
		// BaseGovernment.TilePenalty's comment, which described Civ 1's "dock 1 from
		// any tile yielding 3+" — a rule this codebase does not implement. Nothing
		// docks a tile's own output; the flag only withholds the bonuses City.cs adds.
		[Theory]
		// terrain,             irrigate, mine, special, food, shield, trade
		[InlineData(Terrain.Desert,     true,  false, false, 2, 1, 0)]  // pedia said irrigated 1
		[InlineData(Terrain.Desert,     true,  false, true,  2, 4, 0)]  // oil, irrigated
		[InlineData(Terrain.Forest,     false, false, true,  2, 2, 0)]  // pedia said game 3*
		[InlineData(Terrain.Plains,     false, false, true,  1, 2, 0)]  // pedia said horses 3
		[InlineData(Terrain.River,      false, false, false, 2, 0, 2)]  // pedia said trade 1%
		[InlineData(Terrain.Hills,      false, true,  false, 1, 3, 0)]  // mine bonus: starred
		public void TheTerrainTableMatchesWhatTheCityActuallyPays(
			Terrain terrain, bool irrigate, bool mine, bool special,
			int food, int shield, int trade)
		{
			City city = ATownUnder(new Monarchy());
			ITile t = Map.Instance[45, 25];
			Map.Instance.ChangeTileType(45, 25, terrain);
			t = Map.Instance[45, 25];
			t.Irrigation = irrigate;
			t.Mine = mine;
			((BaseTile)t).Special = special;

			Assert.Equal(food, city.FoodValue(t));
			Assert.Equal(shield, city.ShieldValue(t));
			Assert.Equal(trade, city.TradeValue(t));
		}

		// River gold is 5 trade at Monarchy, not the tile's own 4: City.TradeValue adds
		// one for the river itself. The pedia row said 4% until this was checked.
		[Fact]
		public void AGoldRiverIsWorthFiveTradeToACity()
		{
			City city = ATownUnder(new Monarchy());
			Tiles.River seam = Map.Instance.AllTiles().OfType<Tiles.River>().First(r => r.Gold);

			Assert.Equal(5, city.TradeValue(seam));
		}

		// The starred cells, and only those, lose exactly 1 under Despotism. Nothing
		// docks a tile's own yield — a 4-shield oil field is 4 under either government.
		[Fact]
		public void DespotismWithholdsBonusesAndDocksNothingElse()
		{
			City city = ATownUnder(new Despotism());
			Map.Instance.ChangeTileType(45, 25, Terrain.Hills);
			ITile hills = Map.Instance[45, 25];
			hills.Mine = true;
			Map.Instance.ChangeTileType(46, 25, Terrain.Desert);
			ITile oil = Map.Instance[46, 25];
			((BaseTile)oil).Special = true;

			Assert.Equal(2, city.ShieldValue(hills));   // 3 under Monarchy: the starred cell
			Assert.Equal(4, city.ShieldValue(oil));     // the tile's own yield, untouched
		}

		private static City ATownUnder(IGovernment government)
		{
			Sim.NewGame(width: 80, height: 50, difficulty: 2);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Government = government;
			human.Explore(40, 25, range: 20);
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(human, 0, 40, 25)!;
			Sim.ClearTasks();
			return c;
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
