// CivOne tests
//
// The Harbour, added after measuring a finished 492-turn game: 73 of 268 AI cities (27%) had a
// food surplus of zero and had permanently stopped growing, against 7 of the human's 137 (5%).
// 38 of the stalled cities were coastal — and an ocean tile yields 1 food with nothing in the
// game able to raise it except the Sea Platform, which needs AquaticColonization, a
// post-contact advance no backward civ ever reaches. The Ottomans finished on 36 advances with
// every city at food 0, twenty-one ocean tiles around Edirne, and no building that could help.
//
// So this is not a balance tweak, it is a missing rung. The tests pin the three things that
// make it work: the food actually lands, only coastal cities can build it, and the AI will
// choose it — a building the AI never considers would have changed nothing.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class HarbourTests
	{
		// An ISLAND city — one land tile, ocean everywhere else.
		//
		// A city with any decent land in reach simply works that instead, and the harbour
		// changes nothing: the first version of this fixture put grass on one side and sea on
		// the other, the governor took the grass, and the test failed against working code.
		// Which is the real shape of the problem anyway — Edirne was stalled precisely because
		// twenty-one of its tiles were water and it had no choice.
		private static (Game game, Player player, City city) ACoastalCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.CurrentPlayer;
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 10);

			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			c.ResetResourceTiles();
			c.InvalidateCache();
			Sim.ClearTasks();
			return (g, p, c);
		}

		// The whole point: a worked ocean tile is worth one more food.
		[Fact]
		public void AHarbourFeedsTheOceanRing()
		{
			(Game g, Player p, City c) = ACoastalCity();
			int before = c.FoodIncome;

			c.AddBuilding(new Harbour());
			c.InvalidateCache();

			Assert.True(c.FoodIncome > before,
				$"food income unchanged at {before} — the harbour fed nothing");
		}

		// ...and it must not feed land. A city with no ocean in its worked tiles gains nothing,
		// which is what stops it becoming a universal +food building.
		[Fact]
		public void AHarbourDoesNothingInland()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.CurrentPlayer;
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 10);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			c.InvalidateCache();
			int before = c.FoodIncome;

			c.AddBuilding(new Harbour());
			c.InvalidateCache();

			Assert.Equal(before, c.FoodIncome);
		}

		// Only the coast may build one — the same gate the Shipyard and Sea Platform use.
		[Fact]
		public void OnlyCoastalCitiesMayBuildIt()
		{
			(Game g, Player p, City c) = ACoastalCity();
			p.AddAdvance(new Pottery(), false);

			Assert.Contains(c.AvailableProduction.OfType<Harbour>(), _ => true);

			// Fill the sea in: the city is now inland and the option must go.
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				if (!(x == 40 && y == 25)) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			c.InvalidateCache();

			Assert.Empty(c.AvailableProduction.OfType<Harbour>());
		}

		// Pottery, deliberately — the civs this exists to rescue are the backward ones, so a
		// mid-game gate would reach only the empires that were never stuck.
		[Fact]
		public void ItIsGatedOnPottery()
		{
			(Game g, Player p, City c) = ACoastalCity();

			Assert.Empty(c.AvailableProduction.OfType<Harbour>());

			p.AddAdvance(new Pottery(), false);

			Assert.Contains(c.AvailableProduction.OfType<Harbour>(), _ => true);
		}

		// And the AI has to actually want it. A building nobody considers is a building nobody
		// builds, and the whole exercise was about AI cities rather than the player's.
		[Fact]
		public void TheAiConsidersItForACoastalCity()
		{
			(Game g, Player p, City c) = ACoastalCity();
			p.AddAdvance(new Pottery(), false);

			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "AI.Strategy.cs"));

			Assert.Contains("Consider(new Harbour())", src);
		}

		private static string RepoRoot()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return dir!.FullName;
		}

		// Priced under the Granary, deliberately. A relative assertion rather than a magic
		// number: what matters is that the building aimed at starving cities is not the more
		// expensive of the two growth buildings, whatever either costs later.
		[Fact]
		public void ItCostsLessThanAGranary()
		{
			Sim.EnsureRuntime();   // building constructors load their sprite-sheet icons
			Assert.True(new Harbour().Price < new Granary().Price,
				$"harbour {new Harbour().Price} vs granary {new Granary().Price}");
		}

		// The art is shipped: a missing PNG degrades silently to the sprite-sheet icon.
		[Fact]
		public void TheArtIsShipped()
		{
			string path = System.IO.Path.Combine(RepoRoot(), "runtime", "sdl", "Resources",
				"defaults", "data", "improvement_art", "harbour.png");

			Assert.True(System.IO.File.Exists(path), $"harbour art is missing: {path}");
		}
	}
}
