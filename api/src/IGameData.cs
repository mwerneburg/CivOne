// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;

namespace CivOne
{
	/// <summary>
	/// Abstract interface over a game save file. CivOne ships one implementation backed by the
	/// original Civ 1 binary .sve format; a second COS (YAML) implementation extends it with
	/// CivOne-specific fields. Plugins that add a new save format implement this interface and
	/// register it with the runtime.
	/// </summary>
	public interface IGameData : IDisposable
	{
		/// <summary>Current game turn (0-based). Setting this also updates the stored game year.</summary>
		ushort GameTurn { get; set; }
		/// <summary>Player slot index (0–7) for the human player.</summary>
		ushort HumanPlayer { get; set; }
		ushort RandomSeed { get; set; }
		/// <summary>Difficulty level (0 = Chieftain … 3 = Deity).</summary>
		ushort Difficulty { get; set; }
		/// <summary>bool[8] — true for each player slot still active in the game.</summary>
		bool[] ActiveCivilizations { get; set; }
		/// <summary>byte[8] — per-player civilization identity flags, stored as 0 or 1.</summary>
		byte[] CivilizationIdentity { get; set; }
		/// <summary>Advance ID currently being researched by the human player.</summary>
		ushort CurrentResearch { get; set; }
		/// <summary>byte[8][] — for each player, the list of advance IDs that player has discovered.</summary>
		byte[][] DiscoveredAdvanceIDs { get; set ;}
		/// <summary>string[8] — leader name per player slot.</summary>
		string[] LeaderNames { get; set; }
		/// <summary>string[8] — civilization name per player slot.</summary>
		string[] CivilizationNames { get; set; }
		/// <summary>string[8] — citizen/adjective name per player slot.</summary>
		string[] CitizenNames { get; set; }
		/// <summary>Names of all cities across all players.</summary>
		string[] CityNames { get; set; }
		/// <summary>short[8] — gold treasury per player slot.</summary>
		short[] PlayerGold { get; set; }
		/// <summary>short[8] — accumulated research beakers per player slot.</summary>
		short[] ResearchProgress { get; set; }
		/// <summary>ushort[8] — tax rate percentage per player slot.</summary>
		ushort[] TaxRate { get; set; }
		/// <summary>ushort[8] — science rate percentage per player slot.</summary>
		ushort[] ScienceRate { get; set; }
		/// <summary>ushort[8] — starting map X coordinate per player slot.</summary>
		ushort[] StartingPositionX { get; set; }
		/// <summary>ushort[8] — government type per player slot.</summary>
		ushort[] Government { get; set; }
		/// <summary>
		/// ushort[64] — flat 8×8 diplomatic state matrix. Index <c>i*8+j</c> encodes player i's
		/// stance toward player j: 0x2 = at war, 0x0 = at peace.
		/// </summary>
		ushort[] Diplomacy { get; set; }
		/// <summary>Up to 128 city entries. Empty slots have <see cref="CityData.Status"/> == 0xFF.</summary>
		CityData[] Cities { get; set; }
		/// <summary>UnitData[8][] — unit lists per player slot. Empty slots have TypeId == 0xFF.</summary>
		UnitData[][] Units { get; set; }
		/// <summary>
		/// ushort[22] — one entry per original Civ 1 wonder ID. Value is the city slot index that
		/// owns the wonder; <see cref="ushort.MaxValue"/> means the wonder has not been built.
		/// </summary>
		ushort[] Wonders { get; set; }
		/// <summary>bool[8][80,50] — explored tile flags per player slot.</summary>
		bool[][,] TileVisibility { get; set; }
		/// <summary>ushort[] — game turn on which each advance was first discovered by any player.</summary>
		ushort[] AdvanceFirstDiscovery { get; set; }
		/// <summary>bool[8] — miscellaneous game option flags.</summary>
		bool[] GameOptions { get; set; }
		/// <summary>Turn on which the next advisor/anthology event fires.</summary>
		ushort NextAnthologyTurn { get; set; }
		/// <summary>Number of AI opponents in the game.</summary>
		ushort OpponentCount { get; set; }
		/// <summary>Accumulated global warming counter; triggers terrain degradation at thresholds.</summary>
		ushort GlobalWarmingCount { get; set; }
		/// <summary>Ordered log of notable game events, used to drive the end-game replay sequence.</summary>
		ReplayData[] ReplayData { get; set; }

		/// <summary>
		/// True if the data was loaded from a correctly-sized save file, or freshly initialized.
		/// False if the source bytes were the wrong length; all other members should be ignored.
		/// </summary>
		bool ValidData { get; }
		/// <summary>Serialize the game state to raw save-file bytes.</summary>
		byte[] GetBytes();
		/// <summary>
		/// Returns true if the given map dimensions are supported by this save format.
		/// The .sve adapter only accepts 80×50.
		/// </summary>
		bool ValidMapSize(int width, int height);
	}
}