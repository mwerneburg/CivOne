// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class RailRoad : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE RAILROAD carries goods and",
			"armies across a continent in days.",
			"",
			"SETTLERS may lay RAILROADS, which",
			"cost no movement at all and raise",
			"what their tiles produce by half.",
			"",
			"Allows DARWIN'S VOYAGE.",
		};

		private static readonly string[] _page2 =
		{
			"Rail across your heartland lets a",
			"single army defend every city on",
			"it. Build it between your best",
			"cities first.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public RailRoad() : base(3, 0, 1, Advance.SteamEngine, Advance.BridgeBuilding)
		{
			Name = "RailRoad";
			Type = Advance.RailRoad;
		}
	}
}