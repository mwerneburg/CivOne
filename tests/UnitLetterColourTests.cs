// CivOne tests
//
// The G/M/F/R/I letters on a unit were black on black for the Russians (reported Sep 2026):
// TextSettings.UnitText kept Civ 1's exception for player 1, whose units were white. Every
// unit tile is now the cassette's dark BG0, so one light colour serves every civ.

using CivOne.Graphics;

namespace CivOne.Tests
{
	public class UnitLetterColourTests
	{
		[Fact]
		public void TheLetterIsTheLightInkForEveryCiv()
		{
			Assert.Equal((byte)15, TextSettings.UnitText().Colour);
		}

		// Pinned at the source too: the dark background it must stand out against.
		[Fact]
		public void TheTileBehindItIsTheDarkBackground()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(Sim.RepoRoot(), "src", "Graphics", "Sprites", "Unit.cs"));
			Assert.Contains("output.FillRectangle(0, 0, 16, 16, CassetteTheme.BG0);", src);
			Assert.Contains("TextSettings.UnitText()", src);
		}
	}
}
