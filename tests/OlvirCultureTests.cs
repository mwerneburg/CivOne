// CivOne tests
//
// The Olvir finished a 750-turn game with 35 cities and 0 culture. Nothing excluded them:
// City.CultureRate counts Temple, Colosseum, Library, Cathedral, University, Civic
// Monument, The Internet and wonders — and the Olvir production list (AI.Strategy:3033)
// is defender, granary, settlers, HydroEngineer, Xenolab, Sea Platform. The two sets did
// not intersect at a single entry, so zero was the only value their culture could take.
//
// The Breeding Shrine and the Cascade Cathedral are entries they can reach: their own
// religion, gated by civilization rather than technology.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class OlvirCultureTests
	{
		private static (Game, Player olvir, Player human) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player olvir = g.Players.FirstOrDefault(p => p is not null
			                    && p.Civilization is Civilizations.Olvir)!;
			if (olvir is null)
			{
				// The Olvir join mid-game; for a unit test, seat one directly.
				olvir = new Player(Common.Civilizations.First(c => c is Civilizations.Olvir));
				g.AddPlayer(olvir);
			}
			return (g, olvir, g.HumanPlayer);
		}

		private static City AnOlvirCity(Game g, Player olvir, int x = 40, int y = 25)
		{
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			olvir.Explore(x, y, range: 3);
			City c = g.AddCity(olvir, 0, x, y)!;
			c.Size = 8;
			return c;
		}

		// The defect, stated directly: an Olvir city with their own buildings earns culture.
		[Fact]
		public void AnOlvirCityWithAShrineEarnsCulture()
		{
			(Game g, Player olvir, Player human) = AWorld();
			City c = AnOlvirCity(g, olvir);
			Assert.Equal(0, c.CultureRate);

			c.AddBuilding(new BreedingShrine());
			Assert.True(c.CultureRate > 0, "a Breeding Shrine must earn culture");

			int withShrine = c.CultureRate;
			c.AddBuilding(new CascadeCathedral());
			Assert.True(c.CultureRate > withShrine, "the cathedral must add more");
		}

		// Only the Olvir. A human city must never be offered the spawning rite.
		[Fact]
		public void HumansCannotBuildTheOlvirReligion()
		{
			(Game g, Player olvir, Player human) = AWorld();
			Assert.False(human.ProductionAvailable(new BreedingShrine()));
			Assert.False(human.ProductionAvailable(new CascadeCathedral()));
		}

		[Fact]
		public void TheOlvirCanBuildTheShrine()
		{
			(Game g, Player olvir, Player human) = AWorld();
			AnOlvirCity(g, olvir);
			Assert.True(olvir.ProductionAvailable(new BreedingShrine()));
		}

		// The cathedral is the shrine grown monumental — no colony plumbs one first.
		[Fact]
		public void TheCathedralRequiresAShrineSomewhere()
		{
			(Game g, Player olvir, Player human) = AWorld();
			City c = AnOlvirCity(g, olvir);
			Assert.False(olvir.ProductionAvailable(new CascadeCathedral()));

			c.AddBuilding(new BreedingShrine());
			Assert.True(olvir.ProductionAvailable(new CascadeCathedral()));
		}
	}
}
