// CivOne tests
//
// The AI's tax/luxury/science split. The failure this guards against is a total
// research shutdown: the raise branch of ConsiderSliders only ever moved the
// luxury slider UP, and its trigger ("any city in disorder") is permanently true
// for a large empire, so luxuries ratcheted to 8 against a tax floor of 2 and
// science got nothing for the rest of the game. Measured at turn 440 of a real
// game: Romans 79 cities on 30 advances, Mongols 51 cities on 15.

using System.Linq;
using CivOne;

namespace CivOne.Tests
{
	public class SliderPolicyTests
	{
		// A civ already driven to the old 2/8/0 split must climb back out of it.
		//
		// Scope: the Sim harness cannot manufacture civil disorder, so this exercises
		// the calm path — either the ceiling clamp or the wind-down will rescue it, and
		// the test only fails when BOTH are broken. The clamp's own case (high unrest,
		// where the raise branch would otherwise pin the slider) is verified against a
		// real save rather than here.
		[Fact]
		public void Sliders_AlwaysLeaveTradeForScience()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;

			// The state the old policy pinned every large empire to.
			player.TaxesRate     = 2;
			player.LuxuriesRate  = 8;
			Assert.Equal(0, 10 - player.TaxesRate - player.LuxuriesRate);

			for (int turn = 0; turn < 20; turn++) player.AI!.ConsiderSliders();

			int science = 10 - player.TaxesRate - player.LuxuriesRate;
			Assert.True(science >= 2,
				$"expected at least 2 trade on research, got {science} "
				+ $"(tax {player.TaxesRate}, luxuries {player.LuxuriesRate})");
		}
	}
}
