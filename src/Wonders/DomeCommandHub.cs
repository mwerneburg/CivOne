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
	internal class DomeCommandHub : BaseWonder, IDomeComponent
	{
		private static readonly string[] _page1 =
		{
			"A piece of the planetary DOME.",
			"",
			"The COMMAND HUB binds the Dome's",
			"systems into one mind, aiming the",
			"defences faster than any human",
			"crew could.",
		};

		private static readonly string[] _page2 =
		{
			"Requires COMPUTERS.",
			"",
			"One of the five Dome pieces: Power",
			"Core, Sensor Net, Command Hub,",
			"Kinetic Ring, and Emitter Array.",
			"",
			"Complete all five to shield Earth.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public DomeCommandHub() : base(30)
		{
			Name         = "Dome Command Hub";
			RequiredTech = new Computers();
			ObsoleteTech = null;
			SetSmallIcon(2, 6);
			Type = Wonder.DomeCommandHub;
		}
	}
}
