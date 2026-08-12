// CivOne tests
//
// Cultural Ascendancy — the sixth victory path, and the peaceful mirror of conquest: cities
// come to you rather than being taken.
//
// The measure is the cultural SHADOW: foreign cities within 5 tiles of one of yours whose
// owner holds less than a third of your culture. That is exactly the eligibility test
// ProcessCultureDefections already uses to decide whether a city may change flags, minus the
// dice, the disorder and the garrison — so the victory is built on the same influence the
// flip mechanic models. Counting the flips themselves would be luck: an 8% roll, only on
// cities that happen to be rioting, at most one per turn in the whole world.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class CulturalAscendancyTests
	{
		// A cultured civ, a poor neighbour close by, and a poor neighbour far away.
		private static (Game game, Player us, Player near, Player far) AWorldWithNeighbours()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player us = ps[0], near = ps[1], far = ps[2];
			foreach (Player p in new[] { us, near, far })
			{
				p.Government = new Monarchy();
				p.Explore(45, 25, range: 30);
			}

			g.AddCity(us, 0, 40, 25)!.Size = 6;
			g.AddCity(near, 1, 43, 25)!.Size = 3;   // 3 tiles away — inside the shadow
			g.AddCity(far, 2, 65, 25)!.Size = 3;    // 25 tiles away — outside it

			us.SetCulture(900);
			near.SetCulture(100);   // 900 > 3x100, so it counts
			far.SetCulture(100);
			Sim.ClearTasks();
			return (g, us, near, far);
		}

		// The shadow is proximity AND dominance: a distant city does not count however poor.
		[Fact]
		public void OnlyNearbyCitiesCountTowardTheShadow()
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();

			Assert.Equal(1, g.CulturalShadow(us));
		}

		// ...and a neighbour who keeps up culturally leaves the shadow, even next door.
		[Fact]
		public void ANeighbourWhoKeepsUpIsNotInTheShadow()
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();
			Assert.Equal(1, g.CulturalShadow(us));

			near.SetCulture(400);   // 900 < 3x400

			Assert.Equal(0, g.CulturalShadow(us));
		}

		// The boundary is the same 5 tiles the defection mechanic reaches, not a new number.
		[Theory]
		[InlineData(5, 1)]
		[InlineData(6, 0)]
		public void TheShadowReachesExactlyAsFarAsDefectionDoes(int distance, int expected)
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();
			City n = g.GetCities().First(c => c.Owner == g.PlayerNumber(near));
			n.X = (byte)(40 + distance);

			Assert.Equal(expected, g.CulturalShadow(us));
		}

		// Barbarian towns are not an audience — nobody is admiring you from a raider camp.
		[Fact]
		public void BarbarianCitiesAreNotAnAudience()
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();
			Assert.Equal(1, g.CulturalShadow(us));

			City n = g.GetCities().First(c => c.Owner == g.PlayerNumber(near));
			n.Owner = 0;

			Assert.Equal(0, g.CulturalShadow(us));
		}

		// The story factions are excluded too — the Registry and the Machines do not admire
		// anybody, and a world they have occupied must not hand out a cultural victory. They
		// cannot be conjured into a fresh game (Skynet joins only when the uprising fires), so
		// this pins the predicate at the source, the same way EconomicHegemonyTests pins the
		// Pax Mercatoria exclusions.
		[Fact]
		public void TheShadowExcludesTheStoryFactions()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("internal int CulturalShadow(Player p)");
			Assert.True(at > 0, "CulturalShadow has moved or been rewritten");
			string body = src.Substring(at, src.IndexOf("\n\t\t}", at) - at);

			Assert.Contains("TheOthers", body);
			Assert.Contains("TheThing", body);
			Assert.Contains("Skynet", body);
		}

		private static string RepoRoot()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return dir!.FullName;
		}

		// The target scales with the map, like the AI's expansion target: a fixed count would
		// be trivial on an epic world and unreachable on a small one.
		[Fact]
		public void TheTargetScalesWithTheMap()
		{
			Sim.NewGame(width: 80, height: 50);
			int small = Game.Instance.CulturalShadowTarget;

			Assert.Equal(6, small);
			Assert.True(Game.CultureLeadMultiple >= 2, "a narrow lead should not read as admiration");
		}

		// The ending plate is shipped. A missing one degrades silently — EventArtScreen.FindPath
		// returns null and the win simply skips its picture — so the file is demanded here, the
		// same reason ProbeContactArtTests and LeaderPortraitTests exist. Checks the REPOSITORY
		// defaults rather than the player's install, which would test the machine.
		[Fact]
		public void TheEndingArtIsShipped()
		{
			string path = System.IO.Path.Combine(RepoRoot(), "runtime", "sdl", "Resources",
				"defaults", "data", "event_art", "CulturalAscendancy.png");

			Assert.True(System.IO.File.Exists(path), $"cultural ascendancy art is missing: {path}");
		}

		// The streak survives a save, like the economic one.
		[Fact]
		public void TheStreakRoundTripsThroughASave()
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();
			g.CultureStreak = 7;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "cultstreak.cos");

			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(7u, Game.Instance.CultureStreak);
		}
	}
}
