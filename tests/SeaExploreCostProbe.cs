// TEMPORARY probe — splits site:BestSeaExploreTile (12,156 us/call) into its 625-tile
// scan and the single confirming A* at the end, to decide whether an ocean reachability
// id would actually help. Delete once answered.

using System.Diagnostics;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class SeaExploreCostProbe
	{
		private readonly ITestOutputHelper _out;
		public SeaExploreCostProbe(ITestOutputHelper output) => _out = output;

		[Fact(Skip = "probe; remove -Skip to run")]
		public void SplitTheSeaExploreCost()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			// Open ocean everywhere except one enclosed lake, which is the case the
			// confirming A* exists to catch.
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				if (!Map.Instance[x, y].IsOcean) Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 30; y <= 40; y++)
			for (int x = 60; x <= 70; x++)
				if (y == 30 || y == 40 || x == 60 || x == 70)
					Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			IUnit ship = g.CreateUnit(UnitType.Trireme, 20, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();

			AI ai = AI.Instance(p);
			var sw = new Stopwatch();

			// Whole call, warmed.
			ai.BestSeaExploreTile(ship);
			sw.Restart();
			for (int i = 0; i < 20; i++) ai.BestSeaExploreTile(ship);
			double whole = sw.Elapsed.TotalMilliseconds / 20;

			// A reachable target: A* over open water.
			sw.Restart();
			for (int i = 0; i < 20; i++) Common.GotoStep(ship, 40, 25);
			double reachable = sw.Elapsed.TotalMilliseconds / 20;

			// An UNREACHABLE target: the enclosed lake. This is the worst case the
			// short-circuit would remove entirely.
			sw.Restart();
			for (int i = 0; i < 20; i++) Common.GotoStep(ship, 65, 35);
			double unreachable = sw.Elapsed.TotalMilliseconds / 20;

			_out.WriteLine($"BestSeaExploreTile (whole)  {whole:F3} ms");
			_out.WriteLine($"  GotoStep, reachable       {reachable:F3} ms");
			_out.WriteLine($"  GotoStep, UNREACHABLE     {unreachable:F3} ms");
		}
	}
}
