// CivOne tests
//
// Map.ContentCities scanned every tile on the board to find the cities on a continent.
// Written for Civ 1's 80x50 map, it cost 64,000 tile visits per call on a 320x200 one,
// and ComputeCitizens calls it twice per city per turn (J.S. Bach, Michelangelo) — ~57M
// tile visits a turn at 443 cities, and the single largest cost in a 2196 AD run.
// It now walks the city list instead. These pin the set it returns.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class ContentCitiesTests
	{
		private static (Game, Player) World()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && x != g.HumanPlayer);
			return (g, p);
		}

		// The set must match what a full tile scan would have found, continent by continent.
		[Fact]
		public void ItReturnsTheSameCitiesAFullTileScanWould()
		{
			(Game g, Player p) = World();
			for (int i = 0; i < 12; i++)
			{
				int x = 5 + i * 6, y = 10 + (i % 3) * 8;
				p.Explore(x, y, range: 3);
				g.AddCity(p, (byte)i, x, y);
			}

			var continents = g.GetCities().Select(c => c.Tile!.ContinentId).Distinct().ToArray();
			Assert.NotEmpty(continents);

			foreach (byte id in continents)
			{
				var scanned = Map.Instance.ContinentTiles(id)
					.Where(t => t.City is not null).Select(t => t.City).OrderBy(c => (c.X, c.Y)).ToArray();
				var listed = Map.Instance.ContentCities(id).OrderBy(c => (c.X, c.Y)).ToArray();
				Assert.Equal(scanned, listed);
			}
		}

		// A city on another continent must not leak in — this is what the wonder checks
		// (J.S. Bach, Michelangelo) actually depend on.
		[Fact]
		public void ACityOnAnotherContinent_IsNotIncluded()
		{
			(Game g, Player p) = World();
			// Drown everything, then two landmasses with a clear sea gap.
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				if (!Map.Instance[x, y].IsOcean) Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 10; y < 15; y++) for (int x = 10; x < 15; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			for (int y = 30; y < 35; y++) for (int x = 40; x < 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			p.Explore(12, 12, range: 5);
			p.Explore(42, 32, range: 5);
			City home  = g.AddCity(p, 0, 12, 12)!;
			City away  = g.AddCity(p, 1, 42, 32)!;

			byte homeId = home.Tile!.ContinentId;
			Assert.NotEqual(homeId, away.Tile!.ContinentId);

			var listed = Map.Instance.ContentCities(homeId).ToArray();
			Assert.Contains(home, listed);
			Assert.DoesNotContain(away, listed);
		}
	}
}
