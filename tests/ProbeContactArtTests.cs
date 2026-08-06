// CivOne tests
//
// The probe's phase-3 visual contact plate used to be hard-coded to OlvirInSpace: whoever was
// actually coming, the player saw the Olvir refugee ship. Now it switches on VisitorType.
//
// That switch resolves art by NAME, and misses fall back to OlvirInSpace so an asset-free
// install still gets a picture. Which means a typo'd or uninstalled plate is invisible in
// play — exactly the failure mode LeaderPortraitTests exists for. So demand the files.
//
// Checks the REPOSITORY's defaults, not Settings.DataDirectory: the latter is the player's
// own install, which would make this a test of the machine rather than of what we ship.

using System.IO;
using CivOne;

namespace CivOne.Tests
{
	public class ProbeContactArtTests
	{
		private static string? RepositoryRoot()
		{
			DirectoryInfo? dir = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			return dir?.FullName;
		}

		[Theory]
		[InlineData("OlvirInSpace")]      // Refugees, and the fallback for every other archetype
		[InlineData("ScavengerContact")]  // Scavengers
		[InlineData("OthersIntercept")]   // Owners: the probe does not come home
		public void EveryProbeContactPlateIsShipped(string name)
		{
			string? root = RepositoryRoot();
			if (root is null) return;   // not running from the source tree; nothing to check

			string path = Path.Combine(root, "runtime", "sdl", "Resources",
				"defaults", "data", "event_art", name + ".png");
			Assert.True(File.Exists(path), $"probe contact art is missing: {path}");
		}
	}
}
