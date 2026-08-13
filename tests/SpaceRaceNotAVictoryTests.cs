// CivOne tests
//
// Reaching Alpha Centauri no longer ends the game.
//
// Civ 1's rule was: first ship there wins outright, and if an AI got there first the human
// was simply shown Game Over. This game kept that for the case where the SETI signal had not
// yet been received — so whether the arrival was a milestone or an ending turned on whether
// the player had been TOLD about the neighbours. Under a dark forest premise that is
// backwards: leaving the solar system is exposure, not a trophy, and it is no safer for
// having been done in ignorance.
//
// So the arrival is now a milestone on every path, and what the colony is worth gets settled
// later by whether it survives. The discriminator these tests use is SpaceshipArrivalTurn:
// the old ending branch RETURNED without clearing it, the milestone branch zeroes it.

using System.Linq;

namespace CivOne.Tests
{
	public class SpaceRaceNotAVictoryTests
	{
		// A plain game with no story arc running: SETISignalReceived is false, which is
		// precisely the case that used to end in a Space Race Victory screen.
		private static (Game game, Player human, Player rival) AWorldBeforeTheSignal()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			Player rival = g.Players.First(p => p is not null && p != human && g.PlayerNumber(p) != 0);

			Assert.False(g.SETISignalReceived, "fixture is meant to run BEFORE the signal");
			return (g, human, rival);
		}

		// The arrival check lives in EndTurn's phase B, which runs once _currentPlayer wraps.
		// Driven directly rather than through Sim.RunTurns: RunTurns plays the game, and a
		// fresh world with no cities gives it nothing to do, so it burns its budget without
		// ever completing a round.
		private static void PlayARound(Game g)
		{
			for (int i = 0; i <= g.Players.Count(); i++)
			{
				Sim.ClearTasks();
				g.EndTurn();
			}
		}

		[Fact]
		public void AHumanArrivalBeforeTheSignalIsAMilestoneNotAWin()
		{
			(Game g, Player human, Player rival) = AWorldBeforeTheSignal();
			byte hnum = g.PlayerNumber(human);
			int before = human.MilestoneScore;
			g.SpaceshipArrivalTurn[hnum] = (int)g.GameTurn + 1;

			PlayARound(g);

			// Cleared, so the check does not fire again — and the ending branch could not
			// have run, because it returned with this still set.
			Assert.Equal(0, g.SpaceshipArrivalTurn[hnum]);
			Assert.Equal(before + 100, human.MilestoneScore);
		}

		// The other half of the retirement: a rival getting there first used to be Game Over
		// for the human. Now it is a headline.
		[Fact]
		public void ARivalArrivalBeforeTheSignalDoesNotEndTheHumansGame()
		{
			(Game g, Player human, Player rival) = AWorldBeforeTheSignal();
			byte rnum = g.PlayerNumber(rival);
			g.SpaceshipArrivalTurn[g.PlayerNumber(human)] = 0;
			g.SpaceshipArrivalTurn[rnum] = (int)g.GameTurn + 1;

			PlayARound(g);

			Assert.Equal(0, g.SpaceshipArrivalTurn[rnum]);
			Assert.False(human.IsDestroyed(), "the human lost the game to somebody else's spaceship");
		}

		// Pins the retirement at the source. Both tests above observe behaviour through one
		// field, and a future rewrite could plausibly satisfy them while quietly restoring an
		// ending — so demand that the victory machinery is not reachable from this block.
		[Fact]
		public void TheArrivalBlockAwardsNoSpaceRaceVictory()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "Game.cs"));

			Assert.DoesNotContain("Space Race", src);
		}
	}
}
