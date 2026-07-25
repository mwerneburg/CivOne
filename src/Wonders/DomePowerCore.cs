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
	internal class DomePowerCore : BaseWonder, IDomeComponent
	{
		private static readonly string[] _page1 =
		{
			"The first piece of the planetary",
			"DOME — Earth's shield against what",
			"answers the signal from the stars.",
			"",
			"The POWER CORE feeds the whole",
			"structure, drawing energy enough",
			"to hold a shield over a world.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROBOTICS.",
			"",
			"The Dome is built in five pieces:",
			"Power Core, Sensor Net, Command",
			"Hub, Kinetic Ring, Emitter Array.",
			"",
			"Complete all five to raise the",
			"Dome and hold the planet.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public DomePowerCore() : base(30)
		{
			Name         = "Dome Power Core";
			RequiredTech = new Robotics();
			ObsoleteTech = null;
			SetSmallIcon(1, 6);
			Type = Wonder.DomePowerCore;
		}
	}
}
