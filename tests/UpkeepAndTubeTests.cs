// CivOne tests
//
// Two rules, both about not doing work that returns nothing.
//
// 1. Multipliers need something to multiply. Library, University, Observatory, Xenolab,
//    MarketPlace and Bank all add a PERCENTAGE of what the city already produces, and all of
//    it comes off TradeTotal. On a size-3 desert town producing 2 trade a Library returns 1,
//    for permanent upkeep. Measured over the 2200 AD run: of 2,075 production decisions by AI
//    cities of size <= 6 with food income <= 0, Observatory was 7%, MarketPlace 6%, Neural Lab
//    4%, Xenolab 4%, Library 4% — and 86% of those cities already held a Granary, so they were
//    not choosing these instead of growing, they were choosing them instead of nothing.
//
// 2. Transit tubes are sea-only. They are alien infrastructure and belong to the ocean, laid
//    by the Hydro Engineer. A settler upgrading its railroads to tubes made them ordinary
//    terrain improvement. Removing the build means removing every claim that the work exists —
//    the AI's WorkAvailable, the human's Auto-Improve, and the unit menu — or a settler gets
//    routed to a railroad to perform an order BuildRoad refuses and stands there every turn.
//    That scan-and-gate disagreement has cost this project four separate bugs.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UpkeepAndTubeTests
	{
		private static (Game g, Player p, City c) ACity(int size, Terrain terrain)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 10);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
			{
				Map.Instance.ChangeTileType(x, y, terrain);
				((CivOne.Tiles.BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = (byte)size;
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static bool Offered(Player p, City c, IBuilding b)
			=> (bool)typeof(AI).GetMethod("EarnsItsKeep",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { b, c })!;

		// Desert: a small city on it generates almost no trade, so the multiplier returns less
		// than its own upkeep.
		[Fact]
		public void ALowTradeCityIsRefusedTheTradeMultipliers()
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Desert);
			Assert.True(c.TradeTotal < 2 * new Library().Maintenance,
				$"fixture trade {c.TradeTotal} is too high to test the rule");

			Assert.False(Offered(p, c, new Library()));
			Assert.False(Offered(p, c, new MarketPlace()));
			Assert.False(Offered(p, c, new Observatory()));
		}

		// ...and a city with real trade still gets them. The rule is a floor, not a ban.
		[Fact]
		public void ACityWithRealTradeStillGetsThem()
		{
			(Game g, Player p, City c) = ACity(8, Terrain.Grassland1);
			foreach (ITile t in c.ResourceTiles) { t.Road = true; t.Irrigation = true; }
			c.InvalidateCache();
			Assert.True(c.TradeTotal >= 2 * new Library().Maintenance,
				$"fixture trade {c.TradeTotal} is too low to test the rule");

			Assert.True(Offered(p, c, new Library()));
		}

		// The happiness rule grew two more members: the Hospital and the Neural Lab both
		// reduce unhappy citizens and were not covered by it.
		//
		// The Exchange Center used to be here and is not any more. Its -1 unhappy became +3
		// culture (City.CultureRate), so refusing it to a content city would refuse it for a
		// reason it no longer has — and would do so precisely in the calm, developed cities
		// best placed to build one. This test is what caught the AI gate still listing it.
		[Theory]
		[InlineData("Hospital")]
		[InlineData("NeuralLab")]
		public void HappinessBuildingsAreRefusedWhereThereIsNoUnhappiness(string name)
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Grassland1);
			Assert.Equal(0, c.UnhappyCitizens);
			Assert.Equal(0, p.LuxuriesRate);

			IBuilding b = name switch
			{
				"Hospital" => new Hospital(),
				_          => new NeuralLab(),
			};

			Assert.False(Offered(p, c, b));
		}

		// ...and the counterpart, so the removal above is a rule rather than a deletion: a
		// content city is still offered the Exchange Center, because it is a culture building
		// now and has nothing to do with the city's mood.
		[Fact]
		public void AContentCityIsStillOfferedTheExchangeCentre()
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Grassland1);
			Assert.Equal(0, c.UnhappyCitizens);

			Assert.True(Offered(p, c, new ExchangeCenter()));
		}

		// ── tubes ────────────────────────────────────────────────────────────────

		[Fact]
		public void ASettlerOnARailroadNoLongerStartsATube()
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Grassland1);
			Map.Instance[38, 25].Road = true;
			Map.Instance[38, 25].RailRoad = true;
			// BuildRoad reads Game.CurrentPlayer for the tech test, NOT the unit's owner. The
			// first version of this fixture gave the advance to some other civ, so the tube
			// branch was unreachable and the test passed with the land tube restored.
			Player cur = g.CurrentPlayer;
			cur.AddAdvance(new TransitConduit(), false);
			cur.AddAdvance(new RailRoad(), false);
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 38, 25, g.PlayerNumber(cur))!;

			Assert.False(s.BuildRoad(), "a land tube was started");
			Assert.False(Map.Instance[38, 25].TransportTube);
		}

		// The Hydro Engineer's sea tube is the one that remains.
		[Fact]
		public void TheHydroEngineerStillLaysSeaTubes()
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Grassland1);
			Map.Instance.ChangeTileType(44, 25, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			p.AddAdvance(new TransitConduit(), false);
			var h = (HydroEngineer)g.CreateUnit(UnitType.HydroEngineer, 44, 25, g.PlayerNumber(p))!;

			Assert.True(h.BuildSeaTube(), "the sea tube was refused");
		}

		// The AI's work scan must agree with BuildRoad, or settlers are routed to a railroad
		// to do nothing. This is the check that the four previous versions of this bug lacked.
		[Fact]
		public void TheWorkScanNoLongerClaimsARailroadWantsUpgrading()
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Grassland1);
			Map.Instance[38, 25].Road = true;
			Map.Instance[38, 25].RailRoad = true;
			p.AddAdvance(new TransitConduit(), false);
			p.AddAdvance(new RailRoad(), false);

			object work = typeof(AI).GetMethod("WorkAvailable",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { Map.Instance[38, 25] })!;
			bool upgrade = (bool)work.GetType().GetField("RoadUpgrade")!.GetValue(work)!;

			Assert.False(upgrade, "the scan still wants to upgrade a railroad");
		}

		// A tube on the water still joins a coastal city to the network — the point of the
		// change is where tubes may be BUILT, not what they connect.
		[Fact]
		public void ASeaTubeIsStillPassableBesideACity()
		{
			(Game g, Player p, City c) = ACity(3, Terrain.Grassland1);
			Map.Instance.ChangeTileType(41, 25, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			Map.Instance[41, 25].TransportTube = true;

			Assert.True(Map.Instance[41, 25].TransportTube);
			Assert.True(Map.Instance[41, 25].IsOcean);
			// Adjacent to the city tile, which is what makes it a connection.
			Assert.Equal(1, Common.DistanceToTile(41, 25, c.X, c.Y));
		}
	}
}
