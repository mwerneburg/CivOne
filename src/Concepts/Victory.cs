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
	internal class Victory : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"CIVILIZATION can be won several",
			"ways:",
			"",
			"CONQUEST — be the last civ left.",
			"SPACE RACE — first to Alpha",
			"Centauri.",
			"SCORE — lead the world at 2100 AD.",
			"PAX MERCATORIA — dominate the",
			"world's economy.",
		};

		private static readonly string[] _page2 =
		{
			"CONQUEST outlasts or destroys all",
			"rival civilizations.",
			"",
			"SPACE RACE: build a spaceship and",
			"reach Alpha Centauri first.",
			"",
			"PAX MERCATORIA rewards commerce,",
			"not cannon: half the world's",
			"output for 20 turns, with Banking,",
			"no war you started, and rivals",
			"bound to your trade.",
			"",
			"Lose all cities and units: defeat.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Victory()
		{
			Name = "Winning the Game";
		}
	}
}
