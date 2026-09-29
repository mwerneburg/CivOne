// CivOne tests
//
// The player's clean-up crew (reported Sep 2026): a settler set to clean pollution "would
// waken some rounds later with the pollution still there", and pressing P set it working
// again. Units take their turn before cities, so a settler that found the map clean stood
// down — then the cities polluted again the same turn, often the tile it had just cleaned.
// The player's crew now stays on duty and stands by.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class PollutionCrewTests : System.IDisposable
	{
		private readonly bool _auto;
		public PollutionCrewTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; Settings.Instance.Autopilot = false; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// A human city at 40,25 and a settler of ours on clean-up duty beside it.
		private static (Game g, Settlers s) ACrew()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance; Player h = g.HumanPlayer;
			for (int y = 20; y <= 30; y++) for (int x = 30; x <= 50; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == g.PlayerNumber(h)).ToArray()) g.DisbandUnit(u);
			h.Explore(40, 25, range: 8);
			g.AddCity(h, 0, 40, 25)!.Size = 4;
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 41, 25, g.PlayerNumber(h))!;
			s.AutoClean = true;
			Sim.ClearTasks();
			return (g, s);
		}

		// The report: nothing to clean at the settler's turn...
		[Fact]
		public void WithNothingToCleanTheCrewStandsByInsteadOfStandingDown()
		{
			(Game _, Settlers s) = ACrew();

			s.NewTurn();

			Assert.True(s.AutoClean, "the crew stood down the moment the map was clean");
			Assert.Equal(0, s.MovesLeft);   // standing by, so it does not ask for orders
		}

		// ...and when a city pollutes again, it goes back to work by itself.
		[Fact]
		public void WhenPollutionReturnsTheCrewGoesToIt()
		{
			(Game _, Settlers s) = ACrew();
			s.NewTurn();                              // map clean: stands by
			Map.Instance[42, 26].Pollution = true;    // the city pollutes again, after the units

			s.NewTurn();

			Assert.Equal((42, 26), (s.Goto.X, s.Goto.Y));
		}

		// The AI's crews still stand down: it sizes them to the backlog every turn.
		[Fact]
		public void AnAICrewStillStandsDown()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			for (int y = 20; y <= 30; y++) for (int x = 30; x <= 50; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			g.AddCity(ai, 0, 40, 25)!.Size = 4;
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 41, 25, g.PlayerNumber(ai))!;
			s.AutoClean = true;

			s.NewTurn();

			Assert.False(s.AutoClean);
		}
	}
}
