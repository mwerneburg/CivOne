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

namespace CivOne.Tests
{
	public class RailAnimationSpeedTests
	{
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
