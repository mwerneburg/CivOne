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
	/// Serializable snapshot of one unit entry in the save file. Corresponds to one slot in
	/// the Civ 1 .sve unit table (128 slots per player). Empty slots have <see cref="TypeId"/> == 0xFF.
	/// </summary>
	public struct UnitData
	{
		/// <summary>Unit slot index within the player's unit list (0–127).</summary>
		public byte Id;
		/// <summary>Status bit flags (fortified, sleeping, etc.).</summary>
		public byte Status;
		/// <summary>Map tile coordinates.</summary>
		public byte X, Y;
		/// <summary>Unit type matching <see cref="Enums.UnitType"/>. 0xFF = empty slot.</summary>
		public byte TypeId;
		/// <summary>
		/// Remaining movement encoded as <c>MovesLeft * 3 + PartMoves</c>, reflecting the
		/// fractional movement system where road tiles cost 1/3 of a full move.
		/// </summary>
		public byte RemainingMoves;
		/// <summary>Reserved; always 0. Kept for .sve format compatibility.</summary>
		public byte SpecialMoves;
		/// <summary>Goto destination X. 0xFF means no goto order is active.</summary>
		public byte GotoX;
		/// <summary>Goto destination Y. Only meaningful when <see cref="GotoX"/> != 0xFF.</summary>
		public byte GotoY;
		/// <summary>Reserved; always 0xFF. Kept for .sve format compatibility.</summary>
		public byte Visibility;
		/// <summary>
		/// ID of the next unit stacked on the same tile, forming a singly-linked list.
		/// 0xFF means this is the last unit in the stack.
		/// </summary>
		public byte NextUnitId;
		/// <summary>City slot index of the unit's home city. 0xFF = no home city.</summary>
		public byte HomeCityId;
	}
}