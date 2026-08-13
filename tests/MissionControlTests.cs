// CivOne tests
//
// Mission Control: the ground half of the colony programme, and the city a rival can take to
// end somebody's spaceship victory. Before it existed, nothing any other civilization did
// could touch a ship already under way.
//
// The interesting rule is ONE PER CIVILIZATION, which the engine has exactly one precedent
// for — the Palace (City.cs, production completion), where building a second removes every
// other copy the owner holds. Mission Control borrows that and nothing else: no Courthouse
// displacement, no capital-relocation cutscene.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class MissionControlTests
	{
		private static (Game game, Player player, City a, City b) TwoCities()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			// The HUMAN's cities, not the current player's. At turn 0 the current player is an
			// AI, and its governor rewrites production every turn — SetProduction was being
			// silently replaced with Militia, which looked like the completion path failing.
			Player p = g.HumanPlayer;
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 15);
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			City a = g.AddCity(p, 0, 38, 25)!;
			City b = g.AddCity(p, 1, 46, 25)!;
			// Size 4, not 8: an 8 under Monarchy with no Temple riots, and a city in disorder
			// has its production reset to Militia and its shields zeroed — which looked exactly
			// like the one-per-civ rule failing.
			a.Size = 4; b.Size = 4;
			p.AddAdvance(new SpaceFlight(), false);
			Sim.ClearTasks();
			return (g, p, a, b);
		}

		// Finish a building the way the production loop does.
		private static void Complete(City city, IBuilding building)
		{
			city.SetProduction(building);
			city.Shields = (short)((int)building.Price * 10);
			city.NewTurn();
			Sim.Settle();
		}

		[Fact]
		public void ASecondMissionControlMovesTheFirst()
		{
			(Game g, Player p, City a, City b) = TwoCities();

			Complete(a, new MissionControl());
			Assert.True(a.HasBuilding<MissionControl>(), "the first one was not built");

			Complete(b, new MissionControl());

			Assert.True(b.HasBuilding<MissionControl>(), "the second one was not built");
			Assert.False(a.HasBuilding<MissionControl>(), "the civilization now has two");
		}

		// Exactly one, empire-wide — the property the victory will lean on.
		[Fact]
		public void ACivilizationNeverHoldsMoreThanOne()
		{
			(Game g, Player p, City a, City b) = TwoCities();
			Complete(a, new MissionControl());
			Complete(b, new MissionControl());

			int held = g.GetCities().Count(c => c.Owner == g.PlayerNumber(p) && c.HasBuilding<MissionControl>());

			Assert.Equal(1, held);
		}

		// ...but it does not reach into another civilization's cities.
		[Fact]
		public void AnotherCivilizationKeepsItsOwn()
		{
			(Game g, Player p, City a, City b) = TwoCities();
			Player other = g.Players.First(x => x is not null && x != p && g.PlayerNumber(x) != 0);
			other.Explore(60, 25, range: 8);
			other.AddAdvance(new SpaceFlight(), false);
			City theirs = g.AddCity(other, 2, 60, 25)!;
			theirs.Size = 4;
			// Placed directly: an AI city's production is rewritten by its governor, and what
			// this test cares about is whether OUR completion reaches across the border.
			theirs.AddBuilding(new MissionControl());

			Complete(a, new MissionControl());

			Assert.True(theirs.HasBuilding<MissionControl>(), "a rival's programme was cancelled from abroad");
			Assert.True(a.HasBuilding<MissionControl>());
		}

		[Fact]
		public void ItIsGatedOnSpaceFlight()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			p.Explore(40, 25, range: 8);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 4;

			Assert.Empty(c.AvailableProduction.OfType<MissionControl>());

			p.AddAdvance(new SpaceFlight(), false);

			Assert.Contains(c.AvailableProduction.OfType<MissionControl>(), _ => true);
		}

		// A missing PNG degrades silently to the sprite-sheet icon, so demand the file.
		[Fact]
		public void TheArtIsShipped()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string path = System.IO.Path.Combine(dir!.FullName, "runtime", "sdl", "Resources",
				"defaults", "data", "improvement_art", "mission_control.png");

			Assert.True(System.IO.File.Exists(path), $"mission control art is missing: {path}");
		}
	}
}
