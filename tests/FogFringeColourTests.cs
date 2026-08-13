// CivOne tests
//
// The fog fringe is generated in code, not loaded from an asset — Resources has a _fog
// dictionary but nothing ever writes to it, and MapTile.GetFog calls Free.Instance.Fog()
// unconditionally. So there is no PNG to eyeball, and a wrong colour here is only visible by
// playing.
//
// It must be drawn in the SAME ink the map panel is cleared to (GameMap.Clear(5)), because
// its whole job is to dither the edge of explored ground into the unexplored fill. It was
// stippled with palette 28-31 — neutral greys of the same lightness as index 5's warm brown
// — which read as a black rim around the unknown instead of a fade into it.

using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Tests
{
	public class FogFringeColourTests
	{
		// Index 5 is the map panel's clear colour; 0 is the transparency key.
		private const byte UnexploredInk = 5;

		[Theory]
		[InlineData(Direction.North)]
		[InlineData(Direction.South)]
		[InlineData(Direction.East)]
		[InlineData(Direction.West)]
		public void TheFringeIsDrawnInTheUnexploredInk(Direction direction)
		{
			Sim.EnsureRuntime();

			byte[] used = Free.Instance.Fog(direction).ToByteArray().Distinct().OrderBy(b => b).ToArray();

			Assert.All(used, b => Assert.True(b == 0 || b == UnexploredInk,
				$"fringe uses palette index {b}; only 0 (transparent) and {UnexploredInk} belong"));
		}

		// The fade still has to be a fade: an entirely transparent fringe is no fringe, and an
		// entirely opaque one is a wall. Both halves must appear.
		//
		// Only the 3-pixel strip counts. The sprite is 16x16 with the noise laid into one edge
		// band, so the other 13 rows are untouched zeros — asserting "contains 0" over the
		// whole bitmap is true no matter what the fringe does, and a fully opaque fringe
		// passed that version of this test.
		[Fact]
		public void TheFringeStillDithers()
		{
			Sim.EnsureRuntime();

			byte[] strip = Free.Instance.Fog(Direction.North).ToByteArray().Take(3 * 16).ToArray();

			Assert.Contains((byte)0, strip);
			Assert.Contains(UnexploredInk, strip);
		}
	}
}
