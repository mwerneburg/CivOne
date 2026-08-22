// CivOne tests
//
// Forested hills could not exist on an Earth board.
//
// They are made by CreateForestedHills, which runs only inside the procedural generator. An
// Earth game loads resources/earth_*.bin through LoadEarthBin, which never called it — and the
// file format has no code for wooded slopes either, so the board arrived with several thousand
// bare hills and no way for any of them to be wooded. Reported as "we added forested hills but
// I never see them in play"; the answer was that the terrain was unreachable on the only maps
// being played, not that it was missing artwork.
//
// The epic Earth binary holds 5,233 hills, and its whole byte histogram is ocean/hills/grass/
// forest/mountains/tundra/plains/desert/jungle/river/swamp/arctic — nothing else to decode.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class EarthForestedHillsTests
	{
		private static int Count<T>() where T : ITile =>
			Map.Instance.AllTiles().Count(t => t is T);

		// The report, stated directly.
		[Fact]
		public void TheEpicEarthHasWoodedSlopes()
		{
			Sim.NewGame(competition: 4, map: "earth-epic");

			Assert.True(Count<ForestedHills>() > 0, "not one hill on Earth is wooded");
		}

		// ...and the standard board too, which is the other one a game can start on.
		[Fact]
		public void TheStandardEarthHasWoodedSlopesToo()
		{
			Sim.NewGame(competition: 4, map: "earth-standard");

			Assert.True(Count<ForestedHills>() > 0, "not one hill on Earth is wooded");
		}

		// Wooded, not carpeted. The rule takes hills standing in forest and then skips a
		// diagonal band, so some hills that COULD be wooded deliberately are not — otherwise
		// every treeline would be ringed by an unbroken band of wooded slope, which reads as a
		// pattern and quietly rewrites the shield economy of every range on the map.
		//
		// Asserting "more bare than wooded" was not enough: the adjacency filter alone leaves
		// bare hills in the majority, so that version passed with the band deleted. The
		// property is that an ELIGIBLE hill can still be bare.
		[Fact]
		public void SomeHillsInTheTreelineStayBare()
		{
			Sim.NewGame(competition: 4, map: "earth-epic");

			bool NextToWoods(ITile t)
			{
				for (int dy = -1; dy <= 1; dy++)
				for (int dx = -1; dx <= 1; dx++)
				{
					if (dx == 0 && dy == 0) continue;
					ITile n = Map.Instance[(t.X + dx + Map.WIDTH) % Map.WIDTH, t.Y + dy];
					if (n is Forest || n is Jungle) return true;
				}
				return false;
			}

			int eligibleAndBare = Map.Instance.AllTiles().Count(t => t is Hills && NextToWoods(t));
			Assert.True(eligibleAndBare > 0,
				"every hill standing in woods was converted — the treeline has carpeted over");
			Assert.True(Count<ForestedHills>() > 0, "and none were converted at all");
		}

		// Deterministic: the rule makes no roll, so two loads of the same board agree. Without
		// this a sweep on Earth would vary terrain between runs, which is the confound the
		// fixed-map design exists to remove.
		[Fact]
		public void TheSameBoardWoodsTheSameHills()
		{
			Sim.NewGame(competition: 4, map: "earth-epic", seed: 111);
			var first = Map.Instance.AllTiles().Where(t => t is ForestedHills)
				.Select(t => (t.X, t.Y)).OrderBy(p => p).ToArray();

			Sim.NewGame(competition: 4, map: "earth-epic", seed: 999);
			var second = Map.Instance.AllTiles().Where(t => t is ForestedHills)
				.Select(t => (t.X, t.Y)).OrderBy(p => p).ToArray();

			Assert.Equal(first, second);
		}

		// The MAP.PIC export has no code for either of the terrains this game added, and its
		// default is OCEAN — so a salt flat was being written out as sea, drowning land the
		// same way a wooded hill would have before it was degraded to a bare one.
		[Fact]
		public void TheExportNeverDrownsLand()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(Sim.RepoRoot(), "src", "Map.LoadSave.cs"));
			int at = src.IndexOf("// Save terrainlayer");
			Assert.True(at > 0, "the terrain export has moved");
			string block = src.Substring(at, src.IndexOf("// Save improvement layer", at) - at);

			Assert.Contains("case Terrain.ForestedHills:", block);
			Assert.Contains("case Terrain.SaltFlat:", block);
		}
	}
}
