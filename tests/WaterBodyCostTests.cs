// CivOne tests
//
// NumberWaterBodies asked each tile "is there a city here?" via ITile.City, which is
// Game.GetCity(x,y) — a linear LINQ scan over every city in the world. Evaluated once per
// tile and again for each of its eight neighbours, that is O(map x cities x 9): roughly
// 244 million operations at 64,000 tiles and 424 cities. It runs on every coastal city
// founded or destroyed, so a late-game war pinned a turn at 100% CPU for minutes.
//
// The fill is now O(cities + map). This is a cost test rather than a behaviour one, so it
// asserts a generous bound — enough to catch a return to the quadratic form, loose enough
// not to flake on a slow machine.

using System.Diagnostics;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class WaterBodyCostTests
	{
		[Fact]
		public void TheWaterFill_DoesNotScaleWithCityCount()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);

			// Land band across the middle, ocean above and below, so cities are coastal and
			// the fill has real work to do.
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				Map.Instance.ChangeTileType(x, y, (y >= 22 && y <= 27) ? Terrain.Grassland1 : Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			// Repeat the fill: one pass is sub-millisecond on an 80x50 board, which is pure
			// timer noise. The RATIO of crowded-to-empty is the signal — it is ~1.0 when the
			// fill is O(cities + map) and grows with city count when it is not. Measured at
			// 1.07 fixed against 1.90 quadratic here; on the real 320x200 board with 424
			// cities the quadratic form cost minutes per call, so this small map understates
			// it considerably.
			// 20 reps came in at roughly 11 ms empty, close enough to timer noise that a busy
			// machine could push the ratio over the bar on correct code: it cried wolf twice in
			// one day at 1.57 and 1.67 against a 1.5 threshold, while passing 3/3 in isolation
			// each time. A false alarm on a cost test is expensive — it teaches the reader to
			// discount a red suite, which is how a real regression walks through.
			//
			// More reps, not a looser bound: the signal is 1.07 fixed against 1.90 quadratic,
			// so 1.5 discriminates perfectly well and the problem was purely the sample size.
			// Averaging over 5x the work shrinks the noise instead of widening the target.
			const int Reps = 100;
			double Time()
			{
				var sw = new Stopwatch();
				sw.Restart();
				for (int i = 0; i < Reps; i++) Map.Instance.RecalculateWaterBodies();
				return sw.Elapsed.TotalMilliseconds;
			}

			Time();                        // warm
			double bare = Time();

			int made = 0;
			for (int x = 1; x < Map.WIDTH - 1 && made < 200; x += 1)
			for (int y = 23; y <= 26 && made < 200; y += 1)
			{
				p.Explore(x, y, range: 2);
				if (g.AddCity(p, made % 250, x, y) is not null) made++;
			}
			Assert.True(made > 100, $"needed a crowded world, only placed {made}");

			double crowded = Time();

			Assert.True(crowded < bare * 1.5,
				$"water fill scaled with city count: {bare:F2}ms empty -> {crowded:F2}ms with " +
				$"{made} cities (ratio {crowded / bare:F2}); it should not depend on ITile.City");
		}

		// The fill must still be correct with cities present — a coastal city is sailable,
		// so it belongs to the water body it touches.
		[Fact]
		public void ACoastalCityTileIsPartOfItsWaterBody()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				Map.Instance.ChangeTileType(x, y, (y >= 22 && y <= 27) ? Terrain.Grassland1 : Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			p.Explore(40, 22, range: 3);
			City port = g.AddCity(p, 0, 40, 22)!;

			Assert.Equal(Map.Instance[40, 21].OceanId, Map.Instance[port.X, port.Y].OceanId);
		}
	
		// The gap in the tests above: they all run on 80x50. The real board is 320x200 —
		// 16x the tiles — and that is where the stalls appeared.
		[Fact(Skip = "sizing probe; remove -Skip to run")]
		public void MeasureAtRealMapSize()
		{
			Sim.NewGame(width: 320, height: 200);
			var sw = new Stopwatch();

			sw.Restart();
			for (int i = 0; i < 10; i++) Map.Instance.RecalculateWaterBodies();
			double water = sw.Elapsed.TotalMilliseconds / 10;

			sw.Restart();
			for (int i = 0; i < 10; i++) Map.Instance.CalculateContinentSize();
			double both = sw.Elapsed.TotalMilliseconds / 10;

			// Measured 2026-08-04: NumberWaterBodies 10.1ms, CalculateContinentSize 25.3ms.
			// Kept so the next person does not have to wonder whether the real board differs.
			Assert.True(water < 200 && both < 400, $"NumberWaterBodies={water:F1}ms CalculateContinentSize={both:F1}ms");
		}
	}
}
