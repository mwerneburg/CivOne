// CivOne tests
//
// The two pieces of the sweep harness that would fail SILENTLY.
//
// `GameDecided` reads a private static by reflection: a renamed field makes it return false
// forever, and a sweep would then play every decided game on to the turn cap — an hour of
// nothing per run on an epic map, with no error anywhere.
//
// The Earth staging copies resources/earth_epic.bin into the run's data directory because
// Map.ResolveEarthBin's other two candidates are relative to the executable and a test binary
// does not live where they expect. If that copy stopped working the harness would quietly
// fall back to... nothing, and the sweep would be measuring a different planet in every run —
// which is the one thing the whole design is meant to prevent.

using System.Linq;
using System.Reflection;

namespace CivOne.Tests
{
	public class SweepHarnessTests
	{
		// The reflection target exists and tracks the game, rather than reading false always.
		[Fact]
		public void GameDecidedFollowsTheLoggersLifecycle()
		{
			var field = typeof(DecisionLogger).GetField("_active",
				BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(field);   // renamed field = a stop condition that never fires

			Sim.NewGame(width: 40, height: 30, competition: 3);
			Assert.False(Sim.GameDecided(), "a game that just started is not over");

			DecisionLogger.EndGame(0, "Test", humanWon: false, turns: 1);

			Assert.True(Sim.GameDecided(), "the run did not notice the game ending");
		}

		// Earth loads, at the size that makes it Earth. A sweep that silently generated a
		// random world instead would vary the planet between runs, which is exactly the
		// confound the map option exists to remove.
		[Fact]
		public void TheEpicEarthBoardLoads()
		{
			Sim.NewGame(competition: 4, map: "earth-epic");

			Assert.Equal(320, Map.WIDTH);
			Assert.Equal(200, Map.HEIGHT);
			Assert.True(Map.Instance.Ready, "the board never finished loading");
			// Land, and a sane amount of it — a failed load leaves an all-ocean board, which
			// would still be 320x200 and would still start a game.
			int land = Map.Instance.AllTiles().Count(t => t is not null && !t.IsOcean);
			Assert.True(land > 320 * 200 / 10, $"only {land} land tiles; this is not Earth");
		}

		// ...and the same for the standard board, which is the other size the sweep offers.
		[Fact]
		public void TheStandardEarthBoardLoads()
		{
			Sim.NewGame(competition: 4, map: "earth-standard");

			Assert.Equal(80, Map.WIDTH);
			Assert.Equal(50, Map.HEIGHT);
			int land = Map.Instance.AllTiles().Count(t => t is not null && !t.IsOcean);
			Assert.True(land > 80 * 50 / 10, $"only {land} land tiles; this is not Earth");
		}

		// An unknown map name is a typo in a sweep script, and a typo must not quietly hand
		// back a generated world halfway through a batch.
		[Fact]
		public void AnUnknownMapNameIsRefused()
		{
			Assert.Throws<System.ArgumentException>(() => Sim.NewGame(map: "earth-epicc"));
		}
	}
}
