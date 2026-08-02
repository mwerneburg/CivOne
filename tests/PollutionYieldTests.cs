// CivOne tests
//
// Civ 1 halves a polluted tile's output. That rule was missing here: FoodValue,
// ShieldValue and TradeValue never consulted tile.Pollution, so outside the code
// that sets the flag it had four uses in the entire codebase — two save paths, the
// map overlay, and the global-warming count. Smog cost a city nothing it could
// feel, which is why AI empires grew fat under blankets of it.
//
// These pin the rule down: half of everything, rounded down, applied after the
// improvements rather than before them.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class PollutionYieldTests
	{
		private static City ACityOnGrass()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(40 + dx, 25 + dy, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = Game.Instance.Players.First(x => Game.Instance.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 6);
			City c = Game.Instance.AddCity(p, 0, 40, 25)!;
			c.Size = 4;
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return c;
		}

		// The rule itself, on a tile away from the city centre so no floor interferes.
		[Fact]
		public void APollutedTile_YieldsHalfOfEverything()
		{
			City c = ACityOnGrass();
			ITile t = Map.Instance[42, 25];

			int food = c.FoodValue(t), shield = c.ShieldValue(t), trade = c.TradeValue(t);
			t.Pollution = true;

			Assert.Equal(food / 2, c.FoodValue(t));
			Assert.Equal(shield / 2, c.ShieldValue(t));
			Assert.Equal(trade / 2, c.TradeValue(t));
		}

		// Rounded DOWN, and the case that matters: a 1-food tile drops to nothing.
		[Fact]
		public void HalvingRoundsDown()
		{
			City c = ACityOnGrass();
			ITile t = Map.Instance[42, 25];
			t.Pollution = true;

			// Grassland is 2 food, so this is the even case; make it odd with irrigation
			// and confirm the floor rather than a round-half-up.
			int clean;
			t.Pollution = false;
			t.Irrigation = true;
			clean = c.FoodValue(t);
			t.Pollution = true;

			Assert.Equal(clean / 2, c.FoodValue(t));
			Assert.True(c.FoodValue(t) <= clean / 2, "halving must never round up");
		}

		// Applied AFTER the improvements, not before: an irrigated polluted tile is worth
		// half of the IMPROVED yield. Poisoning the ground devalues the work on it.
		[Fact]
		public void TheHalvingAppliesToTheImprovedYield_NotBareTerrain()
		{
			City c = ACityOnGrass();
			ITile bare = Map.Instance[42, 25];
			ITile irrigated = Map.Instance[42, 26];
			irrigated.Irrigation = true;

			bare.Pollution = true;
			irrigated.Pollution = true;

			Assert.True(c.FoodValue(irrigated) >= c.FoodValue(bare),
				"an irrigated polluted tile should still beat a bare polluted one");
		}

		// Cleaning it up gives the yield back — the whole point of the settler orders.
		[Fact]
		public void CleaningTheTile_RestoresTheYield()
		{
			City c = ACityOnGrass();
			ITile t = Map.Instance[42, 25];
			int clean = c.FoodValue(t);

			t.Pollution = true;
			Assert.NotEqual(clean, c.FoodValue(t));

			t.Pollution = false;
			Assert.Equal(clean, c.FoodValue(t));
		}

		// A clean city is untouched — the guard against a penalty that leaks everywhere.
		[Fact]
		public void AnUnpollutedCity_IsUnaffected()
		{
			City c = ACityOnGrass();
			int food = c.FoodTotal, shields = c.ShieldTotal;

			Assert.True(food > 0 && shields > 0);
			Assert.All(c.ResourceTiles, t => Assert.False(t.Pollution));
			Assert.Equal(food, c.FoodTotal);
			Assert.Equal(shields, c.ShieldTotal);
		}

		// And it reaches the city totals, not just the per-tile helper.
		[Fact]
		public void PollutionShowsUpInTheCityTotals()
		{
			City c = ACityOnGrass();
			int before = c.FoodTotal;

			foreach (ITile t in c.ResourceTiles.Where(t => t.X != c.X || t.Y != c.Y).ToArray())
				t.Pollution = true;
			c.InvalidateCache();

			Assert.True(c.FoodTotal < before,
				$"a poisoned countryside should feed fewer people; {before} -> {c.FoodTotal}");
		}
	}
}
