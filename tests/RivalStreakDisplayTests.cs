// CivOne tests
//
// The score report has to show that somebody ELSE is racing.
//
// Both streak victories are decided for every civilization now, but the Cultural Weight and
// Economic Output pages reported only the human's own progress. A rival sitting on 19 of 20
// looked exactly like a rival sitting on nothing, so the race was invisible from inside it —
// and losing to a Pax Mercatoria you never saw coming is not a fair loss.
//
// Also pinned here: the readout must show what the RULE measures. That is now culture per
// head of population and your rank in it — the cultural shadow was retired because the map
// generator, not play, decided who could ever qualify.

using System.Linq;

namespace CivOne.Tests
{
	public class RivalStreakDisplayTests
	{
		private static string ScreenSource()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "Reports", "CivilizationScore.cs"));
		}

		// The readout exists on BOTH pages — the two paths are equally winnable by a rival,
		// so neither may be the one you cannot see coming.
		[Theory]
		[InlineData("CultureStreak")]
		[InlineData("EconStreak")]
		public void EachPageReportsTheLeadingRivalStreak(string field)
		{
			string src = ScreenSource();

			Assert.Contains($"LeadingRivalStreak(p => Game.Progress(Game.PlayerNumber(p)).{field})", src);
		}

		// The readout must show what the victory JUDGES: culture per head, your rank in it,
		// and whether the clock has opened. It used to draw the cultural shadow, which the
		// rule no longer uses at all — and a readout of a retired rule is worse than none,
		// because it tells a player to work on something that cannot win.
		[Fact]
		public void TheReadoutShowsCulturePerHeadAndRank()
		{
			string src = ScreenSource();

			Assert.Contains("CULTURE PER HEAD", src);
			Assert.Contains("RANK {myRank}/{order.Length}", src);
			Assert.DoesNotContain("IN RANGE", src);          // the retired shadow readout
			Assert.DoesNotContain("BEST NEIGHBOUR", src);    // the retired local bar
		}

		// A player under the populace floor cannot rank at all, and must be told that rather
		// than shown a number they cannot move — the same reason reach used to be drawn.
		[Fact]
		public void APlayerTooSmallToRankIsToldSo()
		{
			string src = ScreenSource();

			Assert.Contains("TOO FEW PEOPLE TO RANK", src);
			Assert.Contains("CultureFloorShare", src);
		}

		// ...and before the gate year the path is sealed, which the screen says outright.
		[Fact]
		public void TheGateYearIsShownWhileItIsShut()
		{
			string src = ScreenSource();

			Assert.Contains("SEALED UNTIL", src);
			Assert.Contains("CultureGateYear", src);
		}

		// The rival readout answers to the same exclusions as the victory rule: a civ that
		// cannot claim the path must not be reported as racing for it.
		[Theory]
		[InlineData("TheOthers")]
		[InlineData("Skynet")]
		[InlineData("Olvir")]
		public void TheRivalReadoutSkipsCivsThatCannotClaim(string faction)
		{
			string src = ScreenSource();
			int at = src.IndexOf("(Player? rival, uint streak) LeadingRivalStreak");
			Assert.True(at > 0, "the rival-streak helper has moved or been rewritten");
			string block = src.Substring(at, 900);

			Assert.Contains(faction, block);
		}

		// A rival on nothing is not news, and drawing "0/20" every turn would train the player
		// to ignore the line that matters.
		[Fact]
		public void AZeroStreakIsNotDrawn()
		{
			string src = ScreenSource();
			int at = src.IndexOf("void DrawRivalStreak");
			Assert.True(at > 0, "the rival-streak drawing helper has moved or been rewritten");
			string block = src.Substring(at, 400);

			Assert.Contains("r.streak == 0) return", block);
		}
	}
}
