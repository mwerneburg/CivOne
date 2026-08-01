// CivOne tests
//
// A treasury that cannot pay its upkeep used to simply not pay it: Player.Gold clamps at
// zero, so the shortfall was written off every turn, forever. Civ 1 sells a building.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class InsolvencyTests
	{
		private static City BrokeCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			// Feed the city, or it starves to size 0 and NewTurn returns before the
			// treasury is ever touched (City.cs:1445).
			for (int y = 20; y <= 30; y++)
			for (int x = 15; x <= 25; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			player.Explore(20, 25, range: 10);
			City city = Game.Instance.AddCity(player, 4, 20, 25)!;
			city.ResetResourceTiles();
			city.AddBuilding(new Barracks());     // upkeep 2
			city.AddBuilding(new CityWalls());    // upkeep 2
			player.Gold = 0;
			player.TaxesRate = 0;                 // nothing coming in
			return city;
		}

		[Fact]
		public void ACityThatCannotPay_SellsABuilding()
		{
			City city = BrokeCity();
			int before = city.Buildings.Length;

			city.NewTurn();

			Assert.True(city.Buildings.Length < before,
				$"an insolvent city should have sold something; still holds {city.Buildings.Length}");
		}

		[Fact]
		public void ASolventCityKeepsItsBuildings()
		{
			City city = BrokeCity();
			city.Player.Gold = 500;
			int before = city.Buildings.Length;

			city.NewTurn();

			Assert.Equal(before, city.Buildings.Length);
		}
	}
}
