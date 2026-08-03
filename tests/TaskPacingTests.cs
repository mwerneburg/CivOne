// CivOne tests
//
// The host loop drops out of fast-forward whenever GameTask.Fast is false, and paces the
// game at 60 Hz instead. Fast asked only about _currentTask — which is null in the gap
// between one task finishing and the next starting, even with a full queue.
//
// Measured 2026-08-03: 518,654 such transitions over 381 turns, ~1,361 per turn at ~3.6ms
// of pacing each — 4.9 seconds of a 13-second turn, more than every named task type put
// together (Show 0.64s, ImprovementBuilt 0.25s, Message 0.25s per turn).
//
// Fast now falls back to the head of the queue: the task about to run. The safety property
// is that this is no more permissive than before — a non-Fast task at the head must still
// read as not-fast, or screens the player needs to see get blurred past.

using System.Linq;
using CivOne;
using CivOne.Tasks;

namespace CivOne.Tests
{
	public class TaskPacingTests
	{
		// Turn carries [Fast]; Message does not.
		private static GameTask AFastTask() => Turn.End();
		private static GameTask ASlowTask() => Message.General("test", "message");

		// The regression: a queued Fast task with nothing current must read as fast.
		[Fact]
		public void BetweenTasks_AQueuedFastTaskStillReadsAsFast()
		{
			Sim.NewGame(width: 80, height: 50);
			Sim.ClearTasks();
			Assert.False(GameTask.Fast, "no queue, nothing current: not fast");

			GameTask.Enqueue(AFastTask());

			Assert.True(GameTask.Any());
			Assert.True(GameTask.Fast,
				"a queued [Fast] task with none yet current must not drop the loop out of fast-forward");
		}

		// The safety half: the fallback must not make a slow task look fast. This is what
		// stops an advisor message or a story screen being fast-forwarded past the player.
		[Fact]
		public void BetweenTasks_AQueuedSlowTaskDoesNotReadAsFast()
		{
			Sim.NewGame(width: 80, height: 50);
			Sim.ClearTasks();

			GameTask.Enqueue(ASlowTask());

			Assert.True(GameTask.Any());
			Assert.False(GameTask.Fast,
				"a Message at the head of the queue must still pace at 60 Hz");
		}

		// An empty queue is not fast — otherwise an idle game would never sleep.
		[Fact]
		public void AnEmptyQueue_IsNotFast()
		{
			Sim.NewGame(width: 80, height: 50);
			Sim.ClearTasks();

			Assert.False(GameTask.Any());
			Assert.False(GameTask.Fast);
		}

		// Order matters: the HEAD of the queue decides, not merely whether any Fast task is
		// present. A slow task about to run must stop the fast-forward even with Fast work
		// queued behind it.
		[Fact]
		public void TheHeadOfTheQueueDecides_NotTheRest()
		{
			Sim.NewGame(width: 80, height: 50);
			Sim.ClearTasks();

			GameTask.Enqueue(ASlowTask());
			GameTask.Enqueue(AFastTask());

			Assert.False(GameTask.Fast,
				"a slow task at the head must win over Fast tasks queued behind it");
		}
	}
}
