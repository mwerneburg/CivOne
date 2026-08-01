// CivOne tests
//
// Two ways the warming model ran away on a large map, both found in a turn-551 /
// 1996 AD autoplayed game on 320x200:
//
//   1. The trigger threshold (8 + 2n polluted tiles) was Civ 1's, counted against
//      Civ 1's fixed 80x50 map. Unscaled on a 64000-tile map it fired a planet-wide
//      climate event on 14 smoking tiles — three events by 1996 AD with one civ
//      responsible, the icecaps down to 0.3% of land, and swamp risen to 14.4%.
//   2. The flood pass compared each tile's ocean-neighbour count against
//      max(0, 7 - n). At n >= 7 that bound is 0, and every land tile in the world
//      satisfies `>= 0` — so one event past the seventh converted the entire map,
//      inland mountains included, to swamp and jungle and wiped every irrigation
//      and mine on it.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class GlobalWarmingTests
	{
		// Pollute n land tiles far from anything and hand back the count.
		private static int Pollute(int n)
		{
			int done = 0;
			foreach (ITile tile in Map.Instance.AllTiles().Where(t => !t.IsOcean && t.City is null))
			{
				if (done >= n) break;
				tile.Pollution = true;
				done++;
			}
			return done;
		}

		// A handful of smoking tiles on a huge map is not a planetary emergency.
		[Fact]
		public void OnALargeMap_AFewPollutedTilesDoNotTriggerWarming()
		{
			Sim.NewGame(width: 320, height: 200);
			Game g = Game.Instance;
			g.GlobalWarmingCount = 0;

			// 20 tiles: comfortably over the old unscaled threshold of 8.
			Assert.Equal(20, Pollute(20));
			g.HandleGlobalWarming();

			Assert.Equal(0, g.GlobalWarmingCount);
			Assert.Equal(20, Map.Instance.AllTiles().Count(t => t.Pollution));
		}

		// ...but a genuinely filthy world still warms. 8/4000 of the map is the bar.
		[Fact]
		public void OnALargeMap_ProportionalPollutionStillTriggersWarming()
		{
			Sim.NewGame(width: 320, height: 200);
			Game g = Game.Instance;
			g.GlobalWarmingCount = 0;

			int needed = 8 * 320 * 200 / 4000;                 // 128
			Assert.True(Pollute(needed + 10) >= needed, "map should have enough land to pollute");
			g.HandleGlobalWarming();

			Assert.Equal(1, g.GlobalWarmingCount);
		}

		// The small-map behaviour Civ 1 had must be unchanged: 8 tiles on 80x50 warms.
		[Fact]
		public void OnAStandardMap_TheOriginalThresholdIsUnchanged()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			g.GlobalWarmingCount = 0;

			Assert.Equal(8, Pollute(8));
			g.HandleGlobalWarming();

			Assert.Equal(1, g.GlobalWarmingCount);
		}

		// Ground that touches no sea cannot be flooded by the sea, however hot it gets.
		[Fact]
		public void DeepInland_IsNeverFloodedNoMatterHowManyEvents()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;

			// A continent big enough that its middle has no ocean neighbour.
			int cx = 40, cy = 25;
			for (int dy = -4; dy <= 4; dy++)
			for (int dx = -4; dx <= 4; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Mountains);
			Map.Instance.RecalculateContinentsIfDirty();
			Assert.Equal(0, Map.Instance[cx, cy].GetBorderTiles().Count(t => t is not null && t.IsOcean));

			// Well past the point where the old bound bottomed out at zero.
			g.GlobalWarmingCount = 9;
			Pollute(4000);
			g.HandleGlobalWarming();

			Assert.Equal(Terrain.Mountains, Map.Instance[cx, cy].Type);
		}

		// The counter must still bite where the sea actually reaches.
		[Fact]
		public void Coastline_IsStillFloodedAtHighCounts()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;

			// A lone forest tile in open water — every neighbour is ocean.
			int cx = 40, cy = 25;
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Ocean);
			Map.Instance.ChangeTileType(cx, cy, Terrain.Forest);
			Map.Instance.RecalculateContinentsIfDirty();

			g.GlobalWarmingCount = 9;
			Pollute(4000);
			g.HandleGlobalWarming();

			Assert.Equal(Terrain.Jungle, Map.Instance[cx, cy].Type);   // forest floods to jungle
		}
	}
}
