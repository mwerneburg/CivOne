// CivOne tests
//
// Camp, terrace and moisture-farm progress were never written to the save, so a reload
// threw away the turns already spent and left the settler idle on a half-built job. Same
// gap as the pollution crew (PollutionCrewTests.ACrewMidCleanSurvivesASaveAndLoad).

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SettlerProgressSaveTests
	{
		[Fact]
		public void HalfBuiltJobsSurviveASaveAndLoad()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance; Player h = g.HumanPlayer; byte hn = g.PlayerNumber(h);
			for (int y = 20; y <= 30; y++) for (int x = 30; x <= 50; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			g.AddCity(h, 0, 40, 25);
			((Settlers)g.CreateUnit(UnitType.Settlers, 41, 25, hn)!).BuildingCamp = 3;
			((Settlers)g.CreateUnit(UnitType.Settlers, 42, 25, hn)!).BuildingTerrace = 4;
			((Settlers)g.CreateUnit(UnitType.Settlers, 43, 25, hn)!).BuildingMoistureFarm = 5;
			// An idle unit, or the load-time recovery for "no human unit can move" wakes them all.
			g.CreateUnit(UnitType.Militia, 40, 25, hn);
			Sim.ClearTasks();
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "settler-progress.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			Settlers At(int x) => Game.Instance.GetUnits().OfType<Settlers>().Single(u => u.X == x && u.Y == 25);
			Assert.Equal(3, At(41).BuildingCamp);
			Assert.Equal(4, At(42).BuildingTerrace);
			Assert.Equal(5, At(43).BuildingMoistureFarm);
		}
	}
}
