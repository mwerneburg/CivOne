// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Concepts
{
	// Culture was implemented (City.CultureRate, Player.Culture, and the
	// defection pass at Game.ProcessCultureDefections) with no entry in the
	// reference book at all — a rule that can take one of your cities away
	// while nothing in the game explains it.
	//
	// Page two states the defection conditions exactly as the code checks them:
	// size <= 5, in disorder last turn, fewer than two defenders on the tile,
	// no Palace, a rival within five tiles at peace with the owner holding at
	// least triple the owner's culture, 8% per eligible city, one per turn.
	//
	// The last line of page one is a deliberate nod to the visitor-archetype
	// draw, which also reads Culture. It says there is another reader without
	// saying who.
	internal class Culture : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"Faith, arts and learning gather",
			"as CULTURE, city by city, turn",
			"by turn.",
			"Temple, Colosseum or Library add",
			"one; Cathedral or University two;",
			"a Civic Monument three.",
			"",
			"Every wonder adds three — and one",
			"the world has outgrown still adds",
			"one. Old glory endures.",
			"",
			"CULTURE TAKES CITIES. A small,",
			"rioting, lightly held town of",
			"yours may defect to an admired",
			"neighbour at peace with you.",
			"",
			"Your rivals are watching. So is",
			"someone else.",
		};

		private static readonly string[] _page2 =
		{
			"Culture takes cities without an",
			"army.",
			"",
			"A town of five citizens or fewer,",
			"in disorder, holding fewer than",
			"two defenders and no Palace, may",
			"change its flag — if a rival at",
			"peace with its owner sits within",
			"five tiles holding three times",
			"the owner's culture.",
			"",
			"The garrison disperses. At most",
			"one city in the world defects in",
			"a turn.",
			"",
			"Build the Temple you keep putting",
			"off.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Culture()
		{
			Name = "Culture";
		}
	}
}
