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

		// A rail or tube step costs no movement point (BaseUnit.MovementDone), so a unit can
		// cross a continent in one turn — and at 16 ticks a tile a trans-oceanic tube line
		// took the better part of a minute to watch. The slide exists to show a move; it does
		// not need to show every one of forty at the same pace.
		//
		// Deliberately the SAME condition the cost rule uses, not a looser one: the step is
		// animated fast exactly when it is free. Anything else and the animation would be
		// telling the player something the rules do not.
		private const int RAIL_STEP_SIZE = 4;

		// Chosen at construction from the two tiles, so the caller does not have to know the
		// tick budget. Mirrors BaseUnit.MovementDone's railRailMove.
		internal static int StepSizeFor(ITile from, ITile to)
			=> from is not null && to is not null
			&& (from.RailRoad || from.TransportTube)
			&& (to.RailRoad || to.TransportTube)
				? RAIL_STEP_SIZE : STEP_SIZE;

		private readonly int _stepSize;

		public readonly int RelX, RelY;

		private int _step = 1;
		private readonly bool _animate;

		public int X { get; private set; }
		public int Y { get; private set; }

		public IUnit ActiveUnit { get; private set; }

		// True while this move is being drawn. An unanimated move (an AI unit nobody can see)
		// finishes in a single step and has no frames to protect.
		internal bool Animating => _animate;

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
			_step += _stepSize;
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

		public MoveUnit(int relX, int relY, bool animate = true, int stepSize = STEP_SIZE)
		{
			RelX = relX;
			RelY = relY;
			ActiveUnit = Game.ActiveUnit!;
			_animate = animate;
			_stepSize = stepSize < 1 ? STEP_SIZE : stepSize;
		}
	}
}