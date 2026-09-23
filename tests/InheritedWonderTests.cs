// CivOne tests
//
// A captured city keeps the previous owner's production order. Nothing re-checked it at
// completion, so in play (Sep 2026) the Chinese took a city back from the Machines and
// finished THE REPROCESSOR — a wonder only the Machines may build. A wonder now completes
// only for an owner allowed to build it, and otherwise rolls over like one lost to a rival.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class InheritedWonderTests
	{
		// An ordinary AI civ's city one turn from finishing `wonder`.
		private static (Game g, City city) OneTurnFrom(System.Func<IWonder> make, params IAdvance[] advances)
		{
			Sim.NewGame(width: 80, height: 50);   // before `make`: a wonder needs the runtime for its icon
			Game g = Game.Instance;
			IWonder wonder = make();
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			foreach (IAdvance a in advances) ai.AddAdvance(a, false);
			City city = g.AddCity(ai, 0, 40, 25)!;
			city.Size = 4;
			city.SetProduction(wonder);
			city.Shields = city.ProductionCost(wonder);
			Sim.ClearTasks();
			return (g, city);
		}

		[Fact]
		public void AnInheritedMachineWonderIsNotFinishedByHumans()
		{
			(Game g, City city) = OneTurnFrom(() => new TheReprocessor());

			city.NewTurn();

			Assert.False(g.WonderBuilt<TheReprocessor>(), "a human civilization finished the machines' wonder");
			Assert.IsNotType<TheReprocessor>(city.CurrentProduction);
		}

		// Control: the same city finishes a wonder it may build.
		[Fact]
		public void AnOrdinaryWonderStillCompletes()
		{
			(Game g, City city) = OneTurnFrom(() => new Pyramids(), new Masonry());

			city.NewTurn();

			Assert.True(g.WonderBuilt<Pyramids>(), "fixture: the city cannot finish a wonder at all");
		}
	}
}
