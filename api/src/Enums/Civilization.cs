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
	/// Civilization IDs. Values 0–14 are original Civ 1 civilizations; Barbarians (0) is a
	/// special pseudo-player, not a playable civ. Value 15 is intentionally skipped. Olvir (16)
	/// is a CivOne-exclusive story-arc civilization. Values 17–26 are extended NPC civilizations
	/// that occupy AI player slots 8–17, expanding the maximum opponent count beyond the
	/// original seven.
	/// </summary>
	public enum Civilization : byte
	{
		Barbarians,
		Romans,
		Babylonians,
		Franks,
		Egyptians,
		Haida,
		Greeks,
		Indians,
		Russians,
		Zulus,
		Guarani,
		Aztecs,
		Chinese,
		English,
		Mongols,
		Olvir = 16,
		Japanese,
		Persians,
		Lakota,
		Arabs,
		Khmer,
		Malians,
		Maori,
		Ottomans,
		Ethiopians,
		Iroquois,
		// Story-arc faction like the Olvir: joins mid-game via AddPlayer when the
		// Owners archetype invades — never selected at game setup.
		TheOthers = 27,
		// Story-arc faction: the South Pole Expedition's cursed outcome. Owns
		// infected cities on a destruction timer; joins mid-game via AddPlayer,
		// never selected at game setup.
		TheThing = 28,
		// Story-arc faction: the machine uprising. Wakes when the world's fifth
		// Neural Lab is completed and seizes the lab cities. Joins mid-game via
		// AddPlayer, never selected at game setup.
		Skynet = 29,
	}
}