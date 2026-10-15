// CivOne tests
//
// A domestic route pays x0.5 (same continent) instead of Civ 1's x0.25 when a road, rail or
// tube network joins the two cities. A road with a gap is not a network.

using System;
using System.Linq;
using System.Reflection;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class DomesticRouteTests
	{
		[Theory]
		[InlineData("none", 0.25f)]
		[InlineData("gap", 0.25f)]
		[InlineData("road", 0.5f)]
		public void ARoadJoinedDomesticRouteIsHalvedOnlyOnce(string road, float multiplier)
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 20; y <= 28; y++)
			for (int x = 6; x <= 30; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			p.Explore(18, 24, range: 20);
			City a = g.AddCity(p, 0, 10, 24)!;
			City b = g.AddCity(p, 1, 26, 24)!;
			a.Size = 8; b.Size = 8;
			if (road != "none")
				for (int x = 11; x <= 25; x++)
					Map.Instance[x, 24].Road = !(road == "gap" && x == 18);
			a.InvalidateCache(); b.InvalidateCache();
			Sim.ClearTasks();

			int distance = Common.TradeDistance(a.X, a.Y, b.X, b.Y);
			double pair = 2 * Math.Sqrt((double)Math.Max(1, BaseTrade(a)) * Math.Max(1, BaseTrade(b)));
			Assert.Equal((int)(multiplier * (distance + 10) * pair / 24), Bonus(a, b));
		}

		private static int Bonus(City from, City to) =>
			(int)typeof(City).GetMethod("RouteBonus", BindingFlags.NonPublic | BindingFlags.Instance)!
				.Invoke(from, new object[] { to })!;

		private static int BaseTrade(City c) =>
			(int)typeof(City).GetProperty("BaseTrade", BindingFlags.NonPublic | BindingFlags.Instance)!
				.GetValue(c)!;
	}
}
