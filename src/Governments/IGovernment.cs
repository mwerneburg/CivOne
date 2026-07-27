// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;

namespace CivOne.Governments
{
	public interface IGovernment : ICivilopedia
	{
		byte Id { get; }
		string NameAdjective { get; }
		IAdvance? RequiredTech { get; }
		int CorruptionMultiplier { get; }

		// Government rules as data — see BaseGovernment for what each one means.
		// Adding a government, or rebalancing one, should be a matter of setting
		// these rather than adding another `is Communism` test to City.cs.
		bool TilePenalty { get; }
		int TradeBonus { get; }
		int SpecialResourceTradeBonus { get; }
		int SettlerFoodCost { get; }
		int FreeUnitSupport { get; }
		bool MartialLaw { get; }
		bool CollapsesInDisorder { get; }
		bool CelebrationGrowsCity { get; }
		int WarWeariness { get; }
		int ScienceBonus { get; }
		int? FixedCorruptionDistance { get; }
		bool PalaceHalvesCorruption { get; }
	}
}