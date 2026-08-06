// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Enums
{
	/// <summary>
	/// Unit type IDs. Values are persisted verbatim in the Civ 1 .sve save format and must
	/// not be reordered. New CivOne unit types should be appended after the last original value.
	/// </summary>
	public enum UnitType
	{
		Settlers = 0,
		Militia = 1,
		Phalanx = 2,
		Legion = 3,
		Musketeers = 4,
		Riflemen = 5,
		Cavalry = 6,
		Knights = 7,
		Catapult = 8,
		Cannon = 9,
		Chariot = 10,
		Armor = 11,
		MechInf = 12,
		Artillery = 13,
		Fighter = 14,
		Bomber = 15,
		Trireme = 16,
		Sail = 17,
		Frigate = 18,
		Ironclad = 19,
		Cruiser = 20,
		Battleship = 21,
		Submarine = 22,
		Carrier = 23,
		Transport = 24,
		Nuclear = 25,
		Diplomat = 26,
		Caravan = 27,
		Explorer = 28,
		HydroEngineer = 29,
		SeaCaravan = 30,
		HoverTank = 31,
		FusionInf = 32,
		// Barbarian kaiju — never producible; woken by the first nuclear
		// detonation once the Manhattan Project egg is planted (Game.AwakenGozira).
		Gozira = 33,
		// Barbarian sea monster — never producible; drawn in by the Lighthouse's
		// cursed roll (Game.UnleashLeviathan). Hunts ships until slain.
		Leviathan = 34,
		// Barbarian guardian — never producible; steps through Stonehenge's
		// cursed roll (Game.OpenStoneDoor) and stands in the stones until slain.
		HengeGuardian = 35,
		// Sea-going settler: sails, puts ashore, founds a city. The answer to civs
		// that start on islands or behind a strait and can never expand by land.
		Longboat = 36,
		// Scavenger extraction craft — never producible, barbarian-owned. Put down by the
		// harvest (Game.ArriveScavengers), it drains the water it stands beside and moves on.
		// Killing them is the only counterplay: no harvesters, no extraction.
		Harvester = 37,
	}
}