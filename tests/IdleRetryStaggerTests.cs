// CivOne tests
//
// Idle units re-probe for a target one turn in eight rather than every turn. The stagger
// key was (X + Y), which is stable per TILE, not per unit — so every idle unit parked in
// the same city retried on the same turn. At turn 708 of the 2026-08-03 run that showed
// as 14.3s of an 18.5s turn spent in A*, with every spike landing on GameTurn & 7 == 4
// and the other seven residues running 0.4-0.6s.

using System.Collections.Generic;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class IdleRetryStaggerTests
	{
		// A caravan with no Goto, not adjacent to a foreign city, is the deferrable case.
		private static (Player owner, List<IUnit> units) AStackOfIdleUnits(int count)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			p.Explore(40, 25, range: 6);

			var units = new List<IUnit>();
			for (int i = 0; i < count; i++)
				units.Add(g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(p))!);
			Sim.ClearTasks();
			return (p, units);
		}

		// The regression: units on ONE tile must not all come due on the same turn.
		[Fact]
		public void IdleUnitsStackedOnOneTile_DoNotAllRetryOnTheSameTurn()
		{
			var (owner, units) = AStackOfIdleUnits(24);
			AI ai = AI.Instance(owner);

			// For each turn in the cycle, how many of the stack are due to re-probe?
			var dueByTurn = new int[8];
			for (ushort turn = 0; turn < 8; turn++)
			{
				Game.Instance.GameTurn = turn;
				dueByTurn[turn] = units.Count(u => !ai.TestIdleRetryDeferred(u));
			}

			Assert.Equal(24, dueByTurn.Sum());          // every unit comes due exactly once
			Assert.True(dueByTurn.Count(n => n > 0) >= 4,
				$"stack bunched onto too few turns: [{string.Join(", ", dueByTurn)}]");
			Assert.True(dueByTurn.Max() <= 12,
				$"one turn carries too much of the stack: [{string.Join(", ", dueByTurn)}]");
		}

		// Each unit must come due exactly once per eight turns — not never (it would stop
		// looking for work) and not every turn (that is the cost the stagger exists to avoid).
		[Fact]
		public void EachIdleUnit_ComesDueExactlyOnceInEight()
		{
			var (owner, units) = AStackOfIdleUnits(8);
			AI ai = AI.Instance(owner);

			foreach (IUnit u in units)
			{
				int due = 0;
				for (ushort turn = 0; turn < 8; turn++)
				{
					Game.Instance.GameTurn = turn;
					if (!ai.TestIdleRetryDeferred(u)) due++;
				}
				Assert.Equal(1, due);
			}
		}

		// The stagger must not drift: the same unit answers the same way on the same turn.
		[Fact]
		public void AUnitsSlotIsStableAcrossTurns()
		{
			var (owner, units) = AStackOfIdleUnits(4);
			AI ai = AI.Instance(owner);
			IUnit u = units[0];

			Game.Instance.GameTurn = 3;
			bool first = ai.TestIdleRetryDeferred(u);
			Game.Instance.GameTurn = 11;   // same residue, one cycle later
			Assert.Equal(first, ai.TestIdleRetryDeferred(u));
			Game.Instance.GameTurn = 19;
			Assert.Equal(first, ai.TestIdleRetryDeferred(u));
		}
	}
}
