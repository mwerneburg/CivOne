// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne
{
	/// <summary>
	/// Serializable snapshot of one city entry in the save file. Corresponds to one slot in
	/// the Civ 1 .sve city table (128 slots total). Empty slots have <see cref="Status"/> == 0xFF.
	/// </summary>
	public struct CityData
	{
		/// <summary>City slot index in the cities array (0–127).</summary>
		public byte Id;
		/// <summary>Index into the owning civilization's city names list.</summary>
		public byte NameId;
		/// <summary>Status flags. 0xFF indicates an empty (unused) slot.</summary>
		public byte Status;
		/// <summary>Building IDs present in this city.</summary>
		public byte[] Buildings;
		/// <summary>Map tile coordinates.</summary>
		public byte X, Y;
		/// <summary>Current city size (population).</summary>
		public byte ActualSize;
		/// <summary>ID of the item currently being produced.</summary>
		public byte CurrentProduction;
		/// <summary>Player slot index of the city owner (0–7).</summary>
		public byte Owner;
		/// <summary>Accumulated food and shields toward next growth / production completion.</summary>
		public ushort Food, Shields;
		/// <summary>
		/// 6-byte bit field encoding which surrounding tiles are currently being worked.
		/// Each bit corresponds to one tile in the city's 20-tile resource ring.
		/// </summary>
		public byte[] ResourceTiles;
		/// <summary>
		/// Unit type IDs of up to 2 units in the city's home garrison.
		/// Any additional fortified units are stored in <see cref="UnitData"/> instead.
		/// </summary>
		public byte[] FortifiedUnits;
	}
}