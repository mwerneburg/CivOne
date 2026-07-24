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
			"A game of CIVILIZATION can be won",
			"in several ways:",
			"",
			"CONQUEST — outlast every rival so",
			"yours is the last civilization",
			"standing on Earth.",
			"",
			"SPACE RACE — build a spaceship and",
			"be first to reach Alpha Centauri.",
		};

		private static readonly string[] _page2 =
		{
			"SCORE — reach the year 2100 AD and",
			"the highest-scoring civilization",
			"wins the game.",
			"",
			"PAX MERCATORIA — command over half",
			"the world's economy for 20 turns,",
			"with Banking, no war you started,",
			"and rivals bound to your trade.",
			"",
			"Lose every city and unit, and you",
			"are defeated.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Victory()
		{
			Name = "Winning the Game";
		}
	}
}
