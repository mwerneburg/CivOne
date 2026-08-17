// CivOne tests
//
// The SS Component Civilopedia page carries the crossing table, and it is generated from
// Game.SpaceshipFlightYears rather than typed out. A hand-written table is exactly the kind
// of thing that survives a formula change and quietly lies: the maxed hull went from a
// 6-year crossing to a 22-year one, and no test would have caught stale prose.
//
// Also here: the page has to FIT. Civilopedia draws the blurb at 9px per line from y=34 and
// then the stats block underneath, on a 200px screen — a table that overflows is invisible
// rather than wrong, which is worse.

using System.Linq;
using CivOne.Buildings;

namespace CivOne.Tests
{
	public class SpaceshipPediaTests
	{
		public SpaceshipPediaTests() => Sim.EnsureRuntime();

		// The page quotes the READER'S crossing times, which now depend on whether their
		// civilization has the exotic fuel. With no game loaded there is no human and no
		// fuel, so the page shows the 0.1c table — and the test must compute the same thing
		// or it would be comparing two different models and calling the difference a bug.
		private const bool PageFuelState = false;

		private static string[] Page2() => new SSComponent().GetPageText(2);

		// The numbers on the page are the numbers the game flies.
		[Fact]
		public void TheTableMatchesTheFlightFormula()
		{
			string[] page = Page2();

			foreach ((int comp, int module) in new[] { (16, 3), (16, 12), (8, 6), (4, 3), (2, 3) })
			{
				int years = (int)System.Math.Round(Game.SpaceshipFlightYears(
					Game.SpaceshipStructuresNeeded(comp, module), comp, module, PageFuelState));
				Assert.True(page.Any(l => l.Contains($" {comp / 2,-8}{module / 3,-7}")
				                       && l.Contains(years.ToString())),
					$"no row for {comp / 2} engines / {module / 3} module sets at {years} years:\n"
					+ string.Join("\n", page));
			}
		}

		// The headline fact the table exists to teach: there is a ceiling, and it is the fuel
		// that sets it. With no game loaded the reader has no fuel, so the page shows the
		// 0.1c table and says so in the heading — quoting 0.2c to a civ that cannot reach it
		// would be the spaceship report's phantom colonists all over again.
		[Fact]
		public void TheTableShowsTheCeilingTheReaderCanActuallyReach()
		{
			Assert.Contains(Page2(), l => l.Contains(PageFuelState ? ".200c" : ".100c"));
			Assert.Contains(Page2(), l => l.Contains(PageFuelState ? "4.4 LIGHT YEARS"
			                                                      : "NO EXOTIC FUEL"));
		}

		// ...and the worst ship is shown as genuinely dreadful, since that is the decision the
		// table is meant to inform.
		[Fact]
		public void TheTableShowsTheMinimumHullAsRuinous()
		{
			Assert.Contains(Page2(), l => l.Contains(".025c"));
		}

		// Layout budget. Lines are 9px from y=34 and the stats block follows with an 8px gap,
		// on a 200px screen — so the blurb cannot run away. 13 lines is the working ceiling.
		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public void ThePageFitsOnTheScreen(int pageNumber)
		{
			string[] page = new SSComponent().GetPageText((byte)pageNumber);

			Assert.True(page.Length <= 13, $"page {pageNumber} is {page.Length} lines, too tall");
			Assert.All(page, l => Assert.True(l.Length <= 36, $"line too wide ({l.Length}): {l}"));
		}

		// The claim that used to sit here — "arriving first wins the SPACE RACE" — stopped
		// being true when arrival became a milestone rather than an ending. Pinned so it
		// cannot wander back in.
		[Fact]
		public void ThePageDoesNotClaimThatArrivingWins()
		{
			string all = string.Join(" ", new SSComponent().GetPageText(1)
			                            .Concat(new SSComponent().GetPageText(2))).ToUpperInvariant();

			Assert.DoesNotContain("WINS", all);
			Assert.DoesNotContain("SPACE RACE", all);
		}
	}
}
