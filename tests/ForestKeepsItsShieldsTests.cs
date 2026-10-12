// CivOne tests
//
// AI settlers converted every forest in reach to plains. WorkAvailable counted Forest as
// "irrigation" (irrigating it converts it), which put it in BestImproveSite's first pass, and
// the scan breaks distance ties toward more shields — toward the forest. Each conversion
// turns 2 shields into 1. Measured over 60 turns from a turn-393 save, once the Lakota had
// enough settlers to reach them: 38 fewer forests and a shield dip the food gain did not
// justify. Swamp and Jungle still convert; they gain by it.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ForestKeepsItsShieldsTests
	{
		[Fact]
		public void ASettlerAmongForestsWalksPastThemToFarmland()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++) for (int x = 30; x <= 55; x++) Map.Instance.ChangeTileType(x, y, Terrain.Forest);
			for (int y = 20; y <= 30; y++) Map.Instance.ChangeTileType(38, y, Terrain.River);
			Map.Instance.ChangeTileType(39, 25, Terrain.Grassland1);   // farmland beside the river, two tiles out
			Map.Instance.ChangeTileType(43, 27, Terrain.Swamp);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Government = new CivOne.Governments.Monarchy();
			ai.Explore(41, 25, range: 20);
			g.AddCity(ai, 0, 41, 25);
			IUnit settler = g.CreateUnit(UnitType.Settlers, 41, 25, g.PlayerNumber(ai))!;
			Sim.ClearTasks();
			AI brain = AI.Instance(ai);

			Assert.False(brain.WorkAvailable(Map.Instance[42, 25]).Irrigation, "a forest is not farm work");
			Assert.True(brain.WorkAvailable(Map.Instance[43, 27]).Conversion, "a swamp still is");

			ITile? site = brain.BestImproveSite(settler);
			Assert.NotNull(site);
			Assert.Equal((39, 25), (site!.X, site.Y));
		}
	}
}
