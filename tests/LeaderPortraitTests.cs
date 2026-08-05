// CivOne tests
//
// Russia's leader changed from Peter to Trotsky (Civilizations/Russian.cs), the portrait
// generator was updated, design/leader_art/leon_trotsky.png was produced on 27 July — and it
// was never copied into the shipped defaults. Nothing failed. BaseLeader.LeaderArtPath just
// returned null and the diplomacy channel drew a blank placeholder, for over a week, visible
// only when Russia happened to open a channel with the player.
//
// That is the whole failure mode of art assets: a missing file degrades silently, and the
// roster is the thing that drifts. So walk the roster and demand a file for each name.
//
// This checks the REPOSITORY's defaults, not Settings.DataDirectory — the latter is the
// player's own install directory, which would make this a test of the machine rather than of
// what we ship.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivOne;

namespace CivOne.Tests
{
	public class LeaderPortraitTests
	{
		// Walk up from the test assembly to the repository root. Returns null when the tests
		// run from somewhere without the source tree, in which case there is nothing to check.
		private static string? RepositoryRoot()
		{
			DirectoryInfo? dir = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			return dir?.FullName;
		}

		// The rule BaseLeader.LeaderArtPath applies to turn a leader's name into a filename.
		private static string ArtFileName(string leaderName)
			=> leaderName.ToLower().Replace('.', '_').Replace(' ', '_') + ".png";

		[Fact]
		public void EveryLeaderInTheRosterHasAPortrait()
		{
			string? root = RepositoryRoot();
			if (root is null) return;   // not running from the source tree; nothing to check

			// Common.Civilizations instantiates every building to build its static tables,
			// which needs the resource layer up; Sim.NewGame is what stands that up.
			Sim.NewGame(width: 80, height: 50);

			string artDir = Path.Combine(root, "runtime", "sdl", "Resources",
				"defaults", "data", "leader_art");
			Assert.True(Directory.Exists(artDir), $"shipped leader art is missing: {artDir}");

			List<string> missing = Common.Civilizations
				.Where(c => c?.Leader is not null)
				.Select(c => c.Leader.Name)
				.Distinct()
				.Where(name => !File.Exists(Path.Combine(artDir, ArtFileName(name))))
				.OrderBy(name => name)
				.ToList();

			Assert.True(missing.Count == 0,
				"leaders with no portrait in the shipped defaults: " + string.Join(", ", missing));
		}

		// The name-to-file rule is load-bearing and easy to break by renaming a leader.
		[Fact]
		public void TheFileNameRuleMatchesTheLoader()
		{
			Assert.Equal("leon_trotsky.png", ArtFileName("Leon Trotsky"));
			Assert.Equal("m_gandhi.png", ArtFileName("M.Gandhi"));
			Assert.Equal("harun_al-rashid.png", ArtFileName("Harun al-Rashid"));
		}
	}
}
