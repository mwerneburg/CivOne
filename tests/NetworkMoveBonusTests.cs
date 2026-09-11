// CivOne tests
//
// Only land units ride the network.
//
// Reported from a game: a Dirigible was crossing railways and sea tubes for free. The cause
// was in BaseUnit.MovementDone, which granted a free step whenever both tiles carried rail or
// tube — and that method is reached by exactly the units that should never get it. Land units
// override it (BaseUnitLand does its own connected-tile accounting, cities as waypoints and
// all), so the concession only ever applied to ships, aircraft and the Dirigible.
//
// A sea tube is an OCEAN tile, which is what made it bite so widely: every ship crossing a
// tube line moved free, including the Olvir hydro engineers laying the things.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class NetworkMoveBonusTests : System.IDisposable
	{
		public void Dispose() => Sim.ClearTasks();

		// A strip at y=25 carrying whatever the caller asks for, on whatever terrain.
		private static (Game game, byte num) AStrip(Terrain terrain, bool rail = false, bool tube = false)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int x = 38; x <= 44; x++)
			{
				Map.Instance.ChangeTileType(x, 25, terrain);
				Map.Instance[x, 25].RailRoad = rail;
				Map.Instance[x, 25].TransportTube = tube;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			byte num = g.PlayerNumber(g.HumanPlayer);
			g.HumanPlayer.Explore(41, 25, range: 10);
			Sim.ClearTasks();
			return (g, num);
		}

		// The move is animated for the human, and an animation does not complete headless —
		// so drive MovementDone the way the game does and then settle the queue.
		private static int MovesAfterOneStep(IUnit unit)
		{
			unit.MovesLeft = unit.Move;
			int fromX = unit.X;
			Assert.True(unit.MoveTo(1, 0), "fixture: the move was refused outright");
			Sim.Settle();
			// Without this the "free ride" assertions are unfalsifiable: a move that never
			// completes leaves MovesLeft untouched, which is indistinguishable from a step
			// that cost nothing. Settle drops tasks that park, so the unit HAS to have
			// arrived for the number below to mean anything.
			Assert.Equal(fromX + 1, unit.X);
			return unit.MovesLeft;
		}

		// The report.
		[Fact]
		public void ADirigibleBuysNoFreeRideFromARailway()
		{
			(Game g, byte num) = AStrip(Terrain.Grassland1, rail: true);
			IUnit airship = g.CreateUnit(UnitType.Dirigible, 40, 25, num)!;

			Assert.Equal(airship.Move - 1, MovesAfterOneStep(airship));
		}

		// ...nor from a sea tube, which is the version it would actually meet: the Dirigible
		// exists to cross tube lines it has no right to enter.
		[Fact]
		public void ADirigibleBuysNoFreeRideFromASeaTube()
		{
			(Game g, byte num) = AStrip(Terrain.Ocean, tube: true);
			IUnit airship = g.CreateUnit(UnitType.Dirigible, 40, 25, num)!;

			Assert.Equal(airship.Move - 1, MovesAfterOneStep(airship));
		}

		// The same fault, one hull over: a sea tube is an ocean tile, so every ship crossing
		// one was moving free.
		[Fact]
		public void AShipBuysNoFreeRideFromASeaTube()
		{
			(Game g, byte num) = AStrip(Terrain.Ocean, tube: true);
			IUnit ship = g.CreateUnit(UnitType.Trireme, 40, 25, num)!;

			Assert.Equal(ship.Move - 1, MovesAfterOneStep(ship));
		}

		// And the half that must NOT change: a land unit on rail still rides for free. That
		// rule lives in BaseUnitLand and is the whole point of building a railway.
		[Fact]
		public void ALandUnitStillRidesTheRailwayForFree()
		{
			(Game g, byte num) = AStrip(Terrain.Grassland1, rail: true);
			IUnit soldier = g.CreateUnit(UnitType.Musketeers, 40, 25, num)!;

			Assert.Equal(soldier.Move, MovesAfterOneStep(soldier));
		}
	}
}
