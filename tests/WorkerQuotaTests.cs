// CivOne tests
//
// A large AI empire's work-settler quota was only consulted when a city's queue ran dry,
// which late in the game is every 50-90 turns, so a 49-city Lakota wanting 24 workers held
// 5. AI.ConsiderWorkers now tops up the shortfall once a turn at the head of the queues.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class WorkerQuotaTests
	{
		[Fact]
		public void ALargeEmptyEmpireQueuesItsWorkersAtOnce()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 10; y <= 40; y++) for (int x = 5; x <= 75; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Game g = Game.Instance;
			Player p = g.Players.First(q => q is not null && q != g.HumanPlayer && g.PlayerNumber(q) != 0);
			p.Government = new CivOne.Governments.Monarchy();
			p.Explore(40, 25, range: 36);
			byte n = g.PlayerNumber(p);
			for (int i = 0; i < 16; i++)
			{
				City c = g.AddCity(p, i, 8 + (i % 8) * 8, i < 8 ? 18 : 32)!;
				c.Size = 4;
				c.ResetResourceTiles();
				c.SetProduction(new Temple());
				c.EnqueueProduction(new Granary());
			}
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == n && u is Settlers).ToArray()) g.DisbandUnit(u);
			Sim.ClearTasks();
			Assert.All(p.Cities, c => Assert.True(c.FoodIncome >= 2, $"{c.Name} food {c.FoodIncome}"));

			AI.Instance(p).ConsiderWorkers();

			// 16 cities want 8 workers; none exist, so exactly 8 queues gain a Settlers first.
			Assert.Equal(8, p.Cities.Count(c => c.ProductionQueue.FirstOrDefault() is Settlers));
			Assert.All(p.Cities, c => Assert.IsType<Temple>(c.CurrentProduction));

			// A second pass counts the queued ones and adds nothing.
			AI.Instance(p).ConsiderWorkers();
			Assert.Equal(8, p.Cities.Count(c => c.ProductionQueue.Any(x => x is Settlers)));
		}
	}
}
