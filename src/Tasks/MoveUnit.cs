// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tasks
{
	[Fast]
	public class MoveUnit : GameTask
	{
		private const int STEP_SIZE = 1;

		public readonly int RelX, RelY;

		private int _step = 1;
		private readonly bool _animate;

		public int X { get; private set; }
		public int Y { get; private set; }

		public IUnit ActiveUnit { get; private set; }

		protected override bool Step()
		{
			// Undrawn moves (AI units with Enemy Moves off, or units moving in fog) skip the
			// 16-tick slide entirely: jump the sprite to its destination and finish in one
			// step. This is the bulk of the late-game between-turns pause — hundreds of AI
			// units each spending ~0.27s animating a sprite nobody sees.
			if (!_animate)
			{
				X = RelX * 16;
				Y = RelY * 16;
				EndTask();
				return true;
			}
			_step += STEP_SIZE;
			X = (RelX * _step);
			Y = (RelY * _step);
			if (_step <= 16)
				return true;
			EndTask();
			return true;
		}

		public override void Run()
		{
		}

		internal ITile TargetTile => ActiveUnit.Tile[RelX, RelY];

		public MoveUnit(int relX, int relY, bool animate = true)
		{
			RelX = relX;
			RelY = relY;
			ActiveUnit = Game.ActiveUnit!;
			_animate = animate;
		}
	}
}