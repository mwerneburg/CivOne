// CivOne tests
//
// The Nanobot Factory's cursed roll (docs/cursed_wonders.md §12).
//
// The curse is permanent: once the goo is seeded the factory never refits a unit again, even
// after the last goo tile is scrubbed. That is deliberate, and the game now says so. After
// the containment-failure notice, a second one reports the factory decommissioned and kept
// as a memorial.
//
// The Civilopedia's page 2 carries the counterplay, which was previously written down only
// in the design doc.

using System.Linq;
using CivOne;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class NanobotFactoryTests
	{
		[Fact]
		public void TheOutbreakIsFollowedByTheMemorialNotice()
		{
			Sim.NewGame(width: 80, height: 50);
			Sim.ClearTasks();

			Game.Instance.AnnounceGreyGoo("Ur");

			string[] lines = Sim.PendingMessageLines();
			int outbreak = System.Array.FindIndex(lines, l => l.Contains("Containment failure"));
			int memorial = System.Array.FindIndex(lines, l => l.Contains("decommissioned"));
			Assert.True(outbreak >= 0, "the containment-failure notice is missing");
			Assert.True(memorial > outbreak, "the memorial notice must follow the outbreak");
			Assert.Contains(lines, l => l.Contains("memorial"));
		}

		// Pinned phrases, like CursedWonderFlavourTests: the cure, stated where a player can find it.
		[Theory]
		[InlineData("SETTLERS")]
		[InlineData("CLEAN POLLUTION")]
		[InlineData("NUCLEAR")]
		public void PageTwoSaysHowToStopTheGoo(string phrase)
		{
			Sim.EnsureRuntime();
			BaseWonder w = (BaseWonder)Reflect.GetWonders().Single(x => x is NanobotFactory);

			Assert.Contains(phrase, string.Join(" ", w.GetPageText(2)));
		}
	}
}
