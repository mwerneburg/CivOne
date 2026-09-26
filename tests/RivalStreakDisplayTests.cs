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
		// so neither may be the one you cannot see coming. They ask it differently, and the
		// difference is deliberate.
		//
		// OUTPUT keeps a rival-only line under its own counter: Pax Mercatoria is an absolute
		// bar, so "our streak" and "their streak" are separate facts that can both be true.
		//
		// CULTURE folds the two into one line naming whoever leads, the human included,
		// because its counter is a RANK — at most one civ can be on a streak at a time, and
		// showing the player's own 0/75 above a rival's 29/75 read as a single counter
		// contradicting itself. Reported from a real game at 1310 AD.
		[Fact]
		public void TheOutputPageReportsTheLeadingRivalStreak()
		{
			Assert.Contains("LeadingRivalStreak(p => Game.Progress(Game.PlayerNumber(p)).EconStreak)",
				ScreenSource());
		}

		[Fact]
		public void TheCulturePageReportsWhoeverLeadsIncludingUs()
		{
			string src = ScreenSource();

			// Anchored on the assignment. A bare "LeadingStreak(" is a SUBSTRING of
			// "LeadingRivalStreak(", so it cannot fail — swapping the helper back to the
			// rival-only form passed that check, which is the whole failure this file exists
			// to catch elsewhere.
			Assert.Contains("(cultHolder, cultStreak) = LeadingStreak(", src);
			Assert.Contains("Game.Progress(Game.PlayerNumber(p)).CultureStreak)", src);
			// The human is a candidate, which is the whole point of the second helper.
			Assert.Contains("uint mine = of(Human);", src);
			// ...and the line names the civ rather than the victory.
			Assert.Contains("{cultHolder.TribeNamePlural.ToUpper()} {cultStreak}/{Game.CultureHoldTurns}", src);
			// The old pair is gone: no second culture row, and no standalone label.
			Assert.DoesNotContain("CULTURAL ASCENDANCY {cultStreak}", src);
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

		// Culture per head has ONE definition, and this screen is where that discipline
		// actually failed.
		//
		// It computed per head in TWO places: the plotted history (Game.CulturePerHeadHistory)
		// and LiveValue, which draws the live end of every line, the axis maximum and the
		// standings list. When the rule gained the blended divisor only the history moved —
		// so every line climbed to 27 and then jumped vertically to 45 at the right-hand
		// edge, and the standings list disagreed with the header directly above it. Reported
		// from a game as "the recent growth remains hidden in vertical lines".
		[Fact]
		public void TheScreenDoesNotComputeCulturePerHeadItself()
		{
			string src = ScreenSource();

			Assert.DoesNotContain("p.Culture / Math.Max(1, p.PeakPopulace)", src);
			// ...it asks the rule instead, for BOTH the live end and the ranking — and the
			// world average too (Game.CulturalWorldAverageNow), which it once also computed
			// itself and got a different answer (CultureGraphLiveEndTests).
			Assert.Contains("Game.CulturalDensity(p, Game.CulturalWorldAverageNow())", src);
		}

		// The populace floor is gone — folded into the divisor as the world's average nation
		// (Game.CulturalDensity) — so the only way not to rank is to have made no culture at
		// all, and that is what the screen must say.
		[Fact]
		public void APlayerWithNoCultureIsToldSo()
		{
			string src = ScreenSource();

			Assert.Contains("NO CULTURE TO RANK", src);
			// Off the shared helper, so the readout and the rule cannot disagree about who
			// leads — they did not use to share anything at all.
			Assert.Contains("CulturalDensity", src);
			Assert.DoesNotContain("CulturalPopulaceFloor", src);
		}

		// ...and while the gate is shut the path is sealed, which the screen says outright.
		// The gate is an ADVANCE now, not a year.
		[Fact]
		public void TheGateIsShownWhileItIsShut()
		{
			string src = ScreenSource();

			Assert.Contains("SEALED UNTIL ELECTRONICS", src);
			Assert.Contains("CultureGateOpenForDisplay", src);
		}

		// Civilizations the rules have already excluded are drawn in grey, so a glance
		// separates rivals from scenery. Pinned at the source because the report needs a live
		// screen to render — the same reason every other check on this file reads text.
		//
		// The trace and the legend row must come from ONE helper: a legend in full colour
		// beside a grey curve is worse than no marking at all, and they were separate
		// expressions of the same palette lookup before this.
		[Fact]
		public void TracesOutOfTheRunningAreGreyed()
		{
			string src = ScreenSource();

			Assert.Contains("byte TraceColour(Player p)", src);
			Assert.Contains("CassetteTheme.INK_LOW", src);
			// Off the shared rule helper, not a second copy of the four names.
			Assert.Contains("Game.CannotClaimStreakVictory(p)", src);
			// Both the curve and its legend row ask the same helper.
			Assert.Contains("byte col  = TraceColour(players[pi]);", src);
			Assert.Contains("byte col = TraceColour(p);", src);
			// A civ half the world has never met cannot ascend, so it is greyed on the culture
			// page — off the rule's own helper.
			Assert.Contains("Game.KnownByHalfTheWorld(p)", src);
			// The Score page ranks everyone alive, so it greys nobody.
			Assert.Contains("if (_page == Page.Score) return true;", src);
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
