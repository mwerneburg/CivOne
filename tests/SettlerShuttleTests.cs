// CivOne tests
//
// Two settlers shuttling between Byblos and Leeds for the rest of the game, observed in the
// 1872 AD run. The mechanism is a target that is thrown away too eagerly:
//
//   ResolveMovementFailure returned ClearMissionWait for a settler on ANY blocked step, and
//   AssignMission then re-picks from wherever the unit now stands — but the site scan is a
//   +/-6 window CENTRED ON THE SETTLER that takes the nearest eligible tile. The window
//   travels with the unit, so the tile it just walked away from becomes the nearest one
//   again. Walk out, get blocked by one of the fifteen hundred units on a late-game map,
//   clear, re-pick the tile behind you, walk back, repeat.
//
// The fix keeps the target across a few failures. It must stay BOUNDED: a permanently
// unreachable target that is never released is a settler frozen for the rest of the game,
// which is a worse bug than the one being fixed. Both halves are asserted here.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SettlerShuttleTests
	{
		private static (Game game, Player ai, IUnit settler) AWorldWithASettler()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Explore(40, 25, range: 20);
			g.AddCity(ai, 0, 40, 25);
			IUnit settler = g.CreateUnit(UnitType.Settlers, 44, 25, g.PlayerNumber(ai))!;
			Sim.ClearTasks();
			return (g, ai, settler);
		}

		// A blocked step is not a reason to forget where you were going.
		[Fact]
		public void ABlockedSettlerKeepsItsTarget()
		{
			var (_, ai, settler) = AWorldWithASettler();

			Assert.True(AI.Instance(ai).TestKeepsTargetOnBlockedStep(settler),
				"clearing the target on the first block is what makes it shuttle");
		}

		// ...but not forever. A settler whose target can never be reached has to be released,
		// or the shuttle is traded for a statue.
		[Fact]
		public void ASettlerGivesUpOnATargetItCanNeverReach()
		{
			var (_, ai, settler) = AWorldWithASettler();
			AI hive = AI.Instance(ai);

			bool released = false;
			for (int attempt = 0; attempt < 20 && !released; attempt++)
				released = !hive.TestKeepsTargetOnBlockedStep(settler);

			Assert.True(released, "a permanently blocked settler must eventually choose afresh");
		}

		// Non-settlers are untouched: the retry budget is a settler rule, and military units
		// have their own resolution (stage and wait for reinforcements).
		[Fact]
		public void TheRetryBudgetIsASettlerRule()
		{
			var (g, ai, _) = AWorldWithASettler();
			IUnit cannon = g.CreateUnit(UnitType.Cannon, 44, 25, g.PlayerNumber(ai))!;
			AI hive = AI.Instance(ai);

			// Whatever a Cannon resolves to, it must not run out of settler retries — twenty
			// blocked steps in a row leave its behaviour exactly as it started.
			bool first = hive.TestKeepsTargetOnBlockedStep(cannon);
			for (int attempt = 0; attempt < 20; attempt++) hive.TestKeepsTargetOnBlockedStep(cannon);

			Assert.Equal(first, hive.TestKeepsTargetOnBlockedStep(cannon));
		}
	}
}
