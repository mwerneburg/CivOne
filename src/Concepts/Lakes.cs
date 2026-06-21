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
	internal class Lakes : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"LAKES are small, enclosed bodies",
			"of fresh water — too small to be",
			"the open sea. A lake tile yields",
			"food and trade like the ocean, and",
			"a FISH resource yields still more.",
			"",
			"Their fresh water lets adjacent",
			"land be IRRIGATED, even far inland",
			"from any river.",
		};

		private static readonly string[] _page2 =
		{
			"A city beside only a lake is NOT",
			"coastal: it cannot build a HARBOR",
			"or the sea wonders — LIGHTHOUSE,",
			"COLOSSUS, MAGELLAN'S EXPEDITION,",
			"and DARWIN'S VOYAGE.",
			"",
			"Lakes are calm waters: unlike the",
			"open ocean, they never spawn",
			"HURRICANES.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Lakes()
		{
			Name = "Lakes";
		}
	}
}
