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
	internal class DomeSensorNet : BaseWonder, IDomeComponent
	{
		private static readonly string[] _page1 =
		{
			"A piece of the planetary DOME.",
			"",
			"The SENSOR NET wraps the sky in a",
			"lattice of detectors, tracking",
			"every object falling toward Earth",
			"long before it arrives.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SUPERCONDUCTORS.",
			"",
			"One of the five Dome pieces: Power",
			"Core, Sensor Net, Command Hub,",
			"Kinetic Ring, and Emitter Array.",
			"",
			"Raise all five to complete it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public DomeSensorNet() : base(30)
		{
			Name         = "Dome Sensor Net";
			RequiredTech = new SuperConductor();
			ObsoleteTech = null;
			SetSmallIcon(0, 6);
			Type = Wonder.DomeSensorNet;
		}
	}
}
