// CivOne tests
//
// The user handed the Japanese a new town that was building a Palace. The Japanese AI finished
// it, and their capital moved outside their own territory, where corruption and waste are
// measured from. The planner never picks a Palace while a capital stands; the queue that came
// with the city was simply never re-planned. City.Owner now drops it on the change of hands.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class InheritedPalaceTests
	{
		private static (Player ai, City gift) AGiftBuildingAPalace(bool aiHasCapital)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++) for (int x = 10; x <= 70; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			Player ai = g.Players.First(p => p is not null && p != human && g.PlayerNumber(p) != 0);
			human.Explore(40, 25, range: 30); ai.Explore(40, 25, range: 30);
			City home = g.AddCity(ai, 0, 20, 25)!;
			if (aiHasCapital) { if (!home.HasBuilding<Palace>()) home.AddBuilding(new Palace()); }
			else home.RemoveBuilding<Palace>();
			City gift = g.AddCity(human, 1, 60, 25)!;
			gift.RemoveBuilding<Palace>();
			gift.SetProduction(new Palace());
			gift.EnqueueProduction(new Palace());
			Sim.ClearTasks();
			gift.Owner = g.PlayerNumber(ai);
			return (ai, gift);
		}

		[Fact]
		public void AnAIWithACapitalDropsTheInheritedPalace()
		{
			(_, City gift) = AGiftBuildingAPalace(aiHasCapital: true);
			Assert.IsNotType<Palace>(gift.CurrentProduction);
			Assert.DoesNotContain(gift.ProductionQueue, p => p is Palace);
		}

		// Without a capital the Palace is exactly what the AI would choose, so it stays.
		[Fact]
		public void AnAIWithoutACapitalKeepsIt()
		{
			(_, City gift) = AGiftBuildingAPalace(aiHasCapital: false);
			Assert.IsType<Palace>(gift.CurrentProduction);
		}
	}
}
