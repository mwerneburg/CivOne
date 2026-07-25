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
	internal class DomeKineticRing : BaseWonder, IDomeComponent
	{
		private static readonly string[] _page1 =
		{
			"A piece of the planetary DOME.",
			"",
			"The KINETIC RING girdles the world",
			"with launchers, throwing a wall of",
			"mass at anything the Sensor Net",
			"marks.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROCKETRY.",
			"",
			"One of the five Dome pieces: Power",
			"Core, Sensor Net, Command Hub,",
			"Kinetic Ring, and Emitter Array.",
			"",
			"Complete all five to raise it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public DomeKineticRing() : base(30)
		{
			Name         = "Dome Kinetic Ring";
			RequiredTech = new Rocketry();
			ObsoleteTech = null;
			SetSmallIcon(3, 6);
			Type = Wonder.DomeKineticRing;
		}
	}
}
