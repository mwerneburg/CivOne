// CivOne tests
//
// The Olvir settlement cluster and river placer gold are both three amber blobs on a 16x16
// tile, drawn from the same PHOS palette family. At tile scale they were indistinguishable:
// a valley of 157 settlement clusters was reported as a map covered in gold, against 65 real
// gold rivers in the same save.
//
// Colour cannot separate them without giving up the Olvir amber, so the separation is by
// silhouette — the domes sit ON the ground and carry a dark base line, the nuggets sit IN it
// and carry a full outline. This pins that, because a sprite regression is invisible until
// someone misreads a map.

using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.Sprites;
using CivOne.IO;

namespace CivOne.Tests
{
	public class OlvirSpriteTests
	{
		public OlvirSpriteTests() => Sim.EnsureRuntime();

		private static Bytemap Cluster() => OlvirSprites.Get(OlvirImprovementType.SettlementCluster).Bitmap;

		// Each dome stands on a dark line. Checked per dome rather than "contains any BG0",
		// which would pass on a single stray pixel anywhere in the tile.
		[Theory]
		[InlineData(3, 5, 3)]    // left dome, three pixels wide
		[InlineData(10, 5, 3)]   // right dome
		[InlineData(6, 11, 4)]   // bottom dome, four wide
		public void EachDomeStandsOnAGroundLine(int x0, int y, int width)
		{
			Bytemap b = Cluster();

			for (int x = x0; x < x0 + width; x++)
				Assert.True(b[x, y] == CassetteTheme.BG0,
					$"expected a dark ground pixel at ({x},{y}), found index {b[x, y]}");
		}

		// The shadow must sit UNDER the dome it belongs to — a ground line floating in empty
		// tile would satisfy the test above while looking like nothing at all.
		[Theory]
		[InlineData(4, 4, 5)]    // left dome body at y=4, shadow at y=5
		[InlineData(11, 4, 5)]
		[InlineData(7, 10, 11)]  // bottom dome body at y=10, shadow at y=11
		public void TheGroundLineSitsDirectlyBelowTheDome(int x, int bodyY, int shadowY)
		{
			Bytemap b = Cluster();

			Assert.True(b[x, bodyY] != 0, $"no dome body at ({x},{bodyY})");
			Assert.Equal(CassetteTheme.BG0, b[x, shadowY]);
		}

		// The point of the exercise: the two sprites must not paint the same pixels. A cheap
		// but real check — they may not share an identical set of occupied positions.
		[Fact]
		public void TheClusterDoesNotMatchThePlacerGoldSilhouette()
		{
			Bytemap cluster = Cluster();
			Bytemap gold = Free.Instance.Special(Terrain.River);

			bool identical = true;
			for (int y = 0; y < 16 && identical; y++)
			for (int x = 0; x < 16; x++)
				if ((cluster[x, y] != 0) != (gold[x, y] != 0)) { identical = false; break; }

			Assert.False(identical, "settlement cluster and placer gold occupy the same pixels");
		}

		// And they differ by more than a pixel or two, since "not identical" is a low bar for
		// two things that must be told apart at a glance.
		[Fact]
		public void TheTwoSpritesDifferSubstantially()
		{
			Bytemap cluster = Cluster();
			Bytemap gold = Free.Instance.Special(Terrain.River);

			int differing = 0;
			for (int y = 0; y < 16; y++)
			for (int x = 0; x < 16; x++)
				if ((cluster[x, y] != 0) != (gold[x, y] != 0)) differing++;

			Assert.True(differing >= 20,
				$"only {differing} pixels distinguish the two sprites; they will read the same on a map");
		}
	}
}
