// CivOne tests
//
// A failed GoTo search is remembered for the rest of the turn (Common._deadEnds). A failed
// search explores everything it can reach before giving up, and a stack shares its start and
// goal, so nineteen English units parked in Indian country ran the same futile search every
// turn once the planner learned the trespass rule (NeutralGroundRouteTests).
//
// What it may and may not remember: an AI unit waits a turn for a way that opens mid-turn; the
// human's own orders are always searched afresh; a tube or floating city clears it at once
// (TubeWayfindingTests.LayingTheLastTileOpensTheRouteAtOnce).

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class DeadEndMemoTests : System.IDisposable
	{
		private readonly bool _auto;
		public DeadEndMemoTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// Two fields joined by one tile at (40,25), held by a neutral's unit.
		private static (Game g, IUnit blocker) ABridgeHeldByANeutral()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 28; x <= 52; x++)
				Map.Instance.ChangeTileType(x, y, x == 40 && y != 25 ? Terrain.Ocean : Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			foreach (IUnit u in g.GetUnits().ToArray()) g.DisbandUnit(u);
			Player neutral = g.Players.Last(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			IUnit blocker = g.CreateUnit(UnitType.Musketeers, 40, 25, g.PlayerNumber(neutral))!;
			Sim.ClearTasks();
			return (g, blocker);
		}

		private static IUnit Mover(Game g, Player p) => g.CreateUnit(UnitType.Musketeers, 35, 25, g.PlayerNumber(p))!;

		[Fact]
		public void AnAiUnitWaitsATurnForAWayThatOpensMidTurn()
		{
			(Game g, IUnit blocker) = ABridgeHeldByANeutral();
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer && g.PlayerNumber(p) != blocker.Owner);
			IUnit unit = Mover(g, ai);
			Assert.Null(Common.GotoStep(unit, 45, 25));

			blocker.X = 30; blocker.Y = 18;   // the bridge clears
			Assert.Null(Common.GotoStep(unit, 45, 25));   // remembered for this turn

			g.GameTurn++;
			Assert.NotNull(Common.GotoStep(unit, 45, 25));
		}

		[Fact]
		public void TheHumansOwnOrdersAreAlwaysSearchedAfresh()
		{
			(Game g, IUnit blocker) = ABridgeHeldByANeutral();
			IUnit unit = Mover(g, g.HumanPlayer);
			Assert.Null(Common.GotoStep(unit, 45, 25));

			blocker.X = 30; blocker.Y = 18;

			Assert.NotNull(Common.GotoStep(unit, 45, 25));
		}
	}
}
