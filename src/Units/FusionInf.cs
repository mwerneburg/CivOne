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
	// Fusion infantry — the tough, mobile defender that screens the Hover Tank. Unlocked
	// once the builder owns the Fusion Core wonder (see Player.UnitAvailable). Move 4 keeps
	// it alongside the armour it supports.
	internal class FusionInf : BaseUnitLand
	{
		public FusionInf() : base(6, 6, 10, 4)   // price 60, attack 6, defense 10, move 4
		{
			Type = UnitType.FusionInf;
			Name = "Fusion Inf.";
			RequiredTech = new FusionPower();
			ObsoleteTech = null;
			SetIcon('C', 0, 0);   // reuse the Mech. Inf. sprite until bespoke art exists
		}
	}
}
