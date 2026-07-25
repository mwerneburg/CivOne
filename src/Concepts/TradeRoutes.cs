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
	internal class TradeRoutes : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"TRADE ROUTES link two cities with",
			"lasting commerce.",
			"",
			"Build a CARAVAN, send it to another",
			"city, and establish a route for a",
			"one-time gold and science bonus.",
			"",
			"The route then adds ongoing TRADE",
			"to both cities.",
		};

		private static readonly string[] _page2 =
		{
			"Routes pay best between distant,",
			"large cities — especially with a",
			"foreign civilization.",
			"",
			"A city holds a limited number of",
			"routes, so choose partners well.",
			"",
			"A CARAVAN can also help build a",
			"wonder instead.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TradeRoutes()
		{
			Name = "Trade Routes";
		}
	}
}