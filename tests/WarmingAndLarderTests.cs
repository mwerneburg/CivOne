// CivOne tests
//
// Two rules that make the Scavenger draw answer to how the planet has been treated, and one
// that makes everyone pay for the treatment.
//
// The water half of LarderScore used to read absolute wetness against a fixed 0.60 constant.
// Every generated map is wetter than that, so the term was clamped at 1.000 in every game
// ever played: half the larder was a constant, the draw could never fall below 25%, and the
// comment promising that "a previous harvest — or a run of global warming — changes the odds"
// described something the code could not do. It now reads today's sea against the world's own
// starting sea, so flooding raises it and extraction lowers it.
//
// On top of that, a world that has never warmed is held at the floor: the Scavengers notice
// civilizations that have already changed their own climate.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class WarmingAndLarderTests
	{
		private static int OceanCount() =>
			Map.Instance.AllTiles().Count(t => t is not null && t.IsOcean);

		// Drown `count` land tiles, the way the sea-level pass does.
		private static void Flood(int count)
		{
			int done = 0;
			for (int y = 0; y < Map.HEIGHT && done < count; y++)
			for (int x = 0; x < Map.WIDTH && done < count; x++)
			{
				if (Map.Instance[x, y] is null || Map.Instance[x, y].IsOcean) continue;
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
				done++;
			}
			Assert.Equal(count, done);
		}

		// The baseline is taken at game creation, before anything can move it.
		[Fact]
		public void TheOriginalOceanCountIsCapturedAtCreation()
		{
			Sim.NewGame(width: 80, height: 50);

			Assert.Equal(OceanCount(), Game.Instance.OriginalOceanTiles);
		}

		// The defect, stated directly: flooding a world must make it a richer target. Under the
		// old fixed-0.60 term this was impossible — the water half was already clamped at 1.0,
		// so the larder could not rise no matter how much coast went under.
		[Fact]
		public void FloodingRaisesTheLarder()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			double before = g.LarderScore();

			Flood(OceanCount() / 20);   // +5% sea, half the water half's range

			Assert.True(g.LarderScore() > before + 0.05,
				$"larder {before:F3} -> {g.LarderScore():F3}; flooding changed almost nothing");
		}

		// ...and lifting the water away makes it poorer, which is what the Scavengers' own
		// extraction does to a second visit.
		[Fact]
		public void DrainingLowersTheLarder()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			double before = g.LarderScore();

			int drain = OceanCount() / 20;
			int done = 0;
			for (int y = 0; y < Map.HEIGHT && done < drain; y++)
			for (int x = 0; x < Map.WIDTH && done < drain; x++)
			{
				if (Map.Instance[x, y] is null || !Map.Instance[x, y].IsOcean) continue;
				Map.Instance.ChangeTileType(x, y, Terrain.Desert);
				done++;
			}

			Assert.True(g.LarderScore() < before - 0.05,
				$"larder {before:F3} -> {g.LarderScore():F3}; draining changed almost nothing");
		}

		// An old save has no baseline; falling back to today's sea reads as an unchanged world
		// rather than a drained one, which would otherwise hand every pre-existing game the
		// lowest possible water term.
		[Fact]
		public void AnUnknownBaselineReadsAsUnchanged()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			double captured = g.LarderScore();

			g.OriginalOceanTiles = 0;   // as a save written before the field existed

			Assert.Equal(captured, g.LarderScore(), 3);
		}

		// Every civilization pays, and the fifth incident costs more than the first.
		[Fact]
		public void EveryCivilizationLosesPointsPerWarmingIncident()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player[] civs = Enumerable.Range(1, 4).Select(i => g.GetPlayer((byte)i)).ToArray();
			foreach (Player p in civs) p.Government = new Monarchy();

			int[] before = civs.Select(p => p.MilestoneScore).ToArray();

			g.GlobalWarmingCount = 1;
			InvokePenalty(g);
			int[] afterFirst = civs.Select(p => p.MilestoneScore).ToArray();

			g.GlobalWarmingCount = 5;
			InvokePenalty(g);
			int[] afterFifth = civs.Select(p => p.MilestoneScore).ToArray();

			for (int i = 0; i < civs.Length; i++)
			{
				int first = before[i] - afterFirst[i];
				int fifth = afterFirst[i] - afterFifth[i];
				Assert.True(first > 0, $"civ {i + 1} paid nothing for the first incident");
				Assert.True(fifth > first, $"civ {i + 1}: fifth cost {fifth}, first cost {first}");
			}
		}

		// The gate itself. The draw is random, so this measures frequency rather than a single
		// outcome — but 8% against ~37% is a wide enough gap that 300 trials separate them
		// without being flaky. Both arms use the same world, so only the warming differs.
		[Fact]
		public void AWorldThatNeverWarmedIsRarelyWorthTheTrip()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;

			int cool = ScavengerDrawsIn(g, warming: 0, trials: 300);
			int cooked = ScavengerDrawsIn(g, warming: 4, trials: 300);

			Assert.True(cool < 60, $"never-warmed world drew Scavengers {cool}/300");
			Assert.True(cooked > cool * 2, $"warmed {cooked}/300 vs cool {cool}/300 — the gate does nothing");
		}

		private static int ScavengerDrawsIn(Game g, int warming, int trials)
		{
			var draw = typeof(Game).GetMethod("SelectVisitorArchetype",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			g.GlobalWarmingCount = (ushort)warming;
			int count = 0;
			for (int i = 0; i < trials; i++)
				if ((VisitorArchetype)draw.Invoke(g, null)! == VisitorArchetype.Scavengers) count++;
			return count;
		}

		private static void InvokePenalty(Game g) =>
			typeof(Game).GetMethod("ApplyWarmingScorePenalty",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(g, null);
	}
}
