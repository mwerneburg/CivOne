// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Tiles
{
	internal class Mountains : BaseTile
	{
		public override byte Movement => 3;
		public override byte Defense => 6;
		public override sbyte Food => 0;
		// The mountain special is IRON, not Civ 1's gold seam: it is the deposit
		// Game.ResourceAt has always read off this tile, and it paid in trade, which
		// no player could connect to cannon and ironclads. Ore pays in shields.
		public override sbyte Shield => (sbyte)(1 + (Special ? 2 : 0) + (Mine ? 1 : 0));
		public override sbyte Trade => 0;
		public override sbyte IrrigationFoodBonus => -1;
		public override byte IrrigationCost => 0;
		public override sbyte MiningShieldBonus => -2;
		public override byte MiningCost => 10;
		
		public Mountains(int x, int y, bool special) : base(x, y, special)
		{
			Type = Terrain.Mountains;
			Name = "Mountains";
		}
		public Mountains() : this(-1, -1, false)
		{
		}
	}
}