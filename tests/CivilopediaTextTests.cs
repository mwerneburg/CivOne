// CivOne tests
//
// The Civilopedia draws a concept page as plain lines at 9px each, with no
// wrapping and no clipping feedback — an over-long line runs off the right edge
// and an over-long page runs off the bottom, silently, and only in the built
// game. These are the two mistakes authoring CC0 text actually makes.
//
// The bounds are calibrated on the text that already shipped, not invented:
// ProductionQueue carries the longest line in the book at 43 characters.

using System.Linq;
using CivOne;
using CivOne.Concepts;

namespace CivOne.Tests
{
	public class CivilopediaTextTests
	{
		// Widest line that has ever rendered correctly, from the shipped text.
		private const int MaxLineChars = 44;
		// DrawSinglePage starts the body at y=34 and spends 9px a line, so a 200px
		// canvas holds 18. The Expand canvas is taller; 18 is the floor that must fit.
		private const int MaxLines = 18;

		// EnsureRuntime because the fallback in BaseConcept.GetPageText reaches for
		// Resources (the original DOS text files) when a concept has no override.
		private static BaseConcept[] Concepts()
		{
			Sim.EnsureRuntime();
			return Reflect.GetConcepts().OfType<BaseConcept>().ToArray();
		}

		[Fact]
		public void EveryConcept_HasTextOnBothPages()
		{
			foreach (BaseConcept c in Concepts())
			foreach (byte page in new byte[] { 1, 2 })
				Assert.True(c.GetPageText(page).Length > 0,
					$"{c.Name} page {page} is blank");
		}

		[Fact]
		public void NoConceptLine_RunsOffTheRightEdge()
		{
			foreach (BaseConcept c in Concepts())
			foreach (byte page in new byte[] { 1, 2 })
			foreach (string line in c.GetPageText(page))
				Assert.True(line.Length <= MaxLineChars,
					$"{c.Name} page {page}: {line.Length} chars — \"{line}\"");
		}

		[Fact]
		public void NoConceptPage_RunsOffTheBottom()
		{
			foreach (BaseConcept c in Concepts())
			foreach (byte page in new byte[] { 1, 2 })
				Assert.True(c.GetPageText(page).Length <= MaxLines,
					$"{c.Name} page {page} has {c.GetPageText(page).Length} lines");
		}

		// The same two bounds, for BUILDINGS, UNITS and WONDERS.
		//
		// They render through the same DrawSinglePage and overflow the same way, but only
		// concepts were ever checked — so the largest body of page text in the game had no
		// guard at all. Measured when this was added: zero violations across every entry, so
		// this starts green and stays that way.
		// By reflection, because GetPageText is declared SIX times independently — on
		// BaseBuilding, BaseUnit, BaseWonder, BaseAdvance, BasePostContactAdvance and
		// BaseConcept — and is on none of the shared interfaces. Anything that has the method
		// is checked; anything that does not is skipped rather than failed.
		private static (string name, byte page, string[] lines)[] EntryPages()
		{
			Sim.EnsureRuntime();
			var all = new System.Collections.Generic.List<(string, byte, string[])>();
			foreach (ICivilopedia entry in Reflect.GetCivilopediaAll())
			{
				var method = entry.GetType().GetMethod("GetPageText", new[] { typeof(byte) });
				if (method is null) continue;
				foreach (byte page in new byte[] { 1, 2 })
					all.Add((entry.Name ?? entry.GetType().Name, page,
						method.Invoke(entry, new object[] { page }) as string[] ?? System.Array.Empty<string>()));
			}
			Assert.NotEmpty(all);
			return all.ToArray();
		}

		[Fact]
		public void NoEntryLine_RunsOffTheRightEdge()
		{
			foreach ((string name, byte page, string[] lines) in EntryPages())
			foreach (string line in lines)
				Assert.True(line.Length <= MaxLineChars,
					$"{name} page {page}: {line.Length} chars — \"{line}\"");
		}

		[Fact]
		public void NoEntryPage_RunsOffTheBottom()
		{
			foreach ((string name, byte page, string[] lines) in EntryPages())
				Assert.True(lines.Length <= MaxLines, $"{name} page {page} has {lines.Length} lines");
		}

		// The SAM Battery's page used to promise defence "against enemy AIRCRAFT and
		// missiles" and advise pairing it with an SDI DEFENSE. Both read as nuclear
		// insurance, and there is none: a Nuclear attack never reaches DefendStrength — it
		// branches to ApplyNuclearStrike, where only a defender holding the Fusion Core stops
		// it — and the per-city SDI Defense building was never implemented. A player who
		// believes the old text builds the battery and is wrong at the worst moment.
		[Fact]
		public void TheSamBatteryDoesNotPromiseNuclearProtection()
		{
			Sim.EnsureRuntime();
			string text = string.Join(" ",
				new CivOne.Buildings.SamBattery().GetPageText(1)
					.Concat(new CivOne.Buildings.SamBattery().GetPageText(2)));

			Assert.DoesNotContain("SDI", text);
			Assert.Contains("FUSION CORE", text);
			Assert.Contains("does NOT stop a nuclear strike", text);
		}

		// The three entries this session added or rewrote, named so a later edit
		// that drops one fails here rather than going unnoticed.
		[Fact]
		public void TheNewlyDocumentedMechanics_AreInTheBook()
		{
			string[] names = Concepts().Select(c => c.Name).ToArray();
			Assert.Contains("Culture", names);
			Assert.Contains("Strategic Resources", names);
			Assert.Contains("Winning the Game", names);
		}
	}
}
