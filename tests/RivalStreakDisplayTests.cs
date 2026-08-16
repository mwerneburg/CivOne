// CivOne tests
//
// The score report has to show that somebody ELSE is racing.
//
// Both streak victories are decided for every civilization now, but the Cultural Weight and
// Economic Output pages reported only the human's own progress. A rival sitting on 19 of 20
// looked exactly like a rival sitting on nothing, so the race was invisible from inside it —
// and losing to a Pax Mercatoria you never saw coming is not a fair loss.
//
// Also pinned here: the dashed "2x BEST RIVAL" bar must never be a function of the viewer's
// own culture. A bar that rose with your score would be one you could never clear.

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

		// The bar must exclude the viewer. Including them would make the target a function of
		// their own score: improve your culture, raise your own bar, never win.
		[Fact]
		public void TheBarExcludesTheViewer()
		{
			string src = ScreenSource();
			int at = src.IndexOf("int best = Game.Players");
			Assert.True(at > 0, "the culture bar has moved or been rewritten");
			string block = src.Substring(at, 400);

			Assert.Contains("p != Human", block);
		}

		// ...and excludes the story factions, which the victory rule refuses as claimants.
		// With an active Registry the bar would otherwise sit at twice THEIR culture — a
		// target the rule does not actually impose on anybody.
		[Theory]
		[InlineData("TheOthers")]
		[InlineData("TheThing")]
		[InlineData("Skynet")]
		public void TheBarExcludesTheStoryFactions(string faction)
		{
			string src = ScreenSource();
			int at = src.IndexOf("int best = Game.Players");
			string block = src.Substring(at, 400);

			Assert.Contains(faction, block);
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
