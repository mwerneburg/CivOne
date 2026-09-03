// CivOne tests
//
// unit_tiles.txt fails silently in three different ways, and all three end with a unit that
// is simply not drawn:
//
//   * a header that is not a UnitType name  — Enum.TryParse fails, FlushTxtSection returns
//   * a section that is not 256 values      — the branch does not run, nothing is registered
//   * a section of all zeros                — registers fine, draws nothing (0 = transparent)
//
// None of them logs a complaint the player would see. A dropped row in the middle of a grid
// is the easy one to make: sixteen rows of sixteen numbers, edited by hand.
//
// Written when blank commented-out templates were added to the file for the units that had
// no section yet, which makes the third case a live risk — uncomment a template, forget to
// draw in it, ship an invisible unit.
//
// Parsing mirrors BaseUnit.LoadUnitTiles exactly: trim, skip blank and '#', '[Name]' opens a
// section, everything else contributes whatever byte.TryParse accepts.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class UnitTileFileTests
	{
		private static string TilePath => Path.Combine(Sim.RepoRoot(),
			"runtime", "sdl", "Resources", "defaults", "unit_tiles", "unit_tiles.txt");

		// Only sections the loader would actually see — commented lines never reach it.
		private static List<(string name, List<byte> pixels)> ActiveSections()
		{
			var sections = new List<(string, List<byte>)>();
			string? current = null;
			var pixels = new List<byte>();

			foreach (string raw in File.ReadAllLines(TilePath))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;
				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					if (current is not null) sections.Add((current, pixels));
					current = line.Substring(1, line.Length - 2);
					pixels = new List<byte>();
				}
				else if (current is not null)
				{
					foreach (string tok in line.Split(new[] { ' ', '\t', ',' },
						System.StringSplitOptions.RemoveEmptyEntries))
						if (byte.TryParse(tok, out byte v)) pixels.Add(v);
				}
			}
			if (current is not null) sections.Add((current, pixels));

			Assert.NotEmpty(sections);
			return sections;
		}

		// A misspelled header is dropped without a word.
		[Fact]
		public void EverySectionNamesARealUnitType()
		{
			foreach ((string name, _) in ActiveSections())
				Assert.True(System.Enum.TryParse(name, ignoreCase: true, out UnitType _),
					$"[{name}] is not a UnitType — the loader will skip it silently");
		}

		// 256 for a map tile. Anything else registers nothing, so a single dropped row
		// deletes the unit's art with no error anywhere.
		[Fact]
		public void EverySectionIsAWholeGrid()
		{
			foreach ((string name, List<byte> pixels) in ActiveSections())
				Assert.True(pixels.Count == 256,
					$"[{name}] has {pixels.Count} values — needs exactly 256 (16x16)");
		}

		// Index 0 is transparent, so an all-zero grid loads perfectly and draws nothing. This
		// is what an uncommented-but-unfilled template ships as.
		[Fact]
		public void NoSectionIsEntirelyTransparent()
		{
			foreach ((string name, List<byte> pixels) in ActiveSections())
				Assert.True(pixels.Any(p => p != 0),
					$"[{name}] is entirely transparent — the unit will be invisible on the map");
		}

		// The templates are meant to sit inert until somebody draws in one. If a header goes
		// live while still full of zeros the test above catches it; this catches the subtler
		// case of the templates quietly disappearing from the file altogether.
		[Theory]
		[InlineData("CruiseMissile")]
		[InlineData("ReaperDrone")]
		[InlineData("HydroEngineer")]
		[InlineData("SeaCaravan")]
		public void TheBlankTemplateIsPresentAndStillCommented(string unit)
		{
			string text = File.ReadAllText(TilePath);

			Assert.Contains($"#[{unit}]", text);
			Assert.DoesNotContain($"\n[{unit}]", text);
		}
	}
}
