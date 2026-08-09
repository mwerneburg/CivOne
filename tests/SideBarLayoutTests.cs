// CivOne tests
//
// The sidebar's demographics panel is 80px wide and shares its year line between the
// date and the lamp (left) and the score (right). The score was first put on the
// POPULATION line, where this test caught it overlapping by 2px at 500,000,000 people
// and a four-digit score — reachable in a 750-turn game, and exactly the point at which
// nobody wants to discover a layout bug. The year is a fixed seven characters, so the
// slack there does not shrink as the game runs.

using CivOne;
using CivOne.Graphics;

namespace CivOne.Tests
{
	public class SideBarLayoutTests
	{
		// SideBar.DrawDemographics: year left-aligned at x=2 with the research lamp laid in
		// at 4 + yearWidth, score right-aligned at x=77, both font 0.
		private const int LeftX = 2, RightX = 77, LampWidth = 12;

		[Theory]
		[InlineData("3980 BC", 0)]        // turn 1
		[InlineData("1350 AD", 2075)]     // the Malian save this was asked for
		[InlineData("2100 AD", 9999)]     // last turn, and a score no real game reaches
		public void TheYearAndTheScoreShareTheirLineWithoutColliding(string year, int score)
		{
			Sim.EnsureRuntime();

			int yearRight = 4 + Resources.Instance.GetTextSize(0, year).Width + LampWidth;
			int scoreLeft = RightX - Resources.Instance.GetTextSize(0, $"{score}").Width;

			Assert.True(yearRight < scoreLeft,
				$"year \"{year}\" and lamp end at {yearRight}, score {score} starts at {scoreLeft}");
		}

		// ...and the population line it was moved OFF still has room for the widest
		// population on its own, which is what the panel was designed for.
		[Fact]
		public void ThePopulationLineStillFitsTheWidestPopulation()
		{
			Sim.EnsureRuntime();
			string popText = $"{Common.NumberSeperator(500_000_000)}#";

			Assert.True(LeftX + Resources.Instance.GetTextSize(0, popText).Width <= 80,
				$"\"{popText}\" overruns the 80px panel");
		}
	}
}
