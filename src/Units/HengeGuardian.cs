// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Units
{
	// Stonehenge's curse. It stepped through and it is not leaving: barbarian-
	// owned, never producible, it stands fortified beside the stones (its own
	// AI branch — it never marches) while the open door tithes the wonder city
	// a citizen every eight turns. Kill it and the door closes. Ancient armies
	// will need most of their strength to manage that.
	internal class HengeGuardian : BaseUnitLand
	{
		public HengeGuardian() : base(20, 12, 8, 1)   // never priced for sale; attack 12, defense 8, move 1
		{
			Type = UnitType.HengeGuardian;
			Name = "Guardian";
			RequiredTech = null;
			ObsoleteTech = null;
			SetIcon('D', 0, 1);   // ponytail: wears the Armor sprite until bespoke art exists
		}
	}
}
