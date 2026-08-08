// CivOne tests
//
// Roster surgery, Aug 2026: the Germans and French — who already shared player slot 3 and a
// homeland — merge into Charlemagne's Franks; the Americans and Inca retire; the Haida,
// Guarani and Maori join. Net roster size is unchanged, because all four newcomers reuse the
// freed enum VALUES (3, 5, 10, 23) rather than extending the range. That matters: the slot
// formula maps original ids 1-14 onto slots 1-7 in buddy pairs and extended ids 17-26 onto
// exclusive slots 8-17, so a civ added at a new id would land in someone else's buddy slot and
// its pre-0AD respawn would clobber whoever really owns it.
//
// The three newcomers were chosen for the places that finish games empty: Haida Gwaii, the
// Parana, and Aotearoa.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Civilizations;

namespace CivOne.Tests
{
	public class RosterAndCoverageTests
	{
		private static ICivilization? Civ(string name)
			=> Common.Civilizations.FirstOrDefault(c => c is not null && c.Name == name);

		[Fact]
		public void TheNewcomersAreInTheRoster()
		{
			Sim.NewGame(width: 80, height: 50);
			foreach (string n in new[] { "Frank", "Haida", "Guarani", "Maori" })
				Assert.True(Civ(n) is not null, $"{n} is not in the roster");
		}

		[Fact]
		public void TheRetiredCivilisationsAreGone()
		{
			Sim.NewGame(width: 80, height: 50);
			foreach (string n in new[] { "German", "French", "American", "Incan" })
				Assert.True(Civ(n) is null, $"{n} is still in the roster");
		}

		// The whole reason for reusing freed enum values. A civ at a fresh id would fall
		// through BaseCivilization's slot formula into the modulo branch and share a buddy
		// slot with an original civ — and Game.PlayerDestroyed writes to that slot on a
		// pre-0AD respawn.
		[Theory]
		[InlineData("Frank",   3)]
		[InlineData("Haida",   5)]
		[InlineData("Guarani", 3)]
		[InlineData("Maori",  14)]
		public void ANewcomerSitsInAFreedSlot(string name, int slot)
		{
			Sim.NewGame(width: 80, height: 50);
			Assert.Equal(slot, Civ(name)!.PreferredPlayerNumber);
		}

		// Every civ has exactly 40 city names. Not cosmetic: Game.CityNameId derives a civ's
		// block offset by summing OTHER civs' name counts through an index that is off by one,
		// so the arithmetic only lands when every list is the same length. A 44-name list gave
		// the Franks "Orleans" as their capital.
		[Fact]
		public void EveryNewcomerHasExactlyFortyNames()
		{
			Sim.NewGame(width: 80, height: 50);
			foreach (string n in new[] { "Frank", "Haida", "Guarani", "Maori" })
				Assert.Equal(40, Civ(n)!.CityNames.Length);
		}

		// Each newcomer must anchor to the place it was added for, or it is just a renamed
		// civ starting wherever the spiral search drops it.
		[Theory]
		[InlineData("Haida",    53.2, -132.0)]
		[InlineData("Guarani", -25.3,  -57.6)]
		[InlineData("Maori",   -38.1,  176.2)]
		[InlineData("Frank",    50.8,    6.1)]
		public void ANewcomerHasAnEarthCentroid(string name, double lat, double lon)
		{
			Sim.NewGame(width: 80, height: 50);
			var (la, lo) = Game.TestEarthCentroid((Civilization)Civ(name)!.Id);
			Assert.Equal(lat, la, 1);
			Assert.Equal(lon, lo, 1);
		}

		// Start a game on the SHIPPED Earth map rather than a generated one. Sim.NewGame
		// generates, which leaves FixedStartPositions false — and the coverage rule is
		// deliberately inert there, so a test written against a generated map asserts
		// nothing at all. The first draft of this did exactly that and passed green.
		private static Game AnEarthGame(short seed)
		{
			Sim.EnsureRuntime();
			Sim.ResetState();
			Common.SetRandomSeed(seed);
			// Stage the shipped map into the test's own data directory, then use the engine's
			// real "Play on Earth" entry point. Two reasons, both learned the hard way:
			//
			//   LoadEarthBin alone loads the tiles but leaves FixedStartPositions false, and
			//   the coverage rule is gated on that flag — so the test would run against an
			//   Earth map the engine does not consider one.
			//
			//   Map.LoadMap resolves the file by walking five directories up from the
			//   executable, which is calibrated for runtime/sdl/bin/<cfg>/net10.0. From the
			//   test assembly that misses, and the loader falls through to MAP.PIC — which
			//   here yields an all-ocean world where every centroid reads as open sea. The
			//   data directory is FIRST in that search order, so staging the file there is
			//   how a test gets the real map through the real code path.
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.True(dir is not null, "not running from the source tree");
			string src = System.IO.Path.Combine(dir!.FullName, "resources", "earth_standard.bin");
			string dataDir = Settings.Instance.DataDirectory;
			System.IO.Directory.CreateDirectory(dataDir);
			System.IO.File.Copy(src, System.IO.Path.Combine(dataDir, "earth_standard.bin"), true);

			Map.Instance.LoadMap();
			var sw = System.Diagnostics.Stopwatch.StartNew();
			while (!Map.Instance.Ready && sw.Elapsed < System.TimeSpan.FromSeconds(30))
				System.Threading.Thread.Sleep(10);
			Assert.True(Map.Instance.Ready, "Earth map did not finish loading");
			var tribe = Common.Civilizations.First(c => c.PreferredPlayerNumber >= 1
			                                         && c.PreferredPlayerNumber <= 7);
			Game.CreateGame(0, 14, tribe, "Tester", "Test", "Testers");
			return Game.Instance;
		}

		private static int RegionsCovered(Game g) => g.Players
			.Where(p => p is not null && g.PlayerNumber(p) != 0)
			.Select(p => Game.TestCivRegion(p.Civilization))
			.Where(r => r != "")
			.Distinct()
			.Count();

		// The coverage rule itself: the drawn civs should occupy as many distinct landmasses
		// as the slots allow. The Franks and Guarani share slot 3, so without this Europe and
		// South America are a straight coin flip against each other.
		[Theory]
		[InlineData((short)1234)]
		[InlineData((short)4321)]
		[InlineData((short)999)]
		public void AnEarthGameSpreadsCivsAcrossLandmasses(short seed)
		{
			Game g = AnEarthGame(seed);
			Assert.True(Map.Instance.FixedStartPositions, "scenario: this must be an Earth map");

			int continents = RegionsCovered(g);

			Assert.Equal(6, continents);   // N.America, S.America, Europe, Africa, Asia, Australasia
		}
	}
}
