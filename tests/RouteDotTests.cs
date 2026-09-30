// CivOne tests
//
// A black dot on a foreign city's banner for each of the player's cities with a trade route
// to it (the user, Oct 2026), so the player can see where their caravans have already been.

using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Tests
{
	public class RouteDotTests
	{
		// Our cities at x = 20, 24, ... and one foreign city far off at 60,25.
		private static (Game g, City[] ours, City theirs) AWorld(int ourCities)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 10; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			City[] ours = Enumerable.Range(0, ourCities).Select(i => g.AddCity(g.HumanPlayer, i, 12 + i * 4, 25)!).ToArray();
			City theirs = g.AddCity(them, ourCities, 66, 25)!;
			Sim.ClearTasks();
			return (g, ours, theirs);
		}

		[Fact]
		public void OneDotPerOfOurCitiesWithARoute()
		{
			(Game _, City[] ours, City theirs) = AWorld(3);
			ours[0].AddTradeRoute(theirs, "Silk");
			ours[2].AddTradeRoute(theirs, "Silk");

			Assert.Equal(2, Icons.RouteDotCount(theirs));
		}

		// Only foreign cities carry them: our own banner is not a ledger of our trade.
		[Fact]
		public void OurOwnCitiesHaveNone()
		{
			(Game _, City[] ours, City _) = AWorld(2);
			ours[1].AddTradeRoute(ours[0], "Silk");

			Assert.Equal(0, Icons.RouteDotCount(ours[0]));
		}

		[Fact]
		public void EightAtMost()
		{
			(Game _, City[] ours, City theirs) = AWorld(10);
			foreach (City c in ours) c.AddTradeRoute(theirs, "Silk");

			Assert.Equal(Icons.MaxRouteDots, Icons.RouteDotCount(theirs));
		}

		// On the drawn icon: the banner pixel is black where a dot is, and banner colour where not.
		[Fact]
		public void TheDotsAreOnTheBanner()
		{
			(Game _, City[] ours, City theirs) = AWorld(1);
			byte before = Icons.City(theirs).Bitmap[1, 14];
			ours[0].AddTradeRoute(theirs, "Silk");
			IBitmap after = Icons.City(theirs);

			Assert.NotEqual(CassetteTheme.BG0, before);
			Assert.Equal(CassetteTheme.BG0, after.Bitmap[1, 14]);
			Assert.NotEqual(CassetteTheme.BG0, after.Bitmap[3, 14]);   // only one dot
		}
	}
}
