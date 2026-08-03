// CivOne tests
//
// CalculateContinentSize numbered only the FOURTEEN largest land regions and left every
// other landmass at the "misc" id. Harmless on Civ 1's fixed 80x50 board, which has
// nothing like fourteen meaningful continents. On 320x200 it left dozens of islands
// unnumbered, and unnumbered means invisible to everything that reasons about land:
//
//   AI.Strategy:2008  land-attack targets require a named continent, so a city on a small
//                     island could never be chosen as a land objective by any AI.
//   AI.Strategy:2370  sameContinent is false when the unit's own id is misc, so a Diplomat
//                     or Caravan standing on an island could not target anything at all,
//                     including cities on its own island.
//
// Observed in a 2057 AD game as a size-11 island city sitting untouched while the mainland
// was being depopulated. ContinentId is a byte and is not persisted, so the cap is now 254.

using System.Collections.Generic;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ContinentNumberingTests
	{
		// Ocean everywhere, so the test can place exactly the landmasses it wants.
		private static void DrownTheWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				if (!Map.Instance[x, y].IsOcean)
					Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
		}

		private static void Land(int x0, int y0, int w, int h)
		{
			for (int y = y0; y < y0 + h; y++)
			for (int x = x0; x < x0 + w; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
		}

		// The regression, at its simplest: more than fourteen separate landmasses, all named.
		[Fact]
		public void MoreThanFourteenLandmasses_AreAllNumbered()
		{
			DrownTheWorld();
			// 20 islands, two rows of ten, each separated by ocean.
			for (int i = 0; i < 20; i++)
				Land(4 + (i % 10) * 7, 6 + (i / 10) * 10, 3, 3);
			Map.Instance.RecalculateContinentsIfDirty();

			var ids = new HashSet<byte>();
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
			{
				ITile t = Map.Instance[x, y];
				if (t.IsOcean) continue;
				Assert.True(Map.NamedContinent(t.ContinentId),
					$"land at ({x},{y}) left unnumbered as {t.ContinentId}");
				ids.Add(t.ContinentId);
			}
			Assert.Equal(20, ids.Count);
		}

		// A small island must get its own id, distinct from the mainland — not be lumped in,
		// and not be left misc. This is the Taiwan case.
		[Fact]
		public void ASmallIslandBesideAMainland_GetsItsOwnNamedId()
		{
			DrownTheWorld();
			Land(5, 5, 30, 30);      // mainland
			Land(45, 20, 2, 2);      // island, well clear of it
			Map.Instance.RecalculateContinentsIfDirty();

			byte mainland = Map.Instance[10, 10].ContinentId;
			byte island   = Map.Instance[45, 20].ContinentId;

			Assert.True(Map.NamedContinent(mainland));
			Assert.True(Map.NamedContinent(island), "the island must not be left in the misc bucket");
			Assert.NotEqual(mainland, island);
		}

		// Ocean is not a continent, and the misc bucket must still exist for anything past
		// the byte's capacity — the fix widens the range, it does not remove the concept.
		[Fact]
		public void OceanIsNotANamedContinent()
		{
			DrownTheWorld();
			Land(5, 5, 10, 10);
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.False(Map.NamedContinent(Map.Instance[40, 40].ContinentId));
			Assert.Equal(Map.MiscContinent, Map.Instance[40, 40].ContinentId);
		}

		// Regions are numbered by DESCENDING size, so the largest landmass is id 1. Several
		// call sites lean on ids being stable and meaningful rather than arbitrary.
		[Fact]
		public void TheLargestLandmass_IsNumberedFirst()
		{
			DrownTheWorld();
			Land(40, 20, 3, 3);      // small, painted first
			Land(5, 5, 20, 20);      // large
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.Equal(1, Map.Instance[10, 10].ContinentId);
			Assert.True(Map.Instance[40, 20].ContinentId > 1);
		}

		// The behavioural payoff: a Diplomat standing on an island can now pick a target on
		// that island. Before, its own id was misc, sameContinent was false for every
		// candidate, and the unit sat idle for the rest of the game.
		//
		// The sixteen decoy landmasses are what make this a regression test rather than a
		// tautology: they are all larger than our island, so under the old fourteen-id cap
		// our island is pushed out of the numbering and lands in the misc bucket. Without
		// them it would be the only landmass, take id 1, and pass either way.
		[Fact]
		public void ADiplomatOnAnIsland_CanTargetACityOnThatIsland()
		{
			DrownTheWorld();
			// 35 tiles each, against our island's 18 — every decoy must outrank it by size,
			// or the island keeps a low id under the old cap and the test proves nothing.
			for (int i = 0; i < 16; i++)
				Land(2 + (i % 8) * 9, 2 + (i / 8) * 8, 7, 5);
			Land(30, 20, 6, 3);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player[] ps = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player mine = ps[0], theirs = ps[1];
			mine.Explore(33, 21, range: 12);
			g.AddCity(mine, 0, 31, 21);
			City target = g.AddCity(theirs, 1, 35, 21)!;
			IUnit dip = g.CreateUnit(UnitType.Diplomat, 33, 21, g.PlayerNumber(mine))!;
			dip.MovesLeft = dip.Move;
			g.GameTurn = (ushort)((dip.X + dip.Y) & 7);
			Sim.ClearTasks();

			AI.Instance(mine).Move(dip);

			Assert.True(Map.NamedContinent(Map.Instance[33, 21].ContinentId),
				"the island itself must be numbered, or this tests nothing");
			Assert.Equal((target.X, target.Y), (dip.Goto.X, dip.Goto.Y));
		}
	}
}
