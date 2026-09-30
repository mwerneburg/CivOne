// CivOne tests
//
// We Love the King Day paid its reward — a free size step, or a free Caravan in a city that
// cannot grow — every time a celebration STARTED. Flipping the luxury slider to end and
// restart celebrations spawned a Caravan in every capped city each time (the user's own
// find, Oct 2026). The reward now comes at most once per City.CelebrationRewardInterval.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CelebrationRewardTests
	{
		// An AI Republic city of size 7 with no Aqueduct, so a celebration pays a Caravan.
		// Temple, Marketplace and Colosseum keep it content; the slider decides the rest.
		private static (Game g, Player p, City c) ACappedCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 50; x++)
			{
				// River, not grassland: grassland yields no trade, so no luxuries to celebrate on.
				Map.Instance.ChangeTileType(x, y, Terrain.River);
				Map.Instance[x, y].Irrigation = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0 && x != g.HumanPlayer);
			p.Government = new CivOne.Governments.Republic();
			p.Explore(40, 25, range: 10);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 7;
			c.AddBuilding(new Temple());
			c.AddBuilding(new MarketPlace());
			c.AddBuilding(new Colosseum());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static int Caravans(Game g, City c) =>
			g.GetUnits().Count(u => u is Caravan && u.Home == c);

		private static void TurnWithLuxuries(Game g, Player p, City c, int lux)
		{
			p.TaxesRate = 10 - lux;
			p.LuxuriesRate = lux;
			c.InvalidateCache();
			c.NewTurn();
			Sim.ClearTasks();
			g.GameTurn++;
		}

		// The exploit: celebrate, stop, celebrate, stop... over ten turns.
		[Fact]
		public void FlippingTheSliderPaysOnceInTenTurns()
		{
			(Game g, Player p, City c) = ACappedCity();
			TurnWithLuxuries(g, p, c, 10);
			Assert.True(c.WasWeLoveKing, $"fixture: no celebration on full luxuries (happy {c.HappyCitizens} content {c.ContentCitizens} unhappy {c.UnhappyCitizens}, lux {c.Luxuries}, food {c.FoodIncome})");
			Assert.Equal(1, Caravans(g, c));   // the first celebration still pays

			for (int i = 1; i < Game_Interval; i++)
				TurnWithLuxuries(g, p, c, i % 2 == 0 ? 10 : 0);

			Assert.Equal(1, Caravans(g, c));
		}

		// ...and once the interval has passed, a new celebration pays again.
		[Fact]
		public void AfterTheIntervalItPaysAgain()
		{
			(Game g, Player p, City c) = ACappedCity();
			TurnWithLuxuries(g, p, c, 10);
			for (int i = 1; i < Game_Interval; i++) TurnWithLuxuries(g, p, c, 0);   // stop

			TurnWithLuxuries(g, p, c, 10);   // start again, ten turns on

			Assert.Equal(2, Caravans(g, c));
		}

		[Fact]
		public void TheRewardTurnSurvivesASave()
		{
			(Game g, Player _, City c) = ACappedCity();
			c.CelebrationRewardTurn = 123;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "wltkd.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));

			Assert.Equal(123, Game.Instance.GetCities().Single(x => x.X == 40 && x.Y == 25).CelebrationRewardTurn);
		}

		private const int Game_Interval = City.CelebrationRewardInterval;
	}
}
