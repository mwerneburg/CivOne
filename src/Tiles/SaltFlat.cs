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
	// What the water leaves behind. The fourteenth terrain, and the first that is never
	// generated: a salt flat exists only where a sea or a lake used to be, so it appears
	// mid-game or not at all.
	//
	// Deliberately worse than Desert, which at least yields a shield. A drained seabed must not
	// feed anyone — a city that loses its ocean tiles to salt should starve, not merely
	// stagnate, or taking the water away reads as an inconvenience instead of a catastrophe.
	// Mining is allowed at a punitive cost so a ruined coast is something a player can still
	// work twenty turns later; irrigation is not, because the salt is the whole problem.
	internal class SaltFlat : BaseTile
	{
		public override byte Movement => 1;
		public override byte Defense => 1;
		public override sbyte Food => 0;
		public override sbyte Shield => (sbyte)(Mine ? 1 : 0);
		public override sbyte Trade => (sbyte)(HasTransportLink ? 1 : 0);

		// Negative bonus = "cannot be irrigated" in the same idiom Desert uses for its penalty;
		// TileExtensions.AllowIrrigation reads the sign.
		public override sbyte IrrigationFoodBonus => -1;
		public override byte IrrigationCost => 0;
		public override sbyte MiningShieldBonus => 1;
		public override byte MiningCost => 10;

		public SaltFlat(int x, int y, bool special) : base(x, y, special)
		{
			Type = Terrain.SaltFlat;
			Name = "Salt Flat";
		}
		public SaltFlat() : this(-1, -1, false)
		{
		}
	}
}
