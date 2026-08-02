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
