// CivOne tests
//
// Pax Mercatoria: hold more than half the world's gross output for Game.EconomicHoldTurns
// consecutive turns — 75, matched to Cultural Ascendancy.
//
// Game.cs:1091 excludes the story factions from econRivals — they are not nations you can
// bind by tribute or trade. The world-output denominator below it does NOT exclude them,
// so an occupying Registry counts against your share. That asymmetry reads like an
// oversight and is not one: an occupied world has no commercial hegemon. This pins the
// ruling so it does not get "tidied up" into consistency.
//
// The recovery half — that throwing the Owners off drops them out of the sum, so a
// liberated world can win properly — is NOT covered here. Attempting it turned up an
// unrelated oddity in Player.IsDestroyed()/Game.PlayerNumber() that wants its own look;
// see the note in the session rather than trusting an assertion that was not written.

using System.Linq;
using CivOne;

namespace CivOne.Tests
{
	public class EconomicHegemonyTests
	{
		// The ruling: story factions are excluded from the rival set but not from the world.
		// Asserted on the source of truth rather than by reaching into a private method, so
		// the test fails if someone propagates the exclusion downward.
		[Fact]
		public void TheWorldOutputSum_DoesNotExcludeTheStoryFactions()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("int worldOut = _players.Where(");
			Assert.True(at > 0, "the Pax Mercatoria world-output sum has moved or been rewritten");
			string line = src.Substring(at, src.IndexOf(';', at) - at);

			Assert.DoesNotContain("TheOthers", line);
			Assert.DoesNotContain("TheThing", line);
			Assert.DoesNotContain("Skynet", line);
		}

		// ...while the rival set DOES exclude them, which is the other half of the asymmetry.
		[Fact]
		public void TheRivalSet_StillExcludesTheStoryFactions()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("Player[] econRivals = _players.Where(");
			Assert.True(at > 0, "the Pax Mercatoria rival set has moved or been rewritten");
			string block = src.Substring(at, src.IndexOf(';', at) - at);

			Assert.Contains("TheOthers", block);
			Assert.Contains("TheThing", block);
			Assert.Contains("Skynet", block);
		}

		// The aggression clause: a war of the human's own making breaks the streak, but not one
		// against a story faction. Skynet declares on everybody the moment it wakes and the
		// Registry arrives to repossess the planet — there is no version of those wars a
		// merchant could have declined, so striking back must not cost the streak. Reported
		// from a real 1921 AD game where the human held twice the world's output, was at war
		// only with the Machines, and could never have started counting.
		[Fact]
		public void TheAggressionClause_ExcludesTheStoryFactions()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("bool aggressing = _players.Any(");
			Assert.True(at > 0, "the Pax Mercatoria aggression test has moved or been rewritten");
			string block = src.Substring(at, src.IndexOf(';', at) - at);

			Assert.Contains("TheOthers", block);
			Assert.Contains("TheThing", block);
			Assert.Contains("Skynet", block);
		}

		private static string RepoRoot()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return dir!.FullName;
		}
	}
}
