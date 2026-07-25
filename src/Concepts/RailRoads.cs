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
	internal class RailRoads : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"RAILROADS replace roads once you",
			"discover the RAILROAD advance.",
			"",
			"SETTLERS lay rails on any land",
			"tile that already has a road.",
			"",
			"Units move along connected rails",
			"for FREE — no movement is spent.",
		};

		private static readonly string[] _page2 =
		{
			"A rail network lets you rush",
			"defenders anywhere in your empire",
			"in a single turn.",
			"",
			"Rails also work the land: a mined",
			"or irrigated tile with rails gives",
			"a little extra yield.",
			"",
			"Enemies can PILLAGE your rails.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public RailRoads()
		{
			Name = "RailRoads";
		}
	}
}