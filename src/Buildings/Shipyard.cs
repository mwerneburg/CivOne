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

namespace CivOne.Buildings
{
	internal class Shipyard : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A SHIPYARD builds and berths naval",
			"units.",
			"",
			"Ships put to sea from a Shipyard",
			"city as VETERANS, fighting at",
			"greater strength.",
		};

		private static readonly string[] _page2 =
		{
			"Requires NAVIGATION.",
			"Coastal cities only.",
			"",
			"A Shipyard is to your fleet what",
			"BARRACKS are to your army — the",
			"mark of a naval power.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Shipyard() : base(8, 3)
		{
			Name = "Shipyard";
			RequiredTech = new Navigation();
			SetIcon(1, 2, false);
			SetSmallIcon(1, 2);
			Type = Building.Shipyard;
		}
	}
}
