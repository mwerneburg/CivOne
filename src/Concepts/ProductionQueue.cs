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
	internal class ProductionQueue : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"The Production Queue lets you plan several",
			"builds ahead of time in a city. When the",
			"current item finishes, the next item in the",
			"queue begins automatically, with no shields",
			"lost in the changeover.",
			"",
			"This means you can set a city to build a",
			"Barracks, then a Granary, then a Temple",
			"without having to check back each turn.",
			"The queue survives saving and loading, so",
			"your plans carry forward across sessions.",
			"",
			"The AI uses the queue for all its cities.",
			"Now you can too.",
		};

		private static readonly string[] _page2 =
		{
			"HOW TO USE",
			"",
			"Open the production picker by clicking",
			"Change in the city manager, or pressing C.",
			"",
			"ENTER  -  Build the selected item now.",
			"         Replaces whatever is building.",
			"",
			"Q      -  Add the selected item to the",
			"         end of the queue. The screen",
			"         stays open so you can keep",
			"         adding items.",
			"",
			"x      -  Click the red x next to any",
			"         queue entry to remove it.",
			"",
			"TAB    -  Cycle filter: All / Units /",
			"         Buildings / Wonders.",
		};

		public override string[] GetPageText(byte pageNumber) =>
			pageNumber == 1 ? _page1 : _page2;

		public ProductionQueue()
		{
			Name = "Production Queue";
		}
	}
}
