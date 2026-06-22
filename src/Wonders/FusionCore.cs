// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Wonders
{
	// Unlocked by Fusion Power. Phase 1 effect: empire-wide "real" SDI — every city of the
	// builder intercepts incoming nuclear strikes (see BaseUnit.Confront, the Nuclear branch).
	// The broader fusion-tech suite (hover armour, fusion infantry, recon drones, a space port,
	// city blast shields) is intended to hang off this wonder in later phases.
	internal class FusionCore : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The FUSION CORE puts portable fusion",
			"power in the field. Its first fruit is",
			"real planetary defence: every one of",
			"your cities is shielded by space-based",
			"interceptors that shoot down any nuclear",
			"strike before it can detonate.",
		};

		private static readonly string[] _page2 =
		{
			"Harnessed fusion is the threshold of a",
			"new age — hovering armour, drones that",
			"never land, blast-shielded cities, and",
			"gates to orbit. The Core is the reactor",
			"behind them all: compact, limitless, and",
			"finally tamed.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public FusionCore() : base(60)
		{
			Name = "Fusion Core";
			RequiredTech = new FusionPower();
			ObsoleteTech = null;
			SetSmallIcon(5, 6);
			Type = Wonder.FusionCore;
		}
	}
}
