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
	internal class SSComponent : BaseBuilding, ISpaceShip
	{
		private static readonly string[] _page1 =
		{
			"A SPACE COMPONENT provides",
			"PROPULSION and FUEL for a",
			"SPACESHIP.",
			"",
			"More components mean a shorter",
			"voyage to ALPHA CENTAURI.",
		};

		private static readonly string[] _page2 =
		{
			"Requires PLASTICS.",
			"",
			"Components are built in pairs of",
			"propulsion and fuel; an unmatched",
			"one adds nothing to your speed.",
			"",
			"Arriving first wins the SPACE",
			"RACE, so travel time matters as",
			"much as launch date.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SSComponent() : base(16)
		{
			Name = "SS Component";
			RequiredTech = new Plastics();
			Type = Building.SSComponent;
		}
	}
}