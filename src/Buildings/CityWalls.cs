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
	internal class CityWalls : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"CITY WALLS multiply the defence of",
			"land units in the city TWELVEFOLD",
			"against most attackers.",
			"",
			"Walled cities are also far harder",
			"for a captor to hold.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MASONRY.",
			"",
			"Howitzers and other siege weapons",
			"ignore walls entirely.",
			"",
			"The GREAT WALL acts as city walls",
			"in all your cities until GUNPOWDER",
			"is discovered.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CityWalls() : base(12, 2)
		{
			Name = "City Walls";
			RequiredTech = new Masonry();
			SetIcon(1, 2, false);
			SetSmallIcon(1, 2);
			Type = Building.CityWalls;
		}
	}
}