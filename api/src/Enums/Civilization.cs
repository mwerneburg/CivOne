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
	/// Civilization IDs. Values 0–14 are original Civ 1 civilizations and map to player slots
	/// in .sve saves; Barbarians (0) is a special pseudo-player, not a playable civ. Olvir is
	/// a CivOne-exclusive civilization assigned value 16, deliberately skipping 15 (Persians,
	/// unused in Civ 1) to leave a gap for potential future additions.
	/// </summary>
	public enum Civilization : byte
	{
		Barbarians,
		Romans,
		Babylonians,
		Germans,
		Egyptians,
		Americans,
		Greeks,
		Indians,
		Russians,
		Zulus,
		French,
		Aztecs,
		Chinese,
		English,
		Mongols,
		Olvir = 16
	}
}