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
	internal class BridgeBuilding : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"BRIDGE BUILDING spans the rivers",
			"that until now turned every road",
			"aside.",
			"",
			"Your SETTLERS may build ROADS on",
			"RIVER tiles.",
		};

		private static readonly string[] _page2 =
		{
			"River tiles are among the best",
			"land you own. Until bridges, your",
			"road network simply stops at the",
			"water.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public BridgeBuilding() : base(4, 1, 0, Advance.IronWorking, Advance.Construction)
		{
			Name = "Bridge Building";
			Type = Advance.BridgeBuilding;
		}
	}
}