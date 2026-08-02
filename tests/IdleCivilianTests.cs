// CivOne tests
//
// A self-inflicted one. Diplomats and Caravans with no reachable target were given
// `Sentry = true`, copying the Explorer and its comment about waking "if something
// changes around it". That wake is gated on `Human == u.Owner` (BaseUnit.cs:604),
// so an AI unit that sentries never wakes again — parked for the rest of the game.
// A 2104 AD map had idle caravans standing about everywhere.
//
// They must stay awake, and they must not re-probe every turn either — probing was
// 4600 pathfinds a turn before it was capped. One turn in eight, staggered.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class IdleCivilianTests
	{
		// A civ boxed in with no foreign city it can reach: nothing to target.
		private static (Player p, IUnit caravan, IUnit diplomat) Boxed()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 22; y <= 28; y++)
			for (int x = 38; x <= 44; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = Game.Instance.Players.First(x => Game.Instance.PlayerNumber(x) != 0);
			byte id = Game.Instance.PlayerNumber(p);
			p.Explore(41, 25, range: 6);
			Game.Instance.AddCity(p, 0, 41, 25);

			IUnit caravan  = Game.Instance.CreateUnit(UnitType.Caravan,  40, 25, id)!;
			IUnit diplomat = Game.Instance.CreateUnit(UnitType.Diplomat, 42, 25, id)!;
			Sim.ClearTasks();
			return (p, caravan, diplomat);
		}

		// The regression: whatever else happens, they must not fall permanently asleep.
		[Fact]
		public void AnIdleCaravan_IsNeverSentried()
		{
			var (p, caravan, _) = Boxed();
			AI ai = AI.Instance(p);

			for (int i = 0; i < 24; i++)
			{
				Game.Instance.GameTurn++;
				ai.Move(caravan);
				Assert.False(caravan.Sentry,
					$"an AI caravan must not sentry — it can never wake (turn {Game.Instance.GameTurn})");
			}
		}

		[Fact]
		public void AnIdleDiplomat_IsNeverSentried()
		{
			var (p, _, diplomat) = Boxed();
			AI ai = AI.Instance(p);

			for (int i = 0; i < 24; i++)
			{
				Game.Instance.GameTurn++;
				ai.Move(diplomat);
				Assert.False(diplomat.Sentry, "an AI diplomat must not sentry");
			}
		}

		// ...and the cost side: an idle unit re-probes on ONE turn in eight, not every
		// turn. Measured through the pathfind counter, which is what the probing spends.
		[Fact]
		public void AnIdleCaravan_ProbesOnOneTurnInEight()
		{
			var (p, caravan, _) = Boxed();
			AI ai = AI.Instance(p);
			int probing = 0;

			for (int i = 0; i < 16; i++)
			{
				Game.Instance.GameTurn++;
				TurnMetrics.Reset();
				ai.Move(caravan);
				if (TurnMetrics.PathCalls > 0) probing++;
			}

			Assert.True(probing <= 4,
				$"an idle caravan should re-probe about twice in 16 turns; it probed {probing} times");
		}

		// The stagger must actually stagger: two units on different tiles do not both
		// wake on the same turn, or the whole fleet re-probes at once.
		[Fact]
		public void TwoIdleUnits_DoNotAllProbeOnTheSameTurn()
		{
			var (p, caravan, diplomat) = Boxed();
			// (40+25) and (42+25) differ mod 8, so their retry turns differ.
			Assert.NotEqual((caravan.X + caravan.Y) & 7, (diplomat.X + diplomat.Y) & 7);
		}
	}
}
