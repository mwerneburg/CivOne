// CivOne tests
//
// The city governor chooses what to build next (the user's request, Sep 2026): ORDER builds
// what keeps a restless city in order, CULTURE builds for culture. Only when the city has
// just FINISHED a building or wonder and nothing is queued — never switching away from
// something partly built, which was the user's condition.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ProductionGovernorTests
	{
		// The city's own turn, directly: Game.EndTurn queues it as a task, and a harness
		// that clears the queue never runs it (StarlabTests does the same).
		private static void ItsTurn(City city)
		{
			city.NewTurn();
			Sim.ClearTasks();
		}

		// A human city about to finish a Barracks this turn, in the governor mode given.
		// (order, culture) mirrors the cycle: CULTURE is ORDER + GROWTH + culture.
		private static (Game g, City city) FinishingABarracks(int size, bool order, bool culture, params IAdvance[] advances)
		{
			Sim.NewGame(width: 80, height: 50);
			// Explicitly off: under Autopilot the AI plans the human's cities and the governor
			// is never asked, and many tests leave it on (Sim.NewGame does not reset it).
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player human = g.HumanPlayer;
			human.Explore(40, 25, range: 10);
			foreach (IAdvance a in advances) human.AddAdvance(a, false);
			City city = g.AddCity(human, 0, 40, 25)!;
			city.Size = (byte)size;
			city.GovernorOrder = order;
			city.GovernorGrowth = order;
			city.GovernorCulture = culture;
			city.SetProduction(new Barracks());
			city.Shields = city.ProductionCost(city.CurrentProduction);
			Sim.ClearTasks();
			return (g, city);
		}

		private static string Building(City c) => c.CurrentProduction?.GetType().Name ?? "nothing";

		[Fact]
		public void CultureModeBuildsForCultureWhenABuildingFinishes()
		{
			(Game _, City city) = FinishingABarracks(3, order: true, culture: true, new Writing());
			// The Temple is the better culture buy per shield (+1 for less), so it goes first;
			// with it standing, the Library is what is left.
			city.AddBuilding(new Temple());

			ItsTurn(city);

			Assert.True(city.HasBuilding<Barracks>(), "fixture: the Barracks did not finish");
			Assert.Equal(nameof(Library), Building(city));
		}

		// Control: the same city with the governor OFF is left alone.
		[Fact]
		public void WithTheGovernorOffNothingIsChosen()
		{
			(Game _, City city) = FinishingABarracks(3, order: false, culture: false, new Writing());

			ItsTurn(city);

			Assert.Equal(nameof(Barracks), Building(city));
		}

		// ORDER alone does not build for culture, and builds nothing for a settled city.
		[Fact]
		public void OrderModeBuildsNothingForACityThatIsAlreadySettled()
		{
			(Game _, City city) = FinishingABarracks(1, order: true, culture: false, new Writing(), new CeremonialBurial());

			ItsTurn(city);

			Assert.Equal(nameof(Barracks), Building(city));
		}

		// "Sufficient", not maximal: a city with unhappy citizens that is nonetheless in
		// order without entertainers — here, held by luxuries — gets no Temple. The size-1
		// test above cannot show this: a Temple changes nothing there, gate or no gate.
		[Fact]
		public void OrderModeLeavesACityThatLuxuriesKeepInOrder()
		{
			(Game _, City city) = FinishingABarracks(8, order: true, culture: false, new Writing(), new CeremonialBurial());
			city.Player.TaxesRate = 0;
			city.Player.LuxuriesRate = 8;   // one unhappy, one happy: in order, no entertainer
			city.InvalidateCache();
			Assert.True(city.UnhappyCitizens > 0, "fixture: nobody is unhappy, so a Temple would change nothing");
			Assert.False(city.IsInDisorder, "fixture: the city is rioting");
			Assert.DoesNotContain(city.Citizens, c => c == Citizen.Entertainer);

			ItsTurn(city);

			Assert.Equal(nameof(Barracks), Building(city));
		}

		// A big city under Despotism is restless: ORDER builds the Temple, not the Library.
		[Fact]
		public void OrderModeBuildsForContentmentInARestlessCity()
		{
			(Game _, City city) = FinishingABarracks(9, order: true, culture: false, new Writing(), new CeremonialBurial());

			ItsTurn(city);

			Assert.Equal(nameof(Temple), Building(city));
		}

		// The user's condition: nothing partly built is ever abandoned.
		[Fact]
		public void SomethingPartlyBuiltIsNeverSwitched()
		{
			(Game _, City city) = FinishingABarracks(3, order: true, culture: true, new Writing());
			city.Shields = 1;

			ItsTurn(city);

			Assert.Equal(nameof(Barracks), Building(city));
		}

		// Nor is a unit in production: finishing one is not a spent order.
		[Fact]
		public void AUnitInProductionIsLeftAlone()
		{
			(Game _, City city) = FinishingABarracks(3, order: true, culture: true, new Writing());
			city.SetProduction(new Militia());
			city.Shields = city.ProductionCost(city.CurrentProduction);

			ItsTurn(city);

			Assert.Equal(nameof(Militia), Building(city));
		}

		// The player's queue always comes first.
		[Fact]
		public void TheQueueComesFirst()
		{
			(Game _, City city) = FinishingABarracks(3, order: true, culture: true, new Writing(), new Pottery());
			city.EnqueueProduction(new Granary());

			ItsTurn(city);

			Assert.Equal(nameof(Granary), Building(city));
		}

		// Its culture is real, but the fifth Neural Lab wakes Skynet — the player's call.
		[Fact]
		public void TheGovernorNeverBuildsANeuralLab()
		{
			(Game g, City city) = FinishingABarracks(3, order: true, culture: true);
			foreach (IAdvance a in Common.Advances) g.HumanPlayer.AddAdvance(a, false);
			Assert.True(city.AvailableProduction.Any(p => p is NeuralLab), "fixture: the lab must be on offer");
			foreach (IBuilding b in city.AvailableProduction.OfType<IBuilding>().Where(b => b is not NeuralLab).ToArray())
				city.AddBuilding(b);

			Assert.IsNotType<NeuralLab>(city.GovernorNextBuilding());
		}
	}
}
