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

		// A ContinentId is a REACHABILITY claim: GotoStepInner short-circuits to "no path" when
		// the two ends carry different named ids. So the flood fill has to agree with how units
		// move, and units move to all eight neighbours.
		//
		// With a 4-connected fill, two landmasses meeting at a corner got different ids while a
		// land unit could walk straight across the diagonal — the planner refused a legal route.
		// Latent while small islands sat in the misc bucket (the short-circuit needs BOTH ends
		// named) and exposed the moment every island got a real id.
		[Fact]
		public void LandmassesTouchingDiagonally_AreOneContinent()
		{
			DrownTheWorld();
			Land(10, 10, 5, 5);      // x10-14, y10-14
			Land(15, 15, 5, 5);      // x15-19, y15-19 — meets the first only at (14,14)/(15,15)
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.Equal(Map.Instance[12, 12].ContinentId, Map.Instance[17, 17].ContinentId);
		}

		// ...and the planner must actually route across that corner, which is the behaviour the
		// id was standing in for.
		[Fact]
		public void AUnitCanPathAcrossADiagonalLandBridge()
		{
			DrownTheWorld();
			Land(10, 10, 5, 5);
			Land(15, 15, 5, 5);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			p.Explore(15, 15, range: 15);
			IUnit u = g.CreateUnit(UnitType.Militia, 12, 12, g.PlayerNumber(p))!;

			Assert.NotNull(Common.GotoStep(u, 17, 17));
		}

		// The other half: a genuine sea gap must still separate them, or the fix has gone too
		// far and every island in a cluster becomes one continent.
		[Fact]
		public void LandmassesSeparatedByOcean_StayDistinct()
		{
			DrownTheWorld();
			Land(10, 10, 5, 5);      // ends at x14
			Land(17, 15, 5, 5);      // starts at x17 — a clear tile of ocean between them
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.NotEqual(Map.Instance[12, 12].ContinentId, Map.Instance[19, 17].ContinentId);
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
			// IdleRetryTurn defers most idle units to their own turn in eight; ask which one.
			for (ushort t = 0; t < 8; t++)
			{
				g.GameTurn = t;
				if (!AI.Instance(mine).TestIdleRetryDeferred(dip)) break;
			}
			Sim.ClearTasks();

			AI.Instance(mine).Move(dip);

			Assert.True(Map.NamedContinent(Map.Instance[33, 21].ContinentId),
				"the island itself must be numbered, or this tests nothing");
			Assert.Equal((target.X, target.Y), (dip.Goto.X, dip.Goto.Y));
		}

		// ── the AI consumers (migrated 2026-08-05) ───────────────────────────
		//
		// The generator was raised to 254 but four AI sites kept testing `id >= 1 && id <= 14`,
		// with one expression left half-migrated: the old filter four lines above a call to
		// Map.NamedContinent. That combination is worse than the original bug. Before, every
		// island really did share the misc id, so "cannot tell, so allow" was honest. After,
		// two different islands hold DISTINCT ids — and the old test called both "unknown",
		// discarded that, and allowed a land march across open water.

		// An archipelago of twenty, so the smaller islands are numbered ABOVE 14. That is the
		// whole point: with only a handful of landmasses every id lands inside the old 1-14
		// window and the stale test gives the right answer by luck. The bug needs high ids.
		private static (int x, int y)[] AnArchipelago()
		{
			DrownTheWorld();
			var seeds = new List<(int x, int y)>();
			for (int i = 0; i < 20; i++)
			{
				int x = 4 + (i % 10) * 7, y = 6 + (i / 10) * 10;
				Land(x, y, 3, 3);
				seeds.Add((x + 1, y + 1));
			}
			Map.Instance.RecalculateContinentsIfDirty();
			return seeds.ToArray();
		}

		// Two islands the generator numbered separately are not the same ground.
		[Fact]
		public void TwoSeparatelyNumberedIslandsAreNotWalkableBetween()
		{
			var seeds = AnArchipelago();
			// Two islands BOTH numbered above the old cap — the case that used to fall through
			// to "both unknown, cannot tell, so allow".
			var high = seeds.Where(s => Map.Instance[s.x, s.y].ContinentId > 14).Take(2).ToArray();
			Assert.True(high.Length == 2, "need two islands numbered above the old 14 cap");

			Game g = Game.Instance;
			IUnit unit = g.CreateUnit(UnitType.Militia, high[0].x, high[0].y, 1)!;
			ITile there = Map.Instance[high[1].x, high[1].y];

			Assert.NotEqual(Map.Instance[high[0].x, high[0].y].ContinentId, there.ContinentId);
			Assert.False(AI.LandReachable(unit, there),
				"distinct landmasses, whatever their id numbers");
		}

		// The other half: a high-numbered island stays walkable within itself. Without this the
		// "fix" could just refuse everything and still pass the test above.
		[Fact]
		public void TheSameHighNumberedIslandStaysWalkable()
		{
			var seeds = AnArchipelago();
			var high = seeds.First(s => Map.Instance[s.x, s.y].ContinentId > 14);

			Game g = Game.Instance;
			IUnit unit = g.CreateUnit(UnitType.Militia, high.x, high.y, 1)!;
			Assert.True(AI.LandReachable(unit, Map.Instance[high.x + 1, high.y]),
				"same landmass, one walk");
		}

		// A high-numbered island against one of the fourteen largest. This case gave the right
		// answer under the old test too, but by accident — the mismatch branch happened to agree.
		[Fact]
		public void AnIslandAndAMainlandAreNotWalkableBetween()
		{
			var seeds = AnArchipelago();
			Land(40, 30, 20, 15);                      // a mainland, comfortably the largest
			Map.Instance.RecalculateContinentsIfDirty();
			var high = seeds.First(s => Map.Instance[s.x, s.y].ContinentId > 14);

			Game g = Game.Instance;
			IUnit unit = g.CreateUnit(UnitType.Militia, 45, 35, 1)!;
			Assert.True(Map.Instance[45, 35].ContinentId <= 14, "the mainland should rank low");
			Assert.False(AI.LandReachable(unit, Map.Instance[high.x, high.y]));
		}

		// The genuinely unknowable case must still permit: unnumbered ground says nothing
		// either way, and refusing there would strand units that can in fact walk.
		[Fact]
		public void UnnumberedGroundIsStillPermitted()
		{
			DrownTheWorld();
			Land(10, 20, 5, 3);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			IUnit unit = g.CreateUnit(UnitType.Militia, 10, 21, 1)!;
			Map.Instance[10, 21].ContinentId = Map.MiscContinent;
			Map.Instance[14, 21].ContinentId = Map.MiscContinent;

			Assert.True(AI.LandReachable(unit, Map.Instance[14, 21]));
		}

		// The predicate itself, since five call sites now lean on it. 15 is the load-bearing
		// value: it was the old sentinel and is now an ordinary island.
		[Fact]
		public void NamedContinentSpansTheWholeRange()
		{
			Assert.False(Map.NamedContinent(0), "0 is not a landmass");
			Assert.False(Map.NamedContinent(Map.MiscContinent), "the misc bucket is not a landmass");
			Assert.True(Map.NamedContinent(1));
			Assert.True(Map.NamedContinent(14));
			Assert.True(Map.NamedContinent(15), "15 was the old sentinel; it is now a real island");
			Assert.True(Map.NamedContinent(254), "the last numberable region");
		}
	}
}
