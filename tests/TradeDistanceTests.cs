// CivOne tests
//
// Civ 1 measured trade and corruption with its own distance, max + min/2: a diagonal step
// counts one and a half, which tracks a caravan's real travel time better than max(dx, dy).
// We used max(dx, dy), so a city twelve tiles off on the diagonal paid, and leaked, exactly
// what one twelve tiles due east did, though the caravan walked half as far again.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class TradeDistanceTests
	{
		[Fact]
		public void ADiagonalCountsOneAndAHalf()
		{
			Sim.NewGame(width: 80, height: 50);
			Assert.Equal(12, Common.TradeDistance(20, 20, 32, 20));
			Assert.Equal(18, Common.TradeDistance(20, 20, 32, 32));
			Assert.Equal(13, Common.TradeDistance(20, 20, 30, 26));   // 10 + 6/2
			Assert.Equal(4, Common.TradeDistance(78, 10, 2, 10));     // across the date line
		}

		// A world of grass so every city's own trade is the same, and a home city with two
		// partners at the same max(dx, dy): one due east, one on the diagonal.
		private static (City home, City east, City diagonal) ThreeCities()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 5; y <= 45; y++) for (int x = 5; x <= 75; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			// Roads, so the cities have trade worth measuring.
			for (int y = 5; y <= 45; y++) for (int x = 5; x <= 75; x++) Map.Instance[x, y].Road = true;
			Game g = Game.Instance;
			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Explore(30, 25, range: 20);
			City home = g.AddCity(p, 0, 20, 15)!, east = g.AddCity(p, 1, 36, 15)!, diagonal = g.AddCity(p, 2, 36, 31)!;
			foreach (City c in new[] { home, east, diagonal }) c.Size = 8;
			Sim.ClearTasks();
			return (home, east, diagonal);
		}

		[Fact]
		public void ADiagonalRoutePaysMoreThanAStraightOneOfTheSameReach()
		{
			(City home, City east, City diagonal) = ThreeCities();
			// Democracy's corruption reads size, not distance, so both partners keep the same
			// trade and only the route's own distance differs.
			Game.Instance.GetPlayer(home.Owner).Government = new CivOne.Governments.Democracy();
			Assert.Equal(east.BaseTrade, diagonal.BaseTrade);

			home.AddTradeRoute(east, "Silk");
			home.AddTradeRoute(diagonal, "Silk");
			int viaEast = home.TradeRoutes.Single(r => r.Partner == east).Value;
			int viaDiagonal = home.TradeRoutes.Single(r => r.Partner == diagonal).Value;

			Assert.True(viaDiagonal > viaEast, $"diagonal {viaDiagonal} vs straight {viaEast}");
		}

		[Fact]
		public void ADiagonalCityLeaksMoreThanAStraightOneOfTheSameReach()
		{
			(City capital, City east, City diagonal) = ThreeCities();
			capital.AddBuilding(new Palace());
			Assert.Equal(east.RawTradeForAi, diagonal.RawTradeForAi);

			Assert.True(diagonal.Corruption > east.Corruption, $"diagonal {diagonal.Corruption} vs straight {east.Corruption}");
		}
			// The caravan's one-off delivery gold is the same formula, and was the same distance.
		[Fact]
		public void ADiagonalDeliveryPaysMoreThanAStraightOneOfTheSameReach()
		{
			(City home, City east, City diagonal) = ThreeCities();
			Game.Instance.GetPlayer(home.Owner).Government = new CivOne.Governments.Democracy();
			Assert.Equal(east.TradeTotal, diagonal.TradeTotal);
			IUnit caravan = Game.Instance.CreateUnit(UnitType.Caravan, home.X, home.Y, home.Owner)!;
			caravan.SetHome(home);

			int toEast = CaravanActions.TradeGoldBonus(caravan, east);
			int toDiagonal = CaravanActions.TradeGoldBonus(caravan, diagonal);

			Assert.True(toDiagonal > toEast, $"diagonal {toDiagonal} vs straight {toEast}");
		}
	}
}
