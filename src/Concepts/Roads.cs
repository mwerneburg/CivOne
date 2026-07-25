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
	internal class Roads : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"SETTLERS build ROADS on land.",
			"They cost nothing to maintain.",
			"",
			"Roads speed movement — a unit",
			"spends only a third of a move to",
			"enter a road tile.",
			"",
			"On grassland, plains and desert,",
			"a road also adds +1 TRADE.",
		};

		private static readonly string[] _page2 =
		{
			"Roads let your empire move troops",
			"quickly to threatened borders and",
			"carry commerce between cities.",
			"",
			"Build them along your frontiers",
			"and between neighbouring cities",
			"first.",
			"",
			"With RAILROAD, roads become rails.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Roads()
		{
			Name = "Roads";
		}
	}
}