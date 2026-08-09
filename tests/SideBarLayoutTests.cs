// CivOne tests
//
// The sidebar is three stacked panels in a fixed 80x192 column, and their heights are three
// separate constants that must agree: the minimap, the demographics box, and the game-info
// panel below it. Growing the demographics box by a line to give the score its own row means
// shrinking game-info by the same 8px and moving its layer offset — miss either and the
// panels overlap or leave a gap, which no compiler catches.
//
// The score's earlier home was the YEAR line, right-aligned; this file caught it colliding
// with a 500,000,000 population when it was tried on the population line before that.

using System.Reflection;
using CivOne;
using CivOne.Graphics;
using CivOne.Screens.GamePlayPanels;

namespace CivOne.Tests
{
	public class SideBarLayoutTests
	{
		// DrawDemographics: score drawn left-aligned at x=2, baseline y=39, inside a panel
		// 47 tall whose bottom border row is y=46.
		private const int ScoreX = 2, ScoreY = 39, PanelHeight = 47, SideBarHeight = 192;

		private static Picture PanelOf(SideBar bar, string field)
			=> (Picture)typeof(SideBar).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bar)!;

		// The three panels tile the column exactly — no overlap, no dead strip.
		[Fact]
		public void ThePanelsFillTheSideBarExactly()
		{
			// A game, not just the runtime: the constructor draws the panels, and
			// DrawDemographics reads the human player.
			Sim.NewGame(width: 80, height: 50);
			using (Palette palette = CassetteTheme.CreatePalette())
			{
				SideBar bar = new SideBar(palette);

				int total = PanelOf(bar, "_miniMap").Height
				          + PanelOf(bar, "_demographics").Height
				          + PanelOf(bar, "_gameInfo").Height;

				Assert.Equal(SideBarHeight, total);
				Assert.Equal(PanelHeight, PanelOf(bar, "_demographics").Height);
			}
		}

		// The widest score any game reaches, plus its label, inside the 80px column.
		[Theory]
		[InlineData(0)]      // turn 1
		[InlineData(2075)]   // the Malian save this was asked for
		[InlineData(9999)]   // a score no real game reaches
		public void TheScoreLineFitsTheColumn(int score)
		{
			Sim.EnsureRuntime();
			int right = ScoreX + Resources.Instance.GetTextSize(0, $"{score} PTS").Width;

			Assert.True(right <= 80, $"\"{score} PTS\" ends at {right}, past the 80px column");
		}

		// ...and sits above the panel's bottom border rather than under it. The height comes
		// from the panel itself, not from the constant above: a score line drawn below a panel
		// that was never grown is exactly the failure this is here for.
		[Fact]
		public void TheScoreLineSitsInsideThePanel()
		{
			Sim.NewGame(width: 80, height: 50);
			using (Palette palette = CassetteTheme.CreatePalette())
			{
				int height = PanelOf(new SideBar(palette), "_demographics").Height;
				int bottom = ScoreY + Resources.Instance.GetTextSize(0, "9999 PTS").Height;

				Assert.True(bottom <= height - 1, $"score line ends at {bottom}, border row is {height - 1}");
			}
		}

		// The population line the score used to share still fits the widest population alone.
		[Fact]
		public void ThePopulationLineStillFitsTheWidestPopulation()
		{
			Sim.EnsureRuntime();
			string popText = $"{Common.NumberSeperator(500_000_000)}#";

			Assert.True(ScoreX + Resources.Instance.GetTextSize(0, popText).Width <= 80,
				$"\"{popText}\" overruns the 80px panel");
		}
	}
}
