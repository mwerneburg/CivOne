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
	internal class Fortress : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"With CONSTRUCTION, SETTLERS can",
			"build a FORTRESS on open land.",
			"",
			"Units defending in a fortress gain",
			"a strong bonus against land",
			"attack.",
			"",
			"A fortress guards borders, passes,",
			"and river crossings.",
		};

		private static readonly string[] _page2 =
		{
			"Unlike open field, a fortress",
			"spreads its defenders' losses: a",
			"stack is not wiped out by a single",
			"defeat.",
			"",
			"Place fortresses on hills or by",
			"rivers to stack the bonuses.",
			"",
			"Enemies can PILLAGE an undefended",
			"fortress away.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Fortress()
		{
			Name = "Fortress";
		}
	}
}