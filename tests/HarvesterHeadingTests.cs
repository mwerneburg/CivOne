// CivOne tests
//
// Observed in the 2200 AD run: harvesters east of a continent followed the shore, harvesters
// west of one charged off into the empty north-west.
//
// WalkHarvester scans rings of increasing Chebyshev distance and took the FIRST worthwhile
// tile in the ring — but every tile on a ring is the same Chebyshev distance away, so the scan
// order (dy from -r, dx from -r) was the tiebreak, and the north-west corner always won.
//
// That is why the two coasts behaved differently. With the sea to the EAST, the northern and
// north-western tiles are land, so the first hit was the sea and the craft walked the shore.
// With the sea to the WEST, the north-west corner IS sea and won every ring, every turn — a
// diagonal march up the coastline and out into open ocean, regardless of where the water
// actually lay.
//
// Same row-major trap as the original Arctic landing. Ranking within the ring by squared
// Euclidean distance fixes it: on a ring the orthogonal tiles are genuinely nearer than the
// corners (r vs r*sqrt(2)), which is the distance the craft has to walk.

using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class HarvesterHeadingTests
	{
		// All land, no specials: only the water this test places is worth walking to.
		private static Game ADryWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				if (Map.Instance[x, y] is BaseTile t) t.Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Map.Instance.ComputeFreshwaterLakes();
			return Game.Instance;
		}

		private static void Water(int x, int y)
		{
			Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			Map.Instance.ComputeFreshwaterLakes();
		}

		// The defect, stated directly: a corner of the ring must not beat an orthogonal tile
		// that is genuinely nearer. Both candidates sit at Chebyshev 3; the eastern one is 3
		// tiles away, the north-western one is 4.2.
		[Fact]
		public void TheNearerWaterWinsEvenWhenItIsNotToTheNorthWest()
		{
			Game g = ADryWorld();
			Water(27, 27);   // dx -3, dy -3 — first in the old scan order
			Water(33, 30);   // dx +3, dy  0 — actually closer
			IUnit craft = g.CreateUnit(UnitType.Harvester, 30, 30, 0)!;

			g.WalkHarvester(craft);

			Assert.Equal((31, 30), ((int)craft.X, (int)craft.Y));
		}

		// The west-coast case from the run: sea to the west, and the craft must go west.
		[Fact]
		public void ACraftOnAWestCoastFollowsTheShore()
		{
			Game g = ADryWorld();
			for (int y = 20; y <= 40; y++) Water(25, y);
			IUnit craft = g.CreateUnit(UnitType.Harvester, 27, 30, 0)!;

			g.WalkHarvester(craft);

			Assert.Equal(26, (int)craft.X);
			Assert.Equal(30, (int)craft.Y);
		}

		// A genuine tie must not become a heading. Water due north and due west of the craft is
		// equidistant by any measure, and first-wins would send every craft in the world north.
		[Fact]
		public void AGenuineTieIsNotAHeading()
		{
			Game g = ADryWorld();
			Water(30, 27);   // due north
			Water(27, 30);   // due west
			IUnit craft = g.CreateUnit(UnitType.Harvester, 30, 30, 0)!;

			bool wentNorth = false, wentWest = false;
			for (int trial = 0; trial < 60; trial++)
			{
				craft.X = 30; craft.Y = 30;
				g.WalkHarvester(craft);
				if (craft.Y == 29) wentNorth = true;
				if (craft.X == 29) wentWest = true;
			}

			Assert.True(wentNorth && wentWest,
				$"north={wentNorth} west={wentWest} — one direction always winning is the march");
		}

		// The nearer ring still wins outright: this is a tiebreak within a ring, not a
		// replacement for the ring order.
		[Fact]
		public void ACloserRingStillBeatsAFurtherOne()
		{
			Game g = ADryWorld();
			Water(28, 28);   // Chebyshev 2, to the north-west
			Water(36, 30);   // Chebyshev 6, due east
			IUnit craft = g.CreateUnit(UnitType.Harvester, 30, 30, 0)!;

			g.WalkHarvester(craft);

			Assert.Equal((29, 29), ((int)craft.X, (int)craft.Y));
		}
	}
}
