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
using CivOne.Advances;
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

		// The human has to research. Headless there is no screen to answer, so its
		// CurrentResearch stayed null for entire games: at turn 688 of a sweep run the human
		// held 63 cities — the largest empire in that world — and TWO advances, against 44 to
		// 84 for every AI. The cost was not to the human but to the WORLD: a quarter of its
		// cities built no Observatory, so the SETI signal, the visitors, the exotic fuel and
		// the spaceship all came late or never, and Cultural Ascendancy won 10 of 14 games
		// against a rival the test rig had crippled.
		[Fact]
		public void TheHumanKeepsResearchingWithNobodyToAskIt()
		{
			Sim.NewGame(width: 40, height: 30, competition: 4);
			Player human = Game.Instance.HumanPlayer;
			int before = human.Advances.Length;
			human.CurrentResearch = null;

			Sim.KeepHumanResearching();

			Assert.NotNull(human.CurrentResearch);
			Assert.Equal(before, human.Advances.Length);   // chosen, not granted
		}

		// ...and it moves ON when one completes. AddAdvance banks the advance but leaves
		// CurrentResearch pointing at it, relying on a TechSelect screen to pick the next —
		// which never runs here, so the same technology completed turn after turn. Measured at
		// turn 682: the human held 171 advances of which 83 were distinct, every one banked
		// twice, against 83 of 83 for every AI. Half a fix made the harness human the strongest
		// civilization in the world, which is worse for a sweep than leaving it in the stone age.
		[Fact]
		public void TheHumanDoesNotResearchTheSameAdvanceTwice()
		{
			Sim.NewGame(width: 40, height: 30, competition: 4);
			Player human = Game.Instance.HumanPlayer;
			Sim.KeepHumanResearching();
			IAdvance chosen = human.CurrentResearch!;
			Assert.NotNull(chosen);

			// Complete it the way the game does, leaving CurrentResearch where AddAdvance does.
			human.AddAdvance(chosen, false);
			human.CurrentResearch = chosen;

			Sim.KeepHumanResearching();

			Assert.True(human.CurrentResearch is null || human.CurrentResearch.Id != chosen.Id,
				$"still researching {chosen.Name}, which it already knows");
		}

		// ...and the sweep actually calls it. The helper working is no use if the run loop
		// never reaches for it, and that is precisely how the bug went unnoticed.
		[Fact]
		public void TheHarnessAsksEveryTurn()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(Sim.RepoRoot(), "tests", "AutoplayHarness.cs"));
			int at = src.IndexOf("Sim.RunTurns(");
			Assert.True(at > 0, "the harness turn loop has moved");

			Assert.Contains("Sim.KeepHumanResearching()", src.Substring(0, at + 400));
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
