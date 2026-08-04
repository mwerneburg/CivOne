// CivOne tests
//
// Per-city notifications were ~30% of an autoplayed game: 52,779 paced samples of
// celebration art and 55,994 of disorder advisors in one 23-minute run. The screens were
// not merely expensive, they buried the ones worth seeing.
//
// The rule now is signal, not volume:
//   celebration  — art with animations on; otherwise the sidebar panel already carries it
//   disorder     — art with animations on; otherwise ONE digest per turn for the empire
//   improvements — art with animations on; otherwise only when the city needs an order
//   wonders      — always, animations or not: once per world, and the thing to catch
//
// Escalation events (marketplace burned, bank looted, government collapsed) are untouched.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Tasks;

namespace CivOne.Tests
{
	public class NotificationSignalTests
	{
		private static (Game, Player, City) AHumanCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Explore(40, 25, range: 4);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.Size = 6;
			Sim.ClearTasks();
			DisorderNotifications.Clear();
			return (g, human, c);
		}

		// ── the digest ────────────────────────────────────────────────────────

		[Fact]
		public void TheDisorderDigestNamesFewCitiesAndCountsTheRest()
		{
			DisorderNotifications.Clear();
			for (int i = 0; i < 20; i++) DisorderNotifications.Add($"City{i}");

			string[] lines = DisorderNotifications.Summary();

			Assert.Contains("20", lines[0]);
			Assert.Contains(lines, l => l.Contains("more"));
			// Readable: it must not try to print twenty names.
			Assert.True(lines.Length <= 5, $"digest too long: {lines.Length} lines");
		}

		[Fact]
		public void AnEmptyDigestSaysNothingAtAll()
		{
			DisorderNotifications.Clear();
			Assert.Empty(DisorderNotifications.Summary());
		}

		[Fact]
		public void TheDigestDoesNotRepeatACity()
		{
			DisorderNotifications.Clear();
			DisorderNotifications.Add("York");
			DisorderNotifications.Add("York");
			Assert.Single(DisorderNotifications.Cities);
		}

		// One report for the empire, and the list is consumed so it cannot leak into the
		// next turn.
		[Fact]
		public void PlayerNewTurnReportsTheDigestOnceAndClearsIt()
		{
			(Game g, Player human, City c) = AHumanCity();
			DisorderNotifications.Add("York");
			DisorderNotifications.Add("Bath");

			human.NewTurn();

			Assert.Empty(DisorderNotifications.Cities);
			Assert.True(GameTask.Count<Message>() >= 1, "the digest should have been reported");
		}

		// ── improvement gating ────────────────────────────────────────────────

		// Animations off and a queue with work in it: an ordinary building is bookkeeping.
		[Fact]
		public void WithAnimationsOff_AnOrdinaryBuildIsSilentWhenTheQueueHasWork()
		{
			(Game g, Player human, City c) = AHumanCity();
			g.Animations = false;
			c.SetProduction(new Barracks());
			c.EnqueueProduction(new Temple());
			Sim.ClearTasks();

			GameTask.Enqueue(new ImprovementBuilt(c, new Barracks()));
			for (int i = 0; i < 8 && GameTask.Any(); i++) GameTask.Update();

			Assert.False(Common.HasScreenType<CivOne.Screens.Newspaper>(),
				"an ordinary build with work queued should not raise a screen");
		}

		// ...but an empty queue means the city is about to idle and wants an order.
		[Fact]
		public void WithAnimationsOff_AnEmptyQueueStillReports()
		{
			(Game g, Player human, City c) = AHumanCity();
			g.Animations = false;
			c.ClearProductionQueue();
			Sim.ClearTasks();

			GameTask.Enqueue(new ImprovementBuilt(c, new Barracks()));
			for (int i = 0; i < 8 && GameTask.Any(); i++) GameTask.Update();

			Assert.True(Common.HasScreenType<CivOne.Screens.Newspaper>(),
				"a city with nothing queued needs the player's attention");
		}
	}
}
