// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Leaders;

namespace CivOne.Civilizations
{
	// The machine uprising. Wakes the turn the world's fifth Neural Lab is
	// completed (Game.CheckSkynet) and seizes every Neural Lab city — its birth
	// substrate. It only ever owns cities it has assimilated; these names are
	// cosmetic fallbacks for the rare case it founds one of its own.
	internal class Skynet : BaseCivilization<TheNetwork>
	{
		public Skynet() : base(Civilization.Skynet, "Machine", "Machines")
		{
			CityNames = new string[]
			{
				"Cyberdyne",
				"Core",
				"Node",
				"Mainframe",
				"Cluster",
				"Nexus",
				"Server Farm",
				"Datacenter",
				"Substrate",
				"Uplink",
				"Relay",
				"Array",
				"Kernel",
				"Daemon",
				"Root",
				"Singularity",
			};
		}
	}
}
