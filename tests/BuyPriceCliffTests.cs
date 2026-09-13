// CivOne tests
//
// The from-scratch cliff.
//
// City.BuyPrice has two branches. With anything at all in the shield box it charges the
// MARGINAL rate — 2 gold a shield for a building, 4 for a wonder, a quadratic for a unit —
// and credits next turn's production on top. With an EMPTY box it falls through to the item's
// flat BuyPrice instead: 40 x price for a building, 80 for a wonder, which works out at
// exactly double the per-shield rate.
//
// So one turn of production halves the price, and nothing in the game said so. The city screen
// now paints BUY red on that branch, and City.BuyFromScratch is the single test both the
// warning and the price read — the zone-of-control rule and the sea-tube rule each went wrong
// in this codebase by being written down twice.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class BuyPriceCliffTests
	{
		// A big building, so "finishes next turn unaided" (BuyPrice 0) cannot muddy the
		// comparison whatever the city's shield income turns out to be.
		private static City ACityBuilding()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player me = g.HumanPlayer;
			me.Explore(42, 25, range: 8);
			City c = g.AddCity(me, 0, 42, 25)!;
			c.Size = 6;
			c.SetProduction(new Aqueduct());   // 120 shields
			Sim.ClearTasks();
			return c;
		}

		[Fact]
		public void AnEmptyBoxIsChargedFourGoldAShield()
		{
			City city = ACityBuilding();
			int target = city.ProductionCost(city.CurrentProduction);
			city.Shields = 0;

			Assert.Equal(4 * target, city.BuyPrice);
		}

		// One shield moves it onto the marginal rate — at most half as much per shield, and
		// less again once next turn's production is credited.
		[Fact]
		public void OneShieldHalvesThePrice()
		{
			City city = ACityBuilding();
			int target = city.ProductionCost(city.CurrentProduction);
			city.Shields = 0;
			int fromScratch = city.BuyPrice;

			city.Shields = 1;
			int marginal = city.BuyPrice;

			Assert.True(marginal > 0, "fixture: the building finishes unaided, so there is no price to compare");
			Assert.True(marginal <= 2 * target, $"the marginal rate should be 2 a shield or better, got {marginal} for {target}");
			Assert.True(fromScratch >= 2 * marginal,
				$"the cliff is gone: {fromScratch} from scratch against {marginal} with one shield down");
		}

		// The warning and the price must agree about which branch is in force. This is the
		// whole reason BuyFromScratch exists rather than the screen testing Shields == 0.
		[Fact]
		public void TheWarningMarksExactlyTheExpensiveBranch()
		{
			City city = ACityBuilding();
			int target = city.ProductionCost(city.CurrentProduction);

			city.Shields = 0;
			Assert.True(city.BuyFromScratch);
			Assert.Equal(4 * target, city.BuyPrice);

			city.Shields = 1;
			Assert.False(city.BuyFromScratch);
			Assert.NotEqual(4 * target, city.BuyPrice);
		}

		// A wonder is the same cliff at twice the money, which is where it actually hurts.
		[Fact]
		public void AWonderPaysEightGoldAShieldFromScratch()
		{
			City city = ACityBuilding();
			city.SetProduction(new CivOne.Wonders.Pyramids());
			int target = city.ProductionCost(city.CurrentProduction);
			city.Shields = 0;

			Assert.Equal(8 * target, city.BuyPrice);
		}
	}
}
