// CivOne tests
//
// City.NewTurn enqueues a We-Love-the-King notification as Show.EventArt("welovethekingday"),
// which builds an EventArtScreen. Show.DropAllWeLovePresidentDay — the guard that exists to
// stop those piling up — filtered on WeLovePresidentDayScreen, a different class produced by
// a second, unused implementation. The purge could never match the screens actually queued.
//
// Measured in the 3165583c run: pace:Show:EventArtScreen was 136.2s over 52,779 samples, the
// largest pacing bucket in the game.
//
// The purge must stay narrow: EventArtScreen also carries pollution, global warming, city
// capture and much else, and dropping those would lose real notifications.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tasks;

namespace CivOne.Tests
{
	public class WeLoveTheKingPurgeTests
	{
		private static void AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Sim.ClearTasks();
		}

		[Fact]
		public void ThePurgeRemovesQueuedCelebrationArt()
		{
			AWorld();
			for (int i = 0; i < 5; i++)
				GameTask.Enqueue(Show.EventArt("welovethekingday", $"We Love the King Day in City{i}!"));
			Assert.Equal(5, GameTask.Count<Show>());

			Show.DropAllWeLovePresidentDay();

			Assert.Equal(0, GameTask.Count<Show>());
		}

		// The half that a blanket `is EventArtScreen` filter would break.
		[Fact]
		public void ThePurgeLeavesOtherEventArtAlone()
		{
			AWorld();
			GameTask.Enqueue(Show.EventArt("pollution", "Pollution in York!"));
			GameTask.Enqueue(Show.EventArt("globalwarming", "Global warming! Icecaps melt."));
			GameTask.Enqueue(Show.EventArt("welovethekingday", "We Love the King Day in Bath!"));
			Assert.Equal(3, GameTask.Count<Show>());

			Show.DropAllWeLovePresidentDay();

			Assert.Equal(2, GameTask.Count<Show>());
		}

		// The art key is what makes the two tellable apart, so pin that it survives the trip
		// from Show.EventArt into the screen.
		[Fact]
		public void EventArtRemembersWhichEventItIs()
		{
			AWorld();
			var screen = new CivOne.Screens.EventArtScreen(
				CivOne.Screens.EventArtScreen.FindPath("pollution")!, "Pollution in York!", "pollution");
			Assert.Equal("pollution", screen.ArtKey);
		}
	}
}
