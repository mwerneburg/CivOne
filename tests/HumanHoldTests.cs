// CivOne tests
//
// The human's victory hold scales with difficulty; the AI's stays at the tuned 75 on every
// level. A longer hold for everyone would make the AI's own wins harder at King — backwards.

using System.Linq;

namespace CivOne.Tests
{
	public class HumanHoldTests
	{
		[Theory]
		[InlineData(0, 50u)]
		[InlineData(2, 75u)]
		[InlineData(4, 100u)]
		public void TheHumansHoldFollowsDifficultyAndTheAIsDoesNot(int difficulty, uint human)
		{
			Sim.NewGame(width: 80, height: 50, difficulty: difficulty);
			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && p != g.HumanPlayer && g.PlayerNumber(p) != 0);

			Assert.Equal(human, g.CultureHoldFor(g.HumanPlayer));
			Assert.Equal(human, g.EconomicHoldFor(g.HumanPlayer));
			Assert.Equal(Game.CultureHoldTurns, g.CultureHoldFor(ai));
			Assert.Equal(Game.EconomicHoldTurns, g.EconomicHoldFor(ai));
		}
	}
}
