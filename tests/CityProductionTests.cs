// CivOne tests
//
// The AI's per-city build plan. Two guards:
//
//   1. A happiness building in a city with no unhappiness is pure upkeep. The
//      autoplayed Japan held nine Temples across ten content size-2 cities, 9 gold
//      a turn out of the 29 it could not afford.
//   2. PlanProduction must never return an empty plan, because CityProduction
//      indexes plan[0]. Adding guard 1 broke exactly this — ResearchGrant carries
//      zero maintenance but was declined by an affordability test, leaving the plan
//      empty and crashing on a real save.

using System;
using System.IO;
using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Buildings;

namespace CivOne.Tests
{
	public class CityProductionTests
	{
		// A content city, with the luxury slider idle, must not be handed a Temple.
		[Fact]
		public void ContentCity_IsNotGivenAHappinessBuilding()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			player.AddAdvance(new CeremonialBurial());   // Temple becomes available
			player.LuxuriesRate = 0;

			City city = Game.Instance.AddCity(player, 0, 40, 25)!;
			Assert.NotNull(city);
			Assert.Equal(0, city.UnhappyCitizens);

			// Re-plan a few times: the plan is partly random at the fallback, so once
			// proves little.
			for (int i = 0; i < 25; i++)
			{
				AI.Instance(player).CityProduction(city);
				Assert.False(city.CurrentProduction is Temple,
					"a city with no unhappy citizens and luxuries at 0 should not build a Temple");
			}
		}

		// Whatever the gates decline, every city still ends up building SOMETHING.
		// Runs over a rich real save so the wonder, fallback and ResearchGrant paths are
		// all exercised, across civs at every stage of development.
		[Fact]
		public void EveryCityAlwaysGetsAProduction()
		{
			Sim.EnsureRuntime();
			Sim.ResetState();
			Settings.Instance.Autopilot = true;
			Assert.True(Game.LoadCos(Path.Combine(
				AppContext.BaseDirectory, "fixtures", "CIVIL3.cos")), "fixture should load");

			int planned = 0;
			foreach (Player p in Game.Instance.Players.Where(x => !x.IsDestroyed() && x.Cities.Length > 0))
			foreach (City c in p.Cities)
			{
				AI.Instance(p).CityProduction(c);
				Assert.NotNull(c.CurrentProduction);
				planned++;
			}
			Assert.True(planned > 50, $"expected a rich save, only planned {planned} cities");
		}
	}
}
