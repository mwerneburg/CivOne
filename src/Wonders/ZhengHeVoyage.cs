// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Wonders
{
	// Coastal only. Reveals the nearest foreign continent and establishes one embassy there.
	// Requires MapMaking (primary) + Writing (secondary).
	internal class ZhengHeVoyage : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"ZHENG HE'S VOYAGE sends a great",
			"treasure fleet across the sea.",
			"",
			"It reveals the nearest foreign",
			"continent and opens an embassy",
			"with a people living there.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MAP MAKING and WRITING.",
			"Build only in a COASTAL city.",
			"",
			"Where Marco Polo charts home",
			"waters, Zheng He reaches the far",
			"shores — first contact by sail.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public ZhengHeVoyage() : base(20)
		{
			Name = "Zheng He's Voyage";
			RequiredTech = new MapMaking();
			ObsoleteTech = null;
			SetSmallIcon(7, 5);
			Type = Wonder.ZhengHeVoyage;
		}
	}
}
