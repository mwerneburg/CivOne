// CivOne tests
//
// The garrison of a city built on water is not a shipwreck.
//
// The AI had one rule for a land unit standing on an ocean tile: it must have just disembarked,
// so step it ashore. A coastal city can BE an ocean tile, and a land unit in one is its
// garrison — so the rule marched it out every turn, AssignMission sent it back in, and neither
// leg cost anything, because a step between a railed tile and a city is free (cities are rail
// waypoints, BaseUnitLand.MovementDone). A unit that never spends a move is never done, and the
// turn never ends.
//
// Measured in a 17-civ run stopped at turn 487 (1937 AD) with the process at 100% CPU and a
// window that could not repaint: one Ottoman Knights bounced between (176,27) and its own city
// of Quierzy 288,641 times in eighty seconds, MovesLeft pinned at 2 the whole way. Loading that
// autosave and driving the turn loop reproduced it in seventy seconds every time; with the two
// fixes here the same turn completes in nine seconds.

using System.Linq;
using System.Drawing;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class OceanCityGarrisonTests
	{
		// A city on water with a railed land tile beside it — the exact pair whose step costs
		// nothing — and a land unit garrisoning the city.
		private static (Game game, Player ai, City city, IUnit garrison) AnOceanCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			// The water the city stands on, and nothing else wet nearby, so a unit stepping
			// ashore has only the one place to go.
			Map.Instance.ChangeTileType(40, 25, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Government = new Monarchy();
			ai.Explore(40, 25, range: 15);

			City c = g.AddCity(ai, 0, 40, 25)!;
			c.Size = 4;
			Assert.True(Map.Instance[40, 25].IsOcean, "fixture: the city must stand on water");

			// Rail on the landward tile: this is what makes the step free in both directions.
			Map.Instance[41, 25].RailRoad = true;

			g.CreateUnit(UnitType.Knights, 40, 25, g.PlayerNumber(ai), false);
			IUnit garrison = g.GetUnits().First(u => u is Knights && u.Owner == g.PlayerNumber(ai));
			garrison.SetHome(c);
			Sim.ClearTasks();
			return (g, ai, c, garrison);
		}

		private static void Move(Player ai, IUnit unit)
		{
			AI.Instance(ai).Move(unit);
			Sim.Settle();
		}

		// The defect, stated directly: the garrison must not be walked out of its own city as
		// though it had washed up there.
		[Fact]
		public void TheGarrisonOfAWaterCityIsNotMarchedAshore()
		{
			(Game g, Player ai, City c, IUnit garrison) = AnOceanCity();
			int x = garrison.X, y = garrison.Y;

			Move(ai, garrison);

			Assert.True(garrison.X == x && garrison.Y == y,
				$"the garrison was walked from ({x},{y}) to ({garrison.X},{garrison.Y})");
		}

		// There is deliberately no test here demanding that a move cost something. The free
		// rail step is the rule working: cities are rail waypoints and always have been, in
		// this game and in the original. A test asserting otherwise passed against the broken
		// code and would have to be deleted the first time anyone read it properly — the
		// defect was the oscillation, not the free move it rode on.

		// A unit genuinely stranded on open water — no city, no tube — is still walked ashore.
		// Removing the disembark rule instead of narrowing it would leave those units adrift.
		[Fact]
		public void ATrulyStrandedUnitStillStepsAshore()
		{
			(Game g, Player ai, City c, IUnit garrison) = AnOceanCity();
			// Open water beside the coast, with the city out of the way.
			Map.Instance.ChangeTileType(38, 25, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			g.CreateUnit(UnitType.Cavalry, 38, 25, g.PlayerNumber(ai), false);
			IUnit castaway = g.GetUnits().First(u => u is Cavalry && u.Owner == g.PlayerNumber(ai));
			Assert.True(castaway.Tile.IsOcean && castaway.Tile.City is null, "fixture: open water");
			Sim.ClearTasks();

			Move(ai, castaway);

			Assert.False(castaway.Tile.IsOcean,
				$"the castaway is still at sea at ({castaway.X},{castaway.Y})");
		}

		// The circuit-breaker behind the fix: whatever a mission decides, the AI will not walk a
		// unit back onto ground it has already left this turn. The garrison bug was one way to
		// produce a free oscillation; the same shape appeared in the same save with a Chinese
		// HydroEngineer stepping between two tiles, and it only stopped because that pair cost
		// movement. On rails nothing stops it, so the refusal is stated once, here.
		[Fact]
		public void TheAiWillNotWalkAUnitBackOntoTilesItHasLeft()
		{
			(Game g, Player ai, City c, IUnit garrison) = AnOceanCity();

			// Send it ashore under its own steam, so the tile it leaves is recorded.
			garrison.Goto = new Point(41, 25);
			Move(ai, garrison);
			Assert.True(garrison.X == 41 && garrison.Y == 25,
				$"fixture: it did not make the first step, it is at ({garrison.X},{garrison.Y})");

			// ...then order it straight back.
			garrison.Goto = new Point(40, 25);
			Move(ai, garrison);

			Assert.True(garrison.X == 41 && garrison.Y == 25,
				"it walked back onto a tile it had already left this turn");
		}
	}
}
