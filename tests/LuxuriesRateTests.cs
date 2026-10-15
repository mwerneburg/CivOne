// CivOne tests
//
// The Luxuries Rate dialog trades luxuries against tax; science stays where the Science Rate
// dialog put it.

namespace CivOne.Tests
{
	public class LuxuriesRateTests
	{
		[Fact]
		public void LuxuriesComeOutOfTaxNotScience()
		{
			Sim.NewGame(width: 80, height: 50);
			Player p = Game.Instance.HumanPlayer;
			p.LuxuriesRate = 0;
			p.TaxesRate = 4;   // science 6

			p.SetLuxuriesKeepingScience(3);
			Assert.Equal((3, 1, 6), (p.LuxuriesRate, p.TaxesRate, p.ScienceRate));

			p.SetLuxuriesKeepingScience(1);
			Assert.Equal((1, 3, 6), (p.LuxuriesRate, p.TaxesRate, p.ScienceRate));

			// More than tax can give is capped at what it holds.
			p.SetLuxuriesKeepingScience(9);
			Assert.Equal((4, 0, 6), (p.LuxuriesRate, p.TaxesRate, p.ScienceRate));
		}
	}
}
