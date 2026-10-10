// CivOne tests
//
// The Pax Mercatoria target belongs on the axis.
//
// The economic-output graph scaled its y-axis to the CIVILIZATIONS only, then clamped the
// threshold line to the graph edge. So once the threshold passed the leader, the line pinned
// itself to the ceiling and stayed there, reading as a fixed target rather than the live series
// it is. Observed at 2200 AD: leader on 4,237, threshold 9,328, flat at the top since about
// 1962. It would have come back into view only at the moment a civ reached it — which is
// precisely too late to be worth showing.
//
// The threshold is a SERIES, not a constant. It was half the world's output until Oct 2026 and
// is now Game.EconomicLeadMargin times the next-largest economy; either way it climbs, so the
// axis has to fit its PEAK across the plotted history, not its value today.

using CivOne.Screens.Reports;

namespace CivOne.Tests
{
	public class EconomicThresholdAxisTests
	{
		// Snapshots are [turn, barbarians, player 1, player 2, ...]. The human is player 1 here.
		private const int Human = 2;
		private static int[] Snap(params int[] civs)
		{
			var snap = new int[civs.Length + 2];
			for (int i = 0; i < civs.Length; i++) snap[i + 2] = civs[i];
			return snap;
		}

		// The bar is the best RIVAL's output times the margin: not the human's own, not the world's.
		[Fact]
		public void TheBarIsTheMarginTimesTheBestRival()
		{
			Assert.Equal((int)(400 * Game.EconomicLeadMargin), CivilizationScore.ThresholdAt(Snap(9000, 400, 300), Human));
		}

		// The barbarians are no economy, and the turn number is not an output.
		[Fact]
		public void TheTurnAndTheBarbariansAreNotRivals()
		{
			int[] snap = { 5000, 7000, 9000, 100, 100 };
			Assert.Equal((int)(100 * Game.EconomicLeadMargin), CivilizationScore.ThresholdAt(snap, Human));
		}

		[Fact]
		public void ThePeakIsTheTallestBarInTheHistory()
		{
			var history = new[] { Snap(100, 100), Snap(400, 600), Snap(300, 300) };
			Assert.Equal((int)(600 * Game.EconomicLeadMargin), CivilizationScore.PeakThreshold(history, 0, Human));
		}

		// Today counts too — the last point of the line is drawn from the live value.
		[Fact]
		public void TodayCountsAsWellAsTheHistory()
		{
			Assert.Equal(3000, CivilizationScore.PeakThreshold(new[] { Snap(100, 100) }, 3000, Human));
		}

		// A fresh game has no history at all, and the page is reachable immediately.
		[Fact]
		public void AnEmptyHistoryFallsBackToToday()
		{
			Assert.Equal(50, CivilizationScore.PeakThreshold(System.Array.Empty<int[]>(), 50, Human));
			Assert.Equal(50, CivilizationScore.PeakThreshold(null!, 50, Human));
		}

		// PeakThreshold is worthless unless the axis actually consults it. Draw() paints a bitmap
		// and cannot be staged headless, so the wiring is pinned on the source instead.
		[Fact]
		public void TheAxisActuallyConsultsIt()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "Reports", "CivilizationScore.cs"));

			Assert.Contains("maxScore = Math.Max(maxScore, PeakThreshold(", src);
			int at = src.IndexOf("maxScore = Math.Max(maxScore, PeakThreshold(");
			Assert.Contains("_page == Page.Output", src.Substring(System.Math.Max(0, at - 200), 200));
		}
	}
}
