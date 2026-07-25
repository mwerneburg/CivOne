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
	internal class SSStructural : BaseBuilding, ISpaceShip
	{
		private static readonly string[] _page1 =
		{
			"A SPACE STRUCTURAL forms the frame",
			"of a SPACESHIP.",
			"",
			"Build them in cities with APOLLO",
			"PROGRAM completed, then launch for",
			"ALPHA CENTAURI.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SPACE FLIGHT.",
			"",
			"Structurals carry the components",
			"and modules; without enough of",
			"them a ship cannot be assembled.",
			"",
			"If your CAPITAL falls while the",
			"ship is in flight, it is lost.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SSStructural() : base(8)
		{
			Name = "SS Structural";
			RequiredTech = new SpaceFlight();
			Type = Building.SSStructural;
		}
	}
}