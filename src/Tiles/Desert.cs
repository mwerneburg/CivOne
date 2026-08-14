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
	internal class Desert : BaseTile
	{
		public override byte Movement => 1;
		public override byte Defense => 2;
		// The desert special is OIL, not an oasis — same deposit Game.ResourceAt has
		// always read here. So it pays in shields, and pays what wetland oil pays (4);
		// the water it used to stand for is handled by the river oases that
		// Map.EnsureFreshwaterReachability plants in dry interiors.
		// Moisture farming: +1 food on ground with no water to draw on. Desert irrigates to 1
		// where a river or oasis is adjacent; a moisture farm is what the deep interior gets
		// instead, and the two stack for the rare tile that can have both.
		public override sbyte Food => (sbyte)((Irrigation ? 1 : 0) + (MoistureFarm ? 1 : 0));
		public override sbyte Shield => (sbyte)(1 + (Special ? 3 : 0) + (Mine ? 1 : 0));
		public override sbyte Trade => (sbyte)(Road || RailRoad ? 1 : 0);
		public override sbyte IrrigationFoodBonus => -2;
		public override byte IrrigationCost => 5;
		public override sbyte MiningShieldBonus => -2;
		public override byte MiningCost => 5;
		
		public Desert(int x, int y, bool special) : base(x, y, special)
		{
			Type = Terrain.Desert;
			Name = "Desert";
			// Half of them. Map.TileIsSpecial plants one special per 4x4 block, which
			// is right for a scattered oasis and far too much for an oilfield: the
			// Sahara came out as continuous derricks. This is a second, orthogonal
			// sieve over the same blocks, keeping a checkerboard half.
			//
			// It lives in the constructor, not at the call sites, because there are six
			// places that build a Desert (generation, both loaders, ChangeTileType) and
			// missing one would give the same tile a different answer depending on how
			// it was made. And it must stay a pure function of (x,y): Special is
			// recomputed from the coordinates on every load and every terrain change,
			// so anything remembered rather than derived comes straight back.
			if (special && ((x / 4) + (y / 4)) % 2 != 0) Special = false;
		}
		public Desert() : this(-1, -1, false)
		{
		}
	}
}