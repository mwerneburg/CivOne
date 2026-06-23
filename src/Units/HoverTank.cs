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

namespace CivOne.Units
{
	// Fusion-powered spearhead, unlocked once the builder owns the Fusion Core wonder
	// (see Player.UnitAvailable). Hovers over rough terrain: it never pays the Hills/
	// Mountains last-move penalty, so it keeps its full speed everywhere.
	internal class HoverTank : BaseUnitLand
	{
		public override bool IgnoresTerrainCost => true;

		public HoverTank() : base(9, 14, 8, 4)   // price 90, attack 14, defense 8, move 4
		{
			Type = UnitType.HoverTank;
			Name = "Hover Tank";
			RequiredTech = new FusionPower();
			ObsoleteTech = null;
			SetIcon('D', 0, 1);   // reuse the Armor sprite until bespoke art exists
		}
	}
}
