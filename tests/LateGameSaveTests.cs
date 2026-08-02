// CivOne tests
//
// Regression coverage for loading a rich, late-game save. The fresh-game
// round-trip test exercises an empty world; this one loads CIVIL3.cos — a real
// turn-668 / 2118 AD Malian game with the full post-contact feature set (Olvir
// cities, tribute pacts, dome assignments, many cities/units). That combination
// is what surfaced the RepairWrappedCities load-time crash, which a fresh-game
// test could never reach.

using System;
using System.IO;
using System.Linq;
using CivOne;

namespace CivOne.Tests
{
	public class LateGameSaveTests
	{
		private static string FixturePath(string name)
			=> Path.Combine(AppContext.BaseDirectory, "fixtures", name);

		// The crash-class guard: a rich save must load to completion without throwing,
		// and produce a populated game state.
		[Fact]
		public void LateGameSave_Loads_WithoutThrowing()
		{
			Sim.EnsureRuntime();
			Sim.ResetState();

			Assert.True(Game.LoadCos(FixturePath("CIVIL3.cos")), "rich late-game save should load");
			Assert.NotNull(Game.Instance);
			Assert.Equal(668, Game.Instance.GameTurn);
			Assert.True(Game.Instance.GetCities().Length > 0, "loaded game should have cities");
		}

		// A NameId indexes the flattened list of every civilization's names, and that layout
		// changed when the per-civ blocks grew from 16 names to 40 — every block after the
		// first moved. CIVIL3.cos was written against the old layout, so it is the guard that
		// loading an old save does not silently rename all 115 of its cities.
		//
		// "Michaelsburg" is the sharp end: renames are stored by overwriting an entry in the
		// saved array, so that name exists in no canonical list and a remap that only
		// consulted the canonical names would rename the player's city back to Brundisium.
		[Fact]
		public void ASaveFromTheOldNameLayout_KeepsItsCityNames()
		{
			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(FixturePath("CIVIL3.cos")), "rich late-game save should load");

			string[] names = Game.Instance.GetCities().Select(c => c.Name).ToArray();
			Assert.Equal(115, names.Length);
			Assert.Contains("Michaelsburg", names);
			Assert.Contains("Timbuktu", names);
			Assert.Contains("Kumbi Saleh", names);
			Assert.DoesNotContain(names, string.IsNullOrWhiteSpace);
		}

		// Determinism on rich state: load -> save -> load -> save must reproduce the
		// file byte-for-byte. Compares the two engine-written saves (not the original
		// file), so it's independent of any older on-disk format the fixture used.
		[Fact]
		public void LateGameSave_RoundTripsDeterministically()
		{
			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(FixturePath("CIVIL3.cos")), "rich late-game save should load");

			string saves = Settings.Instance.SavesDirectory;
			Directory.CreateDirectory(saves);
			string first  = Path.Combine(saves, "civil3_a.cos");
			string second = Path.Combine(saves, "civil3_b.cos");

			Game.Instance.SaveCos(first);
			Sim.ResetState();
			Assert.True(Game.LoadCos(first), "re-load of engine-written save should succeed");
			Game.Instance.SaveCos(second);

			Assert.Equal(File.ReadAllText(first), File.ReadAllText(second));
		}
	}
}
