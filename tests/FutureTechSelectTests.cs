// CivOne tests
//
// Every completed Future Technology opened the choose-your-research dialog, and by then there
// is nothing to choose: Player.AvailableResearch yields real advances until they are all known
// and then yields Future Technology alone (Player.cs). So the last third of a long game asked
// the player to confirm a one-item menu once per completion, on top of the notice that already
// announces the discovery.
//
// TechSelect is the single place this is decided — ProcessScience, GetAdvance, City and
// AddAdvance all route through it — so the suppression lives there and covers every path.
//
// Narrow on purpose: a menu that merely CONTAINS Future Tech (the post-contact branch can put
// a real advance beside it) is still a choice and is still asked.

using System.Linq;
using CivOne.Advances;
using CivOne.Tasks;

namespace CivOne.Tests
{
	public class FutureTechSelectTests
	{
		// Everything terrestrial known, so AvailableResearch has only Future Tech left. Post-
		// contact advances are gated on having met the visitors, which has not happened here.
		private static Player AFullyResearchedHuman()
		{
			Sim.NewGame(width: 40, height: 30, competition: 4);
			Player human = Game.Instance.HumanPlayer;
			foreach (IAdvance advance in Common.Advances.Where(a => !(a is FutureTech)))
				human.AddAdvance(advance, false);
			human.CurrentResearch = null;
			human.Science = 1;            // TechSelect ignores a civ doing no research at all
			Sim.ClearTasks();
			return human;
		}

		// The fixture has to have run out of choices, or the test below proves nothing.
		[Fact]
		public void ThereIsNothingLeftToChoose()
		{
			Player human = AFullyResearchedHuman();

			IAdvance[] available = human.AvailableResearch.ToArray();

			Assert.Single(available);
			Assert.IsType<FutureTech>(available[0]);
		}

		// Counted before and after rather than asserted absent: Common._screens is static and
		// nothing clears it between tests (Sim.ResetState drains the task queue but not the
		// screen list), so a ChooseTech left behind by another test would fail an absence check
		// that has nothing to do with this one.
		private static int OpenDialogs() => Common.Screens.Count(s => s is CivOne.Screens.ChooseTech);

		[Fact]
		public void FutureTechIsTakenWithoutAskingThePlayer()
		{
			Player human = AFullyResearchedHuman();
			int before = OpenDialogs();

			new TechSelect(human).Run();

			Assert.IsType<FutureTech>(human.CurrentResearch);
			Assert.Equal(before, OpenDialogs());
		}

		// The half that fails if the suppression is written as "skip the dialog whenever Future
		// Tech is on the menu": an ordinary game still asks, and asking means CurrentResearch
		// stays unset until the player picks.
		[Fact]
		public void AnOrdinaryChoiceStillAsks()
		{
			Sim.NewGame(width: 40, height: 30, competition: 4);
			Player human = Game.Instance.HumanPlayer;
			human.CurrentResearch = null;
			human.Science = 1;
			Sim.ClearTasks();

			Assert.True(human.AvailableResearch.Count() > 1, "fixture: this civ has no choice to make");
			int before = OpenDialogs();

			new TechSelect(human).Run();

			Assert.Equal(before + 1, OpenDialogs());
			Assert.Null(human.CurrentResearch);
		}
	}
}
