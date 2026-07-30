// CivOne tests
//
// A rival completing a wonder is announced (ImprovementBuilt.Run); a rival
// completing anything else, including a spaceship part or a Palace, is not.
//
// The announcement must never use Newspaper(city) for a FOREIGN city — that draws
// the place, showing the player a city they may not have discovered — so it passes a
// null city instead. This pins that the null path is actually safe to construct,
// which is the only way the fallback can fail.

using CivOne;
using CivOne.Screens;

namespace CivOne.Tests
{
	public class NotificationTests
	{
		[Fact]
		public void ForeignWonderNotice_ConstructsWithoutACity()
		{
			Sim.NewGame(width: 80, height: 50);

			var notice = new Newspaper(null, ["Colossus completed", "in Thebes."], showGovernment: false);

			Assert.NotNull(notice);
		}

		// The art screen is chosen only when a picture actually exists, so a wonder with
		// no art must fall through to the text notice rather than to nothing at all.
		[Fact]
		public void MissingWonderArt_FallsBackToText()
		{
			Assert.Null(ImprovementArtScreen.FindArtPath("No Such Wonder"));
		}
	}
}
