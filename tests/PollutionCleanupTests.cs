// CivOne tests
//
// Two defects in one place, found from opposite directions on the same afternoon.
//
// CORRECTNESS: the cleanup-enrolment gate read Player.Pollution — current EMISSIONS
// (Player.cs sums SmokeStacks) — not smog on the ground. A civ that fitted Mass
// Transit and Recycling Centres dropped to zero emissions and stopped cleaning its
// land entirely. Observed in a 2086 AD game: Moscow producing 0 tonnes, ringed by
// polluted tiles nobody would ever be sent to.
//
// PERFORMANCE: the backlog behind that gate was Map.AllTiles() cross-joined against
// every city, evaluated per settler per turn — ~2 million distance calls. The
// move_split probe put settler moves at 77 ms with the site scans accounting for
// almost none of it.
//
// Both fixed by the same observation: pollution only ever lands inside a city's
// working radius, so the candidate set is a small box around each city, never the
// map. The rewrite must return IDENTICAL answers — that is what these check.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class PollutionCleanupTests
	{
		private static (Player mine, Player theirs) TwoCivsOnGrass()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = Game.Instance.Players
				.Where(p => p is not null && Game.Instance.PlayerNumber(p) != 0).ToArray();
			Player mine = ps[0], theirs = ps[1];

			mine.Explore(30, 25, range: 10);
			Game.Instance.AddCity(mine, 0, 30, 25);
			Game.Instance.AddCity(mine, 1, 36, 25);
			theirs.Explore(52, 25, range: 8);
			Game.Instance.AddCity(theirs, 2, 52, 25);
			Sim.ClearTasks();
			return (mine, theirs);
		}

		// The old predicate, kept verbatim as the oracle the rewrite must match.
		private static int BacklogByFullMapScan(Player p) =>
			Map.Instance.AllTiles().Count(t => t.Pollution
				&& p.Cities.Any(c => Common.DistanceToTile(c.X, c.Y, t.X, t.Y) <= 3));

		// Identical answers, on ground chosen to hit every edge: inside one radius,
		// inside BOTH (the dedupe case), just outside, and near a foreign city.
		[Fact]
		public void TheCheapBacklog_MatchesTheFullMapScan()
		{
			var (mine, _) = TwoCivsOnGrass();
			(int x, int y)[] spots =
			{
				(30, 24),   // inside city A
				(33, 25),   // inside BOTH A and B — must be counted once
				(36, 27),   // inside city B
				(30, 30),   // 5 away from A, outside every radius
				(52, 25 + 1), // beside the FOREIGN city, so not ours to clean
				(45, 25),   // no man's land
			};
			foreach (var (x, y) in spots) Map.Instance[x, y].Pollution = true;

			Assert.Equal(BacklogByFullMapScan(mine), AI.Instance(mine).PollutionBacklog());
		}

		// A clean world counts zero both ways — the gate depends on it.
		[Fact]
		public void ACleanWorld_CountsZeroBothWays()
		{
			var (mine, _) = TwoCivsOnGrass();
			Assert.Equal(0, BacklogByFullMapScan(mine));
			Assert.Equal(0, AI.Instance(mine).PollutionBacklog());
		}

		// Overlapping city radii must not double-count: one tile, one unit of backlog.
		[Fact]
		public void OverlappingRadii_CountATileOnce()
		{
			var (mine, _) = TwoCivsOnGrass();
			Map.Instance[33, 25].Pollution = true;   // within 3 of both our cities

			Assert.Equal(1, AI.Instance(mine).PollutionBacklog());
		}

		// The correctness fix. Emissions zero, ground filthy: a cleaner must still be
		// enrolled. This is Moscow.
		[Fact]
		public void WithZeroEmissionsButFilthyGround_ACleanerIsStillEnrolled()
		{
			var (mine, _) = TwoCivsOnGrass();
			Assert.Equal(0, mine.Pollution);          // no city is emitting anything
			Map.Instance[30, 24].Pollution = true;
			Map.Instance[31, 26].Pollution = true;

			IUnit s = Game.Instance.CreateUnit(UnitType.Settlers, 30, 26,
				Game.Instance.PlayerNumber(mine))!;
			Sim.ClearTasks();

			AI.Instance(mine).Move(s);

			Assert.True(((Settlers)s).AutoClean,
				"a civ with clean cities and dirty land should still send someone out");
		}

		// ...and the bound still holds: no pollution, nobody enrolled. Without this the
		// test above would pass on a rule that simply enrolled everyone always.
		[Fact]
		public void WithNothingToClean_NobodyIsEnrolled()
		{
			var (mine, _) = TwoCivsOnGrass();
			IUnit s = Game.Instance.CreateUnit(UnitType.Settlers, 30, 26,
				Game.Instance.PlayerNumber(mine))!;
			Sim.ClearTasks();

			AI.Instance(mine).Move(s);

			Assert.False(((Settlers)s).AutoClean);
		}

		// The cache is per turn, not forever — a tile cleaned this turn must still be
		// reflected next turn, or the crew stands down while work remains.
		[Fact]
		public void TheBacklogCache_IsPerTurnOnly()
		{
			var (mine, _) = TwoCivsOnGrass();
			AI ai = AI.Instance(mine);
			Map.Instance[30, 24].Pollution = true;
			Assert.Equal(1, ai.PollutionBacklog());

			// Same turn: cached, so the new tile is deliberately not seen yet.
			Map.Instance[31, 26].Pollution = true;
			Assert.Equal(1, ai.PollutionBacklog());

			Game.Instance.GameTurn++;
			Assert.Equal(2, ai.PollutionBacklog());
		}
	}
}
