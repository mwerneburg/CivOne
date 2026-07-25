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
	internal class Pollution : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"POLLUTION appears on tiles worked",
			"by crowded, smoky cities and heavy",
			"production.",
			"",
			"A polluted tile yields far less",
			"until SETTLERS clean it.",
			"",
			"Unchecked, it risks GLOBAL WARMING",
			"that ruins terrain worldwide.",
		};

		private static readonly string[] _page2 =
		{
			"GLOBAL WARMING turns lush land to",
			"desert and swamp across the map —",
			"a slow disaster for everyone.",
			"",
			"Cut pollution at the source with",
			"RECYCLING CENTERS, MASS TRANSIT,",
			"and clean power plants.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Pollution()
		{
			Name = "Pollution";
		}
	}
}