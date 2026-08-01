// CivOne tests
//
// The Others landed around 1700 AD in a 2200 AD game, held 51 cities for five
// centuries, and finished sixth on score while every human power grew around them.
// The decision log said why: running the ordinary production planner, they spent
// 1255 decisions between turns 370 and 749 on 97 Observatories, 95 Cathedrals,
// 89 Colosseums, 66 Hospitals and 51 Marketplaces — against 131 military units.
// An occupying registry had become a city-management sim.
//
// They are cephalopods who do not recognise their captives as anything but stock.
// They build warheads, armour, and saboteurs. Nothing that serves a population.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Buildings;
using CivOne.Wonders;
using CivOne.Advances;
using CivOne.Civilizations;
using CivOne.Units;

namespace CivOne.Tests
{
	public class OthersOccupationTests
	{
		// An occupier holding a developed city on good ground — the exact case where the
		// ordinary planner reached for an Aqueduct and a Temple.
		private static City Occupied(int cities = 1)
		{
			Sim.NewGame(width: 80, height: 50);
			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			ICivilization civ = Common.Civilizations.First(c => c is TheOthers);
			Player others = new Player(civ, "The Registry");
			Game.Instance.AddPlayer(others);
			foreach (IAdvance adv in Common.Advances.Where(a => !(a is FutureTech)))
				if (!others.HasAdvance(adv)) others.AddAdvance(adv, false);
			others.Government = new CivOne.Governments.Communism();
			others.Gold = 1500;

			City first = null!;
			for (int i = 0; i < cities; i++)
			{
				int x = cx + i * 4;
				Map.Instance.ChangeTileType(x, cy, Terrain.Grassland1);
				others.Explore(x, cy, range: 6);
				City c = Game.Instance.AddCity(others, (byte)i, x, cy)!;
				c.Size = 8;
				c.ResetResourceTiles();
				if (i == 0) first = c;
			}
			return first;
		}

		// The finding: nothing that serves a population.
		[Fact]
		public void TheOthers_BuildNothingForTheCaptivePopulation()
		{
			City city = Occupied();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.DoesNotContain(plan, p => p is Temple || p is Cathedral || p is Colosseum
				|| p is Observatory || p is MarketPlace || p is Aqueduct || p is Granary
				|| p is Hospital || p is Library);
		}

		// Nor wonders. They did not come here to develop the place.
		[Fact]
		public void TheOthers_BuildNoWondersAtAll()
		{
			City city = Occupied();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.DoesNotContain(plan, p => p is IWonder);
		}

		// What they DO build is materiel. Nuclear needs the Manhattan Project and HoverTank
		// their own Fusion Core, so in a bare test the fallback armour is what appears —
		// the assertion is that the output is military, not which chassis.
		[Fact]
		public void TheOthers_BuildMateriel()
		{
			City city = Occupied();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			Assert.NotNull(city.CurrentProduction);
			Assert.True(city.CurrentProduction is IUnit,
				$"an occupation should be producing units; got {city.CurrentProduction!.GetType().Name}");
		}

		// They do not garrison what they take. This is a smash-and-grab for livestock, to be
		// over before someone uploads a virus — so an occupied city gets no defender, and is
		// correspondingly cheap to retake. The arc is meant to be broken, not out-produced.
		[Fact]
		public void TheOthers_NeverGarrisonWhatTheyTake()
		{
			City city = Occupied();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.DoesNotContain(plan, p => p is IUnit u && u.Role == UnitRole.Defense);
			Assert.DoesNotContain(plan, p => p is CityWalls);
		}

		// The warheads are the arrival, not the occupation: ExecuteOwnersLanding nukes the
		// capitals and that is the whole of it. Stockpiling more would be a weapon this AI
		// has no doctrine for firing.
		[Fact]
		public void TheOthers_DoNotStockpileWarheads()
		{
			City city = Occupied();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.DoesNotContain(plan, p => p is Nuclear);
		}

		// Saboteurs are a tool, not a doctrine. They had produced 84 Diplomats — a faction
		// that returns early from every diplomatic path — so the count is capped.
		[Fact]
		public void TheOthers_DoNotStockpileSaboteurs()
		{
			City city = Occupied(cities: 2);
			Player others = city.Player;
			byte id = Game.Instance.PlayerNumber(others);
			// Already well past one agent per two cities.
			for (int i = 0; i < 6; i++)
				Game.Instance.CreateUnit(UnitType.Diplomat, city.X, city.Y, id);
			Sim.ClearTasks();

			AI.Instance(others).CityProduction(city);

			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.DoesNotContain(plan, p => p is Diplomat);
		}
	}
}
