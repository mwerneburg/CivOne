// CivOne tests
//
// The Olvir never start a war, so when an AI civ attacks them they ask the human for help
// (the user, Oct 2026). Answering is a war on their behalf, and does not count as a war of
// the human's making.

using System.Linq;

namespace CivOne.Tests
{
	public class OlvirPleaTests : System.IDisposable
	{
		private readonly bool _auto;
		public OlvirPleaTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		private static (Game g, Player olvir, Player aggressor) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			Player olvir = new Player(Common.Civilizations.First(c => c is Civilizations.Olvir));
			g.AddPlayer(olvir);
			Player aggressor = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer && p != olvir);
			Sim.ClearTasks();
			return (g, olvir, aggressor);
		}

		private static bool Pleaded() => QueuedProbes().Contains("Show:OlvirPleaResponse");

		private static string[] QueuedProbes() =>
			((System.Collections.IList)typeof(GameTask).GetField("_tasks",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!)
				.Cast<GameTask>().Select(t => t.ProbeName).ToArray();

		[Fact]
		public void AnAttackOnTheOlvirBringsAPlea()
		{
			(Game _, Player olvir, Player aggressor) = AWorld();
			aggressor.DeclareWar(olvir);
			Assert.True(aggressor.IsAtWar(olvir), "fixture: the war did not start");
			Assert.True(Pleaded(), "the Olvir did not ask for help");
		}

		// Not when the human is the aggressor, and not when the human is already fighting them.
		[Fact]
		public void NoPleaToTheirOwnAttacker()
		{
			(Game g, Player olvir, Player _) = AWorld();
			g.HumanPlayer.DeclareWar(olvir);
			Assert.False(Pleaded());
		}

		[Fact]
		public void NoPleaWhenWeAreAlreadyAtWarWithTheAggressor()
		{
			(Game g, Player olvir, Player aggressor) = AWorld();
			g.HumanPlayer.DeclareWar(aggressor);
			Sim.ClearTasks();
			aggressor.DeclareWar(olvir);
			Assert.False(Pleaded());
		}

		// Answering it: at war with the aggressor, and not a war of our making.
		[Fact]
		public void AnsweringIsNotAWarOfOurMaking()
		{
			(Game g, Player olvir, Player aggressor) = AWorld();
			aggressor.DeclareWar(olvir);
			Player human = g.HumanPlayer;

			human.DeclareWarOnBehalfOf(aggressor);

			Assert.True(human.IsAtWar(aggressor));
			Assert.False(g.StartedWarWith(g.PlayerNumber(human), g.PlayerNumber(aggressor)),
				"answering the Olvir counted as a war we started");
		}
	}
}
