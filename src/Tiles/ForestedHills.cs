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
	// Wooded slopes. The fifteenth terrain, and the first with two parents: it keeps the
	// hill's defence and the forest's timber, and is generated where the two meet.
	//
	// NOT a subclass of Hills, deliberately. Half the engine asks `tile is Hills` to decide
	// whether a settler may mine, irrigate or terraform, and inheriting would silently opt
	// wooded slopes into all of it. Standing alone means each of those sites is a decision
	// somebody made rather than one nobody noticed.
	//
	// The mine is the point of the terrain. Bare hills mine for +2; these cannot be mined at
	// all (MiningShieldBonus < 0, the same idiom Forest uses), so a coal seam under the trees
	// is a two-step investment — chop, then mine — and the trees are worth something in the
	// meantime. That trade is the whole reason the tile exists.
	internal class ForestedHills : BaseTile
	{
		public override byte Movement => 2;
		// The hill's, not the forest's: it is the slope that protects, and trees on a slope
		// are not less defensible than trees on the flat.
		public override byte Defense => 4;
		public override sbyte Food => 1;
		// Forest timber, plus the coal seam if there is one — same +2 a bare hill's special
		// pays, sitting under the trees until someone clears them.
		public override sbyte Shield => (sbyte)(2 + (Special ? 2 : 0));
		public override sbyte Trade => 0;

		// Positive bonus with a cost = "can be cleared" in the idiom Forest uses; Settlers
		// reads the terrain, not the number, to decide that clearing here yields Hills.
		public override sbyte IrrigationFoodBonus => 6;
		public override byte IrrigationCost => 5;
		// Negative = cannot be mined. Chop it first.
		public override sbyte MiningShieldBonus => -1;
		public override byte MiningCost => 0;

		public ForestedHills(int x, int y, bool special) : base(x, y, special)
		{
			Type = Terrain.ForestedHills;
			Name = "Forested Hills";
		}
		public ForestedHills() : this(-1, -1, false)
		{
		}
	}
}
