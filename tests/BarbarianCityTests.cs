// CivOne tests
//
// A turn-219 autoplayed game whose Aztecs were reduced to one city by the horde.
// The barbarians had taken Tenochtitlan around turn 140 and then run the ordinary
// civ production planner out of it: a Settlers on turn 145, then Explorer/Militia
// alternating to the end. Holding exactly one city put them in the tiny-empire
// branch, whose premise ("for a 1-2 city civ, expansion IS survival") is written
// about civilisations, not raiders — so the horde became a fifteenth expanding
// power that no one can ever make peace with.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class BarbarianCityTests
	{
		// A city in barbarian hands, on ground good enough that a civ would develop it.
		private static City BarbarianHeld()
		{
			Sim.NewGame(width: 80, height: 50);
			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player horde = Game.Instance.Players.First(p => p.Civilization is CivOne.Civilizations.Barbarian);
			City city = Game.Instance.AddCity(horde, 0, cx, cy)!;
			horde.Explore(cx, cy, range: 6);
			city.Size = 4;
			city.ResetResourceTiles();
			return city;
		}

		// The finding itself: raiders must never build a settler.
		[Fact]
		public void BarbarianCity_NeverBuildsASettler()
		{
			City city = BarbarianHeld();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.DoesNotContain(plan, p => p is Settlers);
		}

		// ...nor any of the civilisation apparatus. An Explorer is worthless to a player
		// with no research, no diplomacy and no map trading; a building is worse.
		[Fact]
		public void BarbarianCity_BuildsOnlyDefenders()
		{
			City city = BarbarianHeld();
			Sim.ClearTasks();

			AI.Instance(city.Player).CityProduction(city);

			Assert.NotNull(city.CurrentProduction);
			IUnit? unit = city.CurrentProduction as IUnit;
			Assert.True(unit is not null, $"barbarians should garrison, not develop; got {city.CurrentProduction!.GetType().Name}");
			Assert.Equal(UnitRole.Defense, unit!.Role);
			Assert.Empty(city.ProductionQueue);
		}

		// The gate is on the owner, not on city count — a captured city must not start
		// developing the moment the horde happens to hold two of them.
		[Fact]
		public void BarbarianCity_StaysDefensiveWithMoreThanOneCity()
		{
			City first = BarbarianHeld();
			Player horde = first.Player;
			horde.Explore(50, 25, range: 6);
			Map.Instance.ChangeTileType(50, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City second = Game.Instance.AddCity(horde, 1, 50, 25)!;
			second.Size = 4;
			second.ResetResourceTiles();
			Sim.ClearTasks();

			AI.Instance(horde).CityProduction(second);

			Assert.True(second.CurrentProduction is IUnit u && u.Role == UnitRole.Defense,
				$"got {second.CurrentProduction?.GetType().Name}");
		}

		// Control: the same ground under a real civ still gets developed, so the gate
		// cannot be passing by accident (e.g. by suppressing production everywhere).
		[Fact]
		public void ACivilisationOnTheSameGround_StillDevelops()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player player = Game.Instance.HumanPlayer;
			City city = Game.Instance.AddCity(player, 0, cx, cy)!;
			player.Explore(cx, cy, range: 6);
			city.Size = 4;
			city.ResetResourceTiles();
			Sim.ClearTasks();

			AI.Instance(player).CityProduction(city);

			Assert.NotNull(city.CurrentProduction);
			bool onlyDefender = city.CurrentProduction is IUnit d && d.Role == UnitRole.Defense
				&& city.ProductionQueue.Count == 0;
			Assert.False(onlyDefender, "a real civ should plan more than a lone defender");
		}
	}
}
