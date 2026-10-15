// CivOne tests
//
// Only a city's best ceil(Size / 4) routes pay. Routes were uncapped so a foreign caravan could
// never evict one of yours, and a small hub city collected from every caravan the world sent
// it: on a turn-393 save the size-5 Aztec Ayontzinco was paid by 19 routes.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class PaidRoutesTests
	{
		[Fact]
		public void ASmallHubIsPaidOnlyByItsBestRoutes()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 10; y <= 40; y++)
			{
				for (int x = 4; x <= 12; x++)  Map.Instance.ChangeTileType(x, y, Terrain.River);
				for (int x = 60; x <= 68; x++) Map.Instance.ChangeTileType(x, y, Terrain.River);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player a = g.HumanPlayer;
			Player b = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != a);
			a.Explore(8, 25, range: 60);
			b.Explore(64, 25, range: 60);

			City hub = g.AddCity(b, 0, 64, 25)!;
			hub.Size = 2;
			City[] partners = new City[3];
			for (int i = 0; i < 3; i++)
			{
				partners[i] = g.AddCity(a, i + 1, 8, 13 + i * 10)!;
				partners[i].Size = (byte)(4 + 8 * i);   // unequal, so the routes differ in value
				hub.AddTradeRoute(partners[i], "Silk");
			}
			Sim.ClearTasks();
			hub.InvalidateCache();

			int[] values = hub.TradeRoutes.Select(r => r.Value).OrderByDescending(v => v).ToArray();
			Assert.Equal(3, values.Length);
			Assert.True(values[2] > 0 && values[0] > values[2], string.Join(",", values));

			// Size 2: one paid route, the best. The other two are kept.
			Assert.Equal(values[0], hub.TradeRouteBonus);
			Assert.Equal(3, hub.TradeRouteCount);

			// Size 5: two.
			hub.Size = 5;
			int[] grown = hub.TradeRoutes.Select(r => r.Value).OrderByDescending(v => v).ToArray();
			Assert.Equal(grown[0] + grown[1], hub.TradeRouteBonus);
		}
	}
}
