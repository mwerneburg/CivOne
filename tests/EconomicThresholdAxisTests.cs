// CivOne tests
//
// The Pax Mercatoria target belongs on the axis.
//
// The economic-output graph scaled its y-axis to the CIVILIZATIONS only, then clamped the
// half-of-world-output line to the graph edge. So once half the world passed the leader, the
// line pinned itself to the ceiling and stayed there, reading as a fixed target rather than the
// live series it is. Observed at 2200 AD: leader on 4,237, threshold 9,328, flat at the top
// since about 1962. It would have come back into view only at the moment a civ reached it —
// which is precisely too late to be worth showing.
//
// Half of world output is a SERIES, not a constant: it climbs as the world industrialises. So
// the axis has to fit its PEAK across the plotted history, not its value today, or the line
// still leaves the chart partway along.

using CivOne.Screens.Reports;

namespace CivOne.Tests
{
	public class EconomicThresholdAxisTests
	{
		// Snapshots are per-player arrays; index 0 is the barbarian slot.
		private static int[] Snap(params int[] perPlayer)
		{
			var snap = new int[perPlayer.Length + 1];
			for (int i = 0; i < perPlayer.Length; i++) snap[i + 1] = perPlayer[i];
			return snap;
		}

		[Fact]
		public void ThePeakIsHalfTheWorldAtItsLargest()
		{
			var history = new[]
			{
				Snap(100, 100),     // world 200 -> 100
				Snap(400, 600),     // world 1000 -> 500   <- the peak
				Snap(300, 300),     // world 600 -> 300
			};

			Assert.Equal(500, CivilizationScore.PeakHalfWorld(history, liveWorldTotal: 0));
		}

		// Today counts too — the last point of the line is drawn from the live total, not from
		// the history.
		[Fact]
		public void TodayCountsAsWellAsTheHistory()
		{
			var history = new[] { Snap(100, 100) };

			Assert.Equal(1500, CivilizationScore.PeakHalfWorld(history, liveWorldTotal: 3000));
		}

		// The barbarian slot is not part of world output. The threshold line's own loop starts
		// at index 1, and these two must agree or the axis and the line disagree about where
		// the target is.
		[Fact]
		public void TheBarbarianSlotIsExcluded()
		{
			var withBarbarians = new[] { new[] { 10_000, 200, 200 } };

			Assert.Equal(200, CivilizationScore.PeakHalfWorld(withBarbarians, liveWorldTotal: 0));
		}

		// A fresh game has no history at all, and the page is reachable immediately.
		[Fact]
		public void AnEmptyHistoryFallsBackToToday()
		{
			Assert.Equal(50, CivilizationScore.PeakHalfWorld(System.Array.Empty<int[]>(), liveWorldTotal: 100));
			Assert.Equal(50, CivilizationScore.PeakHalfWorld(null!, liveWorldTotal: 100));
		}

		// PeakHalfWorld is worthless unless the axis actually consults it, and deleting that
		// one line broke nothing above — the classic "correct predicate wired to nothing".
		// Draw() paints a bitmap and cannot be staged headless, so the wiring is pinned on the
		// source instead.
		[Fact]
		public void TheAxisActuallyConsultsIt()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "Reports", "CivilizationScore.cs"));

			Assert.Contains("maxScore = Math.Max(maxScore, PeakHalfWorld(", src);
			// ...and only on the page the threshold belongs to.
			int at = src.IndexOf("maxScore = Math.Max(maxScore, PeakHalfWorld(");
			Assert.Contains("_page == Page.Output", src.Substring(System.Math.Max(0, at - 200), 200));
		}

		// The case that motivated the change, in its own numbers: a threshold well above the
		// best civ must dominate the axis, or it cannot be drawn where it belongs.
		[Fact]
		public void AThresholdAboveTheLeaderDrivesTheAxis()
		{
			// Leader 4237, world 18656 -> threshold 9328. The real 2200 AD figures.
			var history = new[] { Snap(4237, 2156, 2063, 1694, 1672, 1534, 1145, 895, 842, 675, 501, 417, 355, 338, 132) };
			int worldHalf = CivilizationScore.PeakHalfWorld(history, liveWorldTotal: 0);

			Assert.True(worldHalf > 4237,
				$"the threshold ({worldHalf}) should sit above the leader, or the axis need not change");
		}
	}
}
