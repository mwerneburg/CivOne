// CivOne tests
//
// Pax Mercatoria's hold is a named constant, and it is 75.
//
// It was a bare `>= 20` with the number spelled out again in the advisor's text and a
// hard-coded 10 for the halfway newspaper — three places to keep in step, and the kind of
// arrangement where the rule and what the game TELLS you about the rule quietly diverge.
//
// The length changed after a 20-turn win came in at 1914 AD from a save resumed at 1895: the
// condition was already met when the game loaded, so the run tested the counter rather than
// the contest. Seventy-five matches Cultural Ascendancy, which earned its number across a
// six-game batch. This one is matched by argument and is NOT yet measured.

using System.IO;
using System.Linq;

namespace CivOne.Tests
{
	public class EconomicHoldTests
	{
		// The change the user asked for, stated as a number.
		[Fact]
		public void TheHoldIsSeventyFiveTurns()
		{
			Assert.Equal(75u, Game.EconomicHoldTurns);
		}

		// The two endurance victories are deliberately the same length. If one moves without
		// the other, that should be a decision rather than a drift.
		[Fact]
		public void TheTwoEnduranceVictoriesAgree()
		{
			Assert.Equal(Game.CultureHoldTurns, Game.EconomicHoldTurns);
		}

		// The Diaspora hold is deliberately NOT one of them: it is a grace period for an enemy
		// to march on a colony site, not a standing to defend. Its comment used to claim it
		// matched the other two, which stopped being true twice over.
		[Fact]
		public void TheDiasporaGracePeriodIsSeparate()
		{
			Assert.Equal(20u, Game.DiasporaStreakTarget);
			Assert.NotEqual(Game.EconomicHoldTurns, Game.DiasporaStreakTarget);
		}

		// The rule and the message must read from the same place. A literal left behind in the
		// victory check is the failure this is really guarding: the game would congratulate you
		// on a hold it then refused to honour, or fire early on one it never announced.
		[Fact]
		public void NoLiteralSurvivesInTheRuleOrItsAdvisories()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("Progress(cnum).EconStreak++;");
			Assert.True(at > 0, "the economic streak block has moved");
			string block = src.Substring(at, src.IndexOf("_econVictoryFired = true;", at) - at);

			Assert.Contains("EconStreak >= EconomicHoldTurns", block);
			Assert.Contains("EconomicHoldTurns / 2", block);        // the halfway newspaper
			Assert.Contains("{EconomicHoldTurns} years", block);    // the advisor's own words
			Assert.DoesNotContain(">= 20", block);
			Assert.DoesNotContain("for 20 years", block);
		}

		// Halfway must be reachable and must not be the finish line — a constant small enough
		// for integer division to collapse the two would make the newspaper fire on the winning
		// turn.
		[Fact]
		public void HalfWayComesBeforeTheEnd()
		{
			Assert.True(Game.EconomicHoldTurns / 2 >= 1);
			Assert.True(Game.EconomicHoldTurns / 2 < Game.EconomicHoldTurns);
		}
	}
}
