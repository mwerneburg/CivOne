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
	/// Tile improvement types built by Olvir settlers. These parallel the human improvement
	/// system but are placed on Olvir-controlled tiles and have Olvir-specific effects.
	/// </summary>
	public enum OlvirImprovementType : byte
	{
		SettlementCluster = 0,
		Aquafarm          = 1,
		CanopyArray       = 2,
		RepairBay         = 3,
		ExchangeNode      = 4,
		BiofilterWall     = 5,
	}
}
