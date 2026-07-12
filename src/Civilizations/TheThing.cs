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
	// The cursed outcome of the South Pole Expedition: the anomaly under the ice
	// was not propulsion hardware. The Thing joins mid-game via Game.InfectCity
	// and only ever owns cities it has assimilated — these names are cosmetic
	// fallbacks (infected cities keep their original names).
	internal class TheThing : BaseCivilization<TheOrganism>
	{
		public TheThing() : base(Civilization.TheThing, "The Thing", "The Thing")
		{
			CityNames = new string[]
			{
				"Outpost 31",
				"Thule Station",
				"Quarantine",
				"Isolation",
				"Assimilation",
				"Imitation",
				"Incubation",
				"Cellular",
				"Divergence",
				"Replication",
			};
		}
	}
}
