// CivOne tests
//
// A transport tube is a walkable bridge, and crossing one must not cost a land unit its
// whole turn.
//
// BaseUnitLand.MovementDone opened with "if (previousTile.IsOcean || Tile.IsOcean) { MovesLeft
// = 0; PartMoves = 0; }" — the rule that ends your turn when you board or leave a ship. A tube
// IS an ocean tile, so stepping onto one from the road wiped the movement that the
// connected-tile logic further down had just granted for free. Reported from play as
// "following a road or rail to a point where a tube started and I would be stopped": the unit
// crossed one tube tile per turn.
//
// ValidMoveTarget already calls tubes and floating cities "walkable bridges for land units".
// This pins the same exemption on the movement cost.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class TubeCrossingTests
	{
		// A road running east to the shore at x=43, then tubed ocean at 44 and 45.
		private static (Game g, Player p, IUnit u) AShorelineWithATube()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 43; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			for (int y = 20; y <= 30; y++)
			for (int x = 44; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(43, 25, range: 20);

			for (int x = 40; x <= 43; x++) Map.Instance[x, 25].Road = true;
			Map.Instance[44, 25].TransportTube = true;
			Map.Instance[45, 25].TransportTube = true;

			g.CreateUnit(UnitType.Militia, 43, 25, g.PlayerNumber(p), false);
			// By POSITION, not just owner: Sim.NewGame already gave this civ starting units
			// elsewhere on the map, and First(owner) picked one of those instead.
			IUnit u = g.GetUnits().First(x => x.Owner == g.PlayerNumber(p) && x.X == 43 && x.Y == 25);
			Sim.ClearTasks();
			return (g, p, u);
		}

		// The report, directly: walking off the road onto the tube must not end the turn.
		//
		// THREE moves, not one. With a single move the fallthrough sets PartMoves = 2 whether
		// or not the turn was zeroed first, so both the fixed and the broken code end at
		// 0 moves / 2 part-moves and the test cannot tell them apart — it passed against the
		// bug it was written for. The zeroing only shows when there was something to lose.
		[Fact]
		public void SteppingFromRoadOntoATubeDoesNotEndTheTurn()
		{
			(Game g, Player p, IUnit u) = AShorelineWithATube();
			u.MovesLeft = 3;

			Assert.True(u.MoveTo(1, 0), "the move onto the tube should be legal");
			Sim.Settle();

			Assert.Equal(44, u.X);
			Assert.True(u.MovesLeft >= 2,
				$"stepping onto a tube cost the whole turn: {u.MovesLeft} moves left of 3");
		}

		// ...and a unit already on the tube keeps going, rather than one tile a turn.
		[Fact]
		public void ATubeCanBeCrossedMoreThanOneTilePerTurn()
		{
			(Game g, Player p, IUnit u) = AShorelineWithATube();
			u.MovesLeft = 3;

			u.MoveTo(1, 0); Sim.Settle();   // onto the first tube tile
			int after = u.X;
			int leftOnTube = u.MovesLeft;
			u.MoveTo(1, 0); Sim.Settle();   // and on along it

			Assert.Equal(44, after);
			Assert.Equal(45, u.X);
			// Tube-to-tube is a rail move and costs nothing, so the unit still has whatever
			// the road step left it — and crucially it was never zeroed on arrival.
			Assert.True(leftOnTube >= 2, $"arriving on the tube left only {leftOnTube} moves");
			Assert.True(u.MovesLeft >= 2, $"crossing the tube left only {u.MovesLeft} moves");
		}

		// The rule this exemption must NOT break: stepping into open water — boarding a ship —
		// still ends the turn.
		[Fact]
		public void BoardingAShipStillEndsTheTurn()
		{
			(Game g, Player p, IUnit u) = AShorelineWithATube();
			Map.Instance[44, 25].TransportTube = false;      // plain sea again
			g.CreateUnit(UnitType.Trireme, 44, 25, g.PlayerNumber(p), false);
			foreach (IUnit boat in g.GetUnits().Where(x => x.X == 44 && x.Y == 25)) boat.Sentry = false;
			u.MovesLeft = 1;

			Assert.True(u.MoveTo(1, 0), "boarding the ship should be legal");
			Sim.Settle();

			Assert.Equal(0, u.MovesLeft);
			Assert.Equal(0, u.PartMoves);
		}
	}
}
