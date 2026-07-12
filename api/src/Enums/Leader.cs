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
	/// Leader IDs. OlvirCouncil is a CivOne-exclusive leader for the Olvir civilization.
	/// Leader values are not persisted in save files — leader assignment is resolved from
	/// civilization identity at load time, so values may be reordered freely.
	/// </summary>
	public enum Leader : byte
	{
		Atilla,
		Caesar,
		Hammurabi,
		Frederick,
		Ramesses,
		Lincoln,
		Alexander,
		Gandhi,
		Shaka,
		Napoleon,
		Montezuma,
		Elizabeth,
		Genghis,
		Peter,
		Deng,
		OlvirCouncil,
		Tokugawa,
		Cyrus,
		SittingBull,
		HarunAlRashid,
		Jayavarman,
		MansaMusa,
		Pachacuti,
		Suleiman,
		HaileSelassie,
		Hiawatha,
		TheRegistry,
		TheOrganism,
	}
}