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
	// The six endings a classic game can reach, and their exact conditions.
	//
	// Checked against the code rather than written from memory. It said FIVE for a
	// long time and was wrong twice over: Cultural Ascendancy and Diaspora were
	// both implemented and neither was listed, and the space race was described as
	// "reach Alpha Centauri first" — a rule this game deliberately removed. Arrival
	// is a milestone; the ending is Diaspora, and a player who read this page
	// landed a colony expecting the game to stop and it did not.
	//
	// A test now asserts every ending the code can actually fire is named here, so
	// the next one added cannot be added quietly.
	//
	// The post-contact endings are deliberately NOT enumerated. That arc is the
	// story, and a reference book that lists its outcomes spoils it. Page two
	// says only that the rules change, which is what a player needs to know.
	internal class Victory : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"CIVILIZATION can be won six ways:",
			"",
			"CONQUEST — be the last civ",
			"standing.",
			"DIASPORA — settle Alpha Centauri",
			"and keep the colony supplied.",
			"THE DOME — finish all five",
			"components.",
			"SCORE — lead the world in 2100.",
			"PAX MERCATORIA — own the world's",
			"economy.",
			"CULTURAL ASCENDANCY — be the most",
			"admired. See its own page.",
			"",
			"If the signal is answered, 2100",
			"is not the end. What follows is",
			"not written here.",
		};

		private static readonly string[] _page2 =
		{
			"CONQUEST outlasts every rival.",
			"",
			"DIASPORA: launching is not",
			"arriving, and arriving is not",
			"winning. A ship in flight dies",
			"with its home city; the colony",
			"must then be supplied 20 years by",
			"a city with MISSION CONTROL. Lose",
			"it and the count begins again.",
			"",
			"PAX MERCATORIA: half the world's",
			"output for 75 turns, with Banking,",
			"three rivals standing, no war of",
			"your starting, half the world bound.",
			"",
			"THE DOME: all five parts, standing",
			"anywhere in the world.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Victory()
		{
			Name = "Winning the Game";
		}
	}
}
