// CivOne tests
//
// The cost half of [[CityRosterCacheTests]]. Player.Cities is read from 109 sites, 70 of them
// in the AI and several per unit move, so its per-access cost is multiplied by every unit in
// the world every turn. Uncached it was O(cities) with two array allocations and a
// player-table lookup per city; cached it is a field read between world changes.
//
// This is a cost test, so it asserts a RATIO rather than a time. A single access is far below
// timer resolution, and absolute thresholds flake on a loaded machine — but the shape of the
// curve is the thing that regressed, and the shape is machine-independent: reading the roster
// N times must not cost N times more when the world is eight times larger.

using System.Diagnostics;
using System.Linq;
using CivOne;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CityRosterCostTests
	{
		private static Player AWorldWith(int cityCount)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			for (int i = 0; i < cityCount; i++)
			{
				int x = 4 + (i % 34) * 2, y = 6 + (i / 34) * 3;
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				p.Explore(x, y, range: 2);
				City c = g.AddCity(p, i, x, y);
				if (c is not null) c.Size = 3;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			return p;
		}

		private static double CostOfReads(Player p, int reads)
		{
			// Warm the cache so the measurement is the steady-state read, which is what the AI
			// actually does — the first access after a world change legitimately rebuilds.
			_ = p.Cities;
			var sw = Stopwatch.StartNew();
			int sink = 0;
			for (int i = 0; i < reads; i++) sink += p.Cities.Length;
			sw.Stop();
			Assert.True(sink >= 0);
			return sw.Elapsed.TotalMilliseconds;
		}

		// The regression, stated as a shape: eight times the cities must not mean eight times
		// the cost to read the roster. Uncached this ratio tracks the city count; cached it
		// sits near 1.0.
		[Fact]
		public void ReadingTheRosterDoesNotScaleWithCityCount()
		{
			const int Reads = 40000;

			Player few = AWorldWith(8);
			double smallWorld = CostOfReads(few, Reads);

			Player many = AWorldWith(64);
			double bigWorld = CostOfReads(many, Reads);

			Assert.Equal(64, many.Cities.Length);   // the big world must really be big

			// Generous: the cached form measures ~1.0, the uncached one ~8x. Anything under 3
			// means the per-access cost is not tracking the world size.
			double ratio = bigWorld / System.Math.Max(0.001, smallWorld);
			Assert.True(ratio < 3.0,
				$"roster reads scale with city count (8 cities {smallWorld:0.0}ms, "
				+ $"64 cities {bigWorld:0.0}ms, ratio {ratio:0.00})");
		}

		// The cache must not survive a world change — the cheap read is only correct because
		// founding a city rebuilds it. Guards the obvious wrong "fix" of caching forever.
		[Fact]
		public void AWorldChangeStillCostsARebuild()
		{
			Player p = AWorldWith(32);
			City[] before = p.Cities;

			Map.Instance.ChangeTileType(70, 40, Terrain.Grassland1);
			p.Explore(70, 40, range: 2);
			Game.Instance.AddCity(p, 999, 70, 40);

			Assert.NotSame(before, p.Cities);
			Assert.Equal(before.Length + 1, p.Cities.Length);
		}
	}
}
