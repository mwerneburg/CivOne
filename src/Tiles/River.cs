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
	internal class River : BaseTile
	{
		public override byte Movement => 1;
		public override byte Defense => 3;
		public override sbyte Food => 2;
		public override sbyte Shield => (sbyte)(Special ? 1 : 0);
		// Placer gold. +3 on top of the river's own trade, so a gold river reads 4 —
		// jungle gems, not the 5 the mountain seam used to pay, because the river
		// brings 2 food with it that the mountain never did.
		public override sbyte Trade => (sbyte)(1 + (Gold ? 3 : 0));

		// Gold cannot ride on Special: BaseTile.AlternateSpecial already spends that
		// flag on the river shield, and it fires on HALF of every river — far too many
		// tiles to hang a deposit on (~700 on an Epic map against the ~87 a lattice
		// special gets). So this is the standard map lattice, the same one iron, coal
		// and gems use.
		//
		// Derived from position, never stored: Map.ChangeTileType and both loaders
		// rebuild tiles from coordinates alone, so anything remembered here would be
		// lost on the next load and the seam would move.
		public bool Gold { get; }
		public override sbyte IrrigationFoodBonus => -2;
		public override byte IrrigationCost => 5;
		public override sbyte MiningShieldBonus => -1;
		public override byte MiningCost => 0;
		
		public River(int x, int y) : base(x, y, false)
		{
			Type = Terrain.River;
			Special = AlternateSpecial();
			// x < 0 is the parameterless prototype used for scoring, which has no place
			// on the map and must not touch the Map singleton.
			Gold = x >= 0 && Map.TileIsSpecial(x, y);
			Name = "River";
		}
		public River() : this(-1, -1)
		{
		}
	}
}