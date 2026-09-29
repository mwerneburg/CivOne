// CivOne tests
//
// Autoplay honours the player's governors (the user, Sep 2026). With every Russian city on
// the Culture governor and autoplay running, culture came out LOWER than a run with no
// governors at all — because under Autopilot the AI managed the human's cities with its own
// defaults and never read the governor: AI.ConsiderCitizens called AutoAssignCitizens with
// culture off, and City.NewTurn handed production straight to the AI planner.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class AutopilotGovernorTests : System.IDisposable
	{
		private readonly bool _auto;
		public AutopilotGovernorTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// CitizenGovernorTests' human city — size 10 on irrigated grassland under Monarchy,
		// with a Temple and Colosseum so there are citizens to spare — but under Autopilot.
		private static (Game g, Player human, City city) AHumanCityOnAutopilot(bool culture)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Irrigation = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Government = new CivOne.Governments.Monarchy();
			human.Explore(40, 25, range: 20);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.Size = 10;   // at 20 it needs five entertainers to hold order, and has no one to spare
			c.AddBuilding(new Temple());
			c.AddBuilding(new Colosseum());
			c.ResetResourceTiles();
			c.GovernorOrder = c.GovernorGrowth = c.GovernorCulture = culture;
			Settings.Instance.Autopilot = true;
			Assert.NotNull(human.AI);   // the AI is driving
			Sim.ClearTasks();
			return (g, human, c);
		}

		private static int Artists(City c) => c.Citizens.Count(z => z == Citizen.Artist);

		[Fact]
		public void UnderAutopilotACultureCityGetsItsArtists()
		{
			(Game _, Player human, City c) = AHumanCityOnAutopilot(culture: true);

			AI.Instance(human).ConsiderCitizens();

			Assert.True(Artists(c) >= c.Size / City.ArtistPerPopulace,
				$"the culture governor was ignored under autopilot: {Artists(c)} artists");
		}

		// Control: with the governor off, autoplay keeps the AI's own management.
		[Fact]
		public void ACityWithNoGovernorKeepsTheAIsDefault()
		{
			(Game _, Player human, City c) = AHumanCityOnAutopilot(culture: false);
			City twin = c;
			AI.Instance(human).ConsiderCitizens();
			int ai = Artists(twin);

			(Game _, Player h2, City governed) = AHumanCityOnAutopilot(culture: true);
			AI.Instance(h2).ConsiderCitizens();

			Assert.True(Artists(governed) > ai, $"governed {Artists(governed)} artists, ungoverned {ai}");
		}

		// Production: a governed city that finishes a building starts a culture building next,
		// rather than whatever the AI planner had queued.
		[Fact]
		public void UnderAutopilotACultureCityBuildsForCulture()
		{
			(Game _, Player human, City c) = AHumanCityOnAutopilot(culture: true);
			human.AddAdvance(new Writing(), false);
			c.SetProduction(new Barracks());
			c.Shields = c.ProductionCost(c.CurrentProduction);
			c.EnqueueProduction(new Granary());   // what an AI plan might have queued

			c.NewTurn();
			Sim.ClearTasks();

			Assert.True(c.HasBuilding<Barracks>(), "fixture: the Barracks did not finish");
			Assert.Equal(nameof(Library), c.CurrentProduction?.GetType().Name);
		}
	}
}
