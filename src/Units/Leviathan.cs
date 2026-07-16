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
	// The Lighthouse's curse. The light carries farther than intended, and
	// something in the deep answers. Barbarian-owned, never producible; spawns
	// veteran off the wonder city (Game.UnleashLeviathan) and hunts ships on
	// its own AI branch (AI.Barbarians) — it never raids, never lands, never
	// gives up. Ancient navies fear it; ironclads make it a trophy.
	internal class Leviathan : BaseUnitSea
	{
		public Leviathan() : base(20, 8, 6, 2)   // never priced for sale; attack 8, defense 6, move 2
		{
			Type = UnitType.Leviathan;
			Name = "Leviathan";
			RequiredTech = null;
			ObsoleteTech = null;
			SetIcon('A', 1, 0);   // ponytail: wears the Battleship sprite until bespoke monster art exists
		}
	}
}
