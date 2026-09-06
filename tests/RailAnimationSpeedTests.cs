// CivOne tests
//
// A rail or tube step costs no movement point, so a unit can cross a continent in a single
// turn. At 16 ticks a tile the slide made a long tube line take the better part of a minute
// to sit through — and the human's own moves always animate: MoveIsVisible returns true for
// every unit the player owns, and Game.Animations gates city celebration/disorder art, not
// unit movement.
//
// The fast slide is tied to the SAME test the cost rule uses (BaseUnit.MovementDone's
// railRailMove). Free step, quick slide. If the two ever diverge the animation starts
// telling the player something the rules do not.

using CivOne;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;
using System.Linq;

namespace CivOne.Tests
{
	// IDisposable, because three of these tests start a REAL move to make MovingUnit
	// genuinely set — and a started move leaves a MoveUnit task in the static GameTask
	// queue. Left there it outlives the test. Cleared after each one.
	public class RailAnimationSpeedTests : System.IDisposable
	{
		public void Dispose() => Sim.ClearTasks();

		private static ITile Tile(int x, int y, Terrain t, bool rail = false, bool tube = false)
		{
			Map.Instance.ChangeTileType(x, y, t);
			ITile tile = Map.Instance[x, y];
			tile.RailRoad = rail;
			tile.TransportTube = tube;
			return tile;
		}

		[Fact]
		public void ARailToRailStepAnimatesFaster()
		{
			Sim.NewGame(width: 80, height: 50);
			ITile a = Tile(40, 25, Terrain.Grassland1, rail: true);
			ITile b = Tile(41, 25, Terrain.Grassland1, rail: true);

			Assert.True(MoveUnit.StepSizeFor(a, b) > MoveUnit.StepSizeFor(a, Tile(42, 25, Terrain.Grassland1)));
		}

		[Fact]
		public void ATubeToTubeStepAnimatesFaster()
		{
			Sim.NewGame(width: 80, height: 50);
			ITile a = Tile(40, 25, Terrain.Ocean, tube: true);
			ITile b = Tile(41, 25, Terrain.Ocean, tube: true);
			ITile plain = Tile(42, 25, Terrain.Grassland1);

			Assert.True(MoveUnit.StepSizeFor(a, b) > MoveUnit.StepSizeFor(a, plain));
		}

		// Stepping OFF the line is an ordinary move and costs a point, so it keeps the
		// ordinary slide. This is the half that fails if the condition is loosened to "either
		// tile has rail".
		[Fact]
		public void LeavingTheLineKeepsTheOrdinarySpeed()
		{
			Sim.NewGame(width: 80, height: 50);
			ITile onRail = Tile(40, 25, Terrain.Grassland1, rail: true);
			ITile offRail = Tile(41, 25, Terrain.Grassland1);

			Assert.Equal(1, MoveUnit.StepSizeFor(onRail, offRail));
			Assert.Equal(1, MoveUnit.StepSizeFor(offRail, onRail));
		}

		// ── the pacing root ──────────────────────────────────────────────────────────
		//
		// The tick budget is the other half of the problem. Even at 4 ticks a tile, a long
		// GoTo paces at 60 Hz because RuntimeHandler.FastForwarding — the mechanism that
		// already drops that pacing for AI turns — was refused to the human's own turn.
		// It now also covers a human unit in flight under GoTo.

		// A real move, not a faked flag: Moving derives from a live Movement task, and
		// MovingUnit is what the host loop actually reads. Game.ActiveUnit is null in a test
		// harness — its getter requires the unit's owner to be the CURRENT player, and a
		// fresh Sim game starts on slot 0 — so setting it proves nothing.
		private static IUnit AUnitInFlight(Player owner, bool withGoto)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.ChangeTileType(41, 25, Terrain.Grassland1);
			IUnit u = g.CreateUnit(UnitType.Militia, 40, 25, g.PlayerNumber(owner))!;
			Sim.ClearTasks();
			if (withGoto) u.Goto = new System.Drawing.Point(60, 25);
			u.MoveTo(1, 0);
			return u;
		}

		[Fact]
		public void AHumanUnitUnderGotoFastForwards()
		{
			Sim.NewGame(width: 80, height: 50);
			IUnit u = AUnitInFlight(Game.Instance.HumanPlayer, withGoto: true);

			Assert.True(u.Moving, "fixture: the unit is not actually mid-move");
			Assert.True(RuntimeHandler.HumanGotoInFlight);
		}

		// A unit the player is stepping by hand keeps 60 Hz. This is the half that fails if
		// the condition is widened to "a human unit is moving" — that would fast-forward every
		// step the player takes and lose the deliberate, turn-by-turn feel of a move.
		[Fact]
		public void AHumanUnitMovingByHandDoesNotFastForward()
		{
			Sim.NewGame(width: 80, height: 50);
			IUnit u = AUnitInFlight(Game.Instance.HumanPlayer, withGoto: false);

			Assert.True(u.Moving, "fixture: the unit is not actually mid-move");
			Assert.False(RuntimeHandler.HumanGotoInFlight);
		}

		// An AI unit is not the human's business; FastForwarding's CurrentPlayer test already
		// carries that case and this must not double-count it.
		[Fact]
		public void AnAiUnitIsNotHumanGotoInFlight()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			IUnit u = AUnitInFlight(ai, withGoto: true);

			Assert.True(u.Moving, "fixture: the unit is not actually mid-move");
			Assert.False(RuntimeHandler.HumanGotoInFlight);
		}

		// Open ground is untouched: this is not a general speed-up of the game's animation.
		[Fact]
		public void OrdinaryGroundIsUnchanged()
		{
			Sim.NewGame(width: 80, height: 50);
			ITile a = Tile(40, 25, Terrain.Grassland1);
			ITile b = Tile(41, 25, Terrain.Grassland1);

			Assert.Equal(1, MoveUnit.StepSizeFor(a, b));
		}
	}
}
