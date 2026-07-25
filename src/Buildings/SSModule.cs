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
	internal class SSModule : BaseBuilding, ISpaceShip
	{
		private static readonly string[] _page1 =
		{
			"A SPACE MODULE houses the",
			"colonists, their life support and",
			"the solar panels that keep them",
			"alive.",
			"",
			"Modules decide how many settlers",
			"arrive at ALPHA CENTAURI.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROBOTICS.",
			"",
			"Habitation, life support and solar",
			"panels must be balanced. Colonists",
			"without life support do not",
			"survive the journey.",
			"",
			"The costliest spaceship part.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SSModule() : base(32)
		{
			Name = "SS Module";
			RequiredTech = new Robotics();
			Type = Building.SSModule;
		}
	}
}