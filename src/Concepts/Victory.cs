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
	// The five endings a classic game can reach, and their exact conditions.
	//
	// Checked against the code rather than written from memory: conquest at
	// Game.cs:1446, space race at :1191, the Dome at :986, score at :1215, Pax
	// Mercatoria at :1018. The page used to omit the Dome entirely and to state
	// the 2100 score ending without its waiver — which is the one a player
	// actually notices, because a contacted game sails past 2100 and keeps going.
	//
	// The post-contact endings are deliberately NOT enumerated. That arc is the
	// story, and a reference book that lists its outcomes spoils it. Page two
	// says only that the rules change, which is what a player needs to know.
	internal class Victory : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"CIVILIZATION can be won five",
			"ways:",
			"",
			"CONQUEST — be the last civ",
			"standing.",
			"SPACE RACE — reach Alpha",
			"Centauri first.",
			"THE DOME — finish all five",
			"components.",
			"SCORE — lead the world in 2100.",
			"PAX MERCATORIA — own the world's",
			"economy.",
			"",
			"If the signal is answered, 2100",
			"is not the end. What follows is",
			"not written here.",
		};

		private static readonly string[] _page2 =
		{
			"CONQUEST outlasts or destroys",
			"every rival.",
			"",
			"SPACE RACE: launching is not",
			"arriving. A ship still in flight",
			"is lost with its home city.",
			"",
			"PAX MERCATORIA: half the world's",
			"output for 75 turns, with",
			"Banking, three rivals standing,",
			"no war of your starting, and",
			"half the world bound to you by",
			"tribute, pact or trade.",
			"",
			"THE DOME needs all five of its",
			"components standing, anywhere in",
			"the world.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Victory()
		{
			Name = "Winning the Game";
		}
	}
}
