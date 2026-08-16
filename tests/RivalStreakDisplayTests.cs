// CivOne tests
//
// The score report has to show that somebody ELSE is racing.
//
// Both streak victories are decided for every civilization now, but the Cultural Weight and
// Economic Output pages reported only the human's own progress. A rival sitting on 19 of 20
// looked exactly like a rival sitting on nothing, so the race was invisible from inside it —
// and losing to a Pax Mercatoria you never saw coming is not a fair loss.
//
// Also pinned here: the dashed bar must show what the RULE measures — twice the best culture
// among your NEIGHBOURS, since that is what the victory compares you to. It must never be a
// function of the viewer's own culture, which would be a bar you could never clear.

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

		// The bar is the one the RULE applies: twice the best culture among the player's
		// neighbours, not twice the world's best. A bar drawn from the world's best would show
		// a target nobody is judged against — the Mongols of the 13-civ run dominated every
		// neighbour they had and would still have watched a line set by a civ two thousand
		// tiles away.
		//
		// Viewer and story-faction exclusions now come free: CulturalReachAndShadow skips its
		// own owner and refuses the story factions at source, so the screen cannot drift from
		// the rule by forgetting one.
		[Fact]
		public void TheBarIsTwiceTheBestNEIGHBOUR()
		{
			string src = ScreenSource();

			Assert.Contains("bestNeighbour * Game.CultureLeadMultiple", src);
			Assert.Contains("BEST NEIGHBOUR", src);
			Assert.DoesNotContain("int best = Game.Players", src);   // the old world-wide max
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

		// The shadow readout must show the REACH as well as the target. The target is three
		// fifths of the reach, so on its own it looks like a number out of nowhere — and a
		// player under the floor has to be able to see that the path is shut rather than
		// merely losing it.
		[Fact]
		public void TheShadowReadoutShowsTheReachItsTargetCameFrom()
		{
			string src = ScreenSource();

			Assert.Contains("Game.CulturalReachAndShadow(Human)", src);
			Assert.Contains("OF {inRange} IN RANGE", src);
			Assert.DoesNotContain("Game.CulturalShadowTarget}", src);   // the old no-argument property
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
