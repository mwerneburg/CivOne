// CivOne tests
//
// Railroads had no routing path. SettlerImprovementFor was happy to rail a tile, but
// BestImproveSite — which decides where the settler WALKS — rejected every tile that was
// already irrigated or mined, which is precisely the set of tiles a railroad belongs on.
// So rails only ever got built where a settler happened to be standing. A full game
// finished with seven railed tiles in the whole world.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class RailroadRoutingTests
	{
		// A city whose countryside is finished: every tile farmed and roaded.
		private static (Player player, IUnit settler) FinishedCountryside()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			byte id = Game.Instance.PlayerNumber(player);

			for (int y = 20; y <= 30; y++)
			for (int x = 15; x <= 25; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game.Instance.AddCity(player, 0, 20, 25);
			player.Explore(20, 25, range: 15);
			for (int y = 23; y <= 27; y++)
			for (int x = 18; x <= 22; x++)
			{
				Map.Instance[x, y].Irrigation = true;
				Map.Instance[x, y].Road = true;
			}
			return (player, Game.Instance.CreateUnit(UnitType.Settlers, 20, 24, id)!);
		}

		// Mines had the same missing routing path.
		[Fact]
		public void AHillIsRoutableEvenThoughItIsNotFarmable()
		{
			var (player, settler) = FinishedCountryside();
			Map.Instance.ChangeTileType(19, 26, Terrain.Hills);

			ITile? site = AI.Instance(player).BestImproveSite(settler);

			Assert.NotNull(site);
			Assert.True(site!.X == 19 && site.Y == 26,
				$"expected the unmined hill; got ({site.X},{site.Y}) {site.GetType().Name}");
		}

		// ...but food still comes first when there is farm work in range.
		[Fact]
		public void AHillNeverDisplacesFarmWork()
		{
			var (player, settler) = FinishedCountryside();
			Map.Instance.ChangeTileType(19, 26, Terrain.Hills);
			Map.Instance.ChangeTileType(21, 26, Terrain.Plains);
			Map.Instance[21, 26].Irrigation = false;

			ITile? site = AI.Instance(player).BestImproveSite(settler);

			Assert.NotNull(site);
			Assert.True(site!.X == 21 && site.Y == 26,
				$"the farm tile should win over the hill; got ({site.X},{site.Y})");
		}

		[Fact]
		public void WithoutRailroad_AFinishedCountrysideOffersNoWork()
		{
			var (player, settler) = FinishedCountryside();
			Assert.Null(AI.Instance(player).BestImproveSite(settler));
		}

		[Fact]
		public void WithRailroad_TheSettlerIsRoutedToARoadedTile()
		{
			var (player, settler) = FinishedCountryside();
			player.AddAdvance(new RailRoad(), false);

			ITile? site = AI.Instance(player).BestImproveSite(settler);

			Assert.NotNull(site);
			Assert.True(site!.Road && !site.RailRoad,
				$"expected a roaded, un-railed tile; got road {site.Road} rail {site.RailRoad}");
		}

		// Food still comes first: an unfarmed tile in range must outrank the rail upgrade.
		[Fact]
		public void RailNeverDisplacesFarmWork()
		{
			var (player, settler) = FinishedCountryside();
			player.AddAdvance(new RailRoad(), false);
			// Plains, not grassland: under Despotism the tile penalty makes irrigating
			// grassland worthless and BestImproveSite rightly skips it.
			Map.Instance.ChangeTileType(21, 26, Terrain.Plains);

			ITile? site = AI.Instance(player).BestImproveSite(settler);

			Assert.NotNull(site);
			Assert.True(site!.X == 21 && site.Y == 26,
				$"the unfarmed tile should win over any rail upgrade; got ({site.X},{site.Y})");
		}
	}
}
