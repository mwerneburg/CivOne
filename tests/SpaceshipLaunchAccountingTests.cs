// CivOne tests
//
// What happens to ship parts once the ship has gone.
//
// ClearSpaceShipProduction switched each city's CURRENT production away from ship parts but
// left the production QUEUE untouched, and the counter increment in City.NewTurn never asked
// whether the ship had launched. So cities went on finishing parts for a vessel already under
// way: wasted shields, and counters that no longer describe the hull that flew.
//
// The evidence was a hull that could not exist. In a finished run the Romans launched on turn
// 675 and ended the game holding 42 structurals with 16 components and 8 modules — a
// combination that requires 43. It was not a rounding error in the launch test; the parts
// arrived afterwards.
//
// Also here: the ship-budget rescale. SpaceshipTarget budgeted the empire's ENTIRE shield
// output over the horizon, which no civ ever spends on a ship — the ordinary production chain
// keeps winning most of those choices. Three civs aimed at the maximum hull and missed it on
// the run after the target shipped, so the budget is now a share of output.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class SpaceshipLaunchAccountingTests
	{
		private static (Game g, Player p, City c) ALaunchedCiv()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 15);
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 8;
			p.AddAdvance(new Advances.SpaceFlight(), false);
			Sim.ClearTasks();
			return (g, p, c);
		}

		// A part finished after launch must not move the counter.
		[Fact]
		public void APartFinishedAfterLaunchIsNotCounted()
		{
			(Game g, Player p, City c) = ALaunchedCiv();
			byte me = g.PlayerNumber(p);
			g.Progress(me).SpaceshipStructural = 20;
			// A real turn number, not g.GameTurn: a fresh game is on turn 0, and 0 is exactly
			// the value that MEANS "not launched". Setting it from GameTurn left the ship on
			// the pad and the test failed against correct code.
			g.Progress(me).SpaceshipLaunchTurn = 400;               // she has sailed

			c.SetProduction(new SSStructural());
			c.Shields = (short)((int)new SSStructural().Price * 10);
			c.NewTurn();
			Sim.Settle();

			Assert.Equal(20, g.Progress(me).SpaceshipStructural);
		}

		// ...and before launch it still is, or this guard has broken the whole mechanic.
		[Fact]
		public void APartFinishedBeforeLaunchStillCounts()
		{
			(Game g, Player p, City c) = ALaunchedCiv();
			byte me = g.PlayerNumber(p);
			g.Progress(me).SpaceshipStructural = 20;
			Assert.Equal(0, g.Progress(me).SpaceshipLaunchTurn);

			c.SetProduction(new SSStructural());
			c.Shields = (short)((int)new SSStructural().Price * 10);
			c.NewTurn();
			Sim.Settle();

			Assert.Equal(21, g.Progress(me).SpaceshipStructural);
		}

		// The shields should not be spent at all: the queue is emptied of parts at launch, so
		// no city carries on building one. Clearing only CurrentProduction left the queue to
		// feed the city another part next turn.
		[Fact]
		public void LaunchEmptiesQueuedShipParts()
		{
			(Game g, Player p, City c) = ALaunchedCiv();
			c.SetProduction(new SSStructural());
			c.EnqueueProduction(new SSComponent());
			c.EnqueueProduction(new SSModule());
			c.EnqueueProduction(new Barracks());

			g.ClearSpaceShipProduction(g.PlayerNumber(p));

			Assert.DoesNotContain(c.ProductionQueue, x => x is ISpaceShip);
			Assert.False(c.CurrentProduction is ISpaceShip);
			// ...and it leaves everything else in the queue alone.
			Assert.Contains(c.ProductionQueue, x => x is Barracks);
		}

		// ── the budget share ─────────────────────────────────────────────────────

		// The rescale, stated as the invariant it exists for: the hull a civ commits to must
		// cost less than the share of its output that actually reaches a ship. Asserted at a
		// third, which is what the constant says — an assertion against the full output would
		// pass under the old, wrong budget too.
		[Theory]
		[InlineData(3, 6)]
		[InlineData(6, 12)]
		[InlineData(12, 20)]
		public void TheTargetFitsTheShareOfOutputThatReachesTheShip(int cities, int size)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 15);
			for (int y = 18; y <= 32; y++)
			for (int x = 28; x <= 52; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			for (int i = 0; i < cities; i++)
				g.AddCity(p, (byte)i, 30 + i * 2, 25)!.Size = (byte)size;
			Sim.ClearTasks();

			(int comp, int module) = AI.Instance(p).SpaceshipTarget();
			int cost = Game.SpaceshipStructuresNeeded(comp, module) * new SSStructural().Price * 10
			         + comp   * new SSComponent().Price * 10
			         + module * new SSModule().Price * 10;
			int perTurn = p.Cities.Sum(c => System.Math.Max(0, c.ShieldIncome));

			// The minimum hull is the floor and may be unaffordable — there is nothing smaller
			// to aim at. Anything above the floor has to fit the realistic budget.
			if (comp > 2 || module > 3)
				Assert.True(cost <= perTurn * 100 / 3,
					$"a {comp}/{module} hull costs {cost} on {perTurn} shields/turn — more than a third of output over 100 turns");
		}
	}
}
