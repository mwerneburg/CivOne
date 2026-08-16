// CivOne tests
//
// Cultural Ascendancy — the sixth victory path, and the peaceful mirror of conquest: cities
// come to you rather than being taken.
//
// The measure is the cultural SHADOW: foreign cities within 5 tiles of one of yours whose
// owner holds less than HALF your culture. That began as the same eligibility test
// ProcessCultureDefections uses to decide whether a city may change flags, minus the dice,
// the disorder and the garrison — but at a third nothing ever qualified, so the two now
// differ deliberately (see Game.CultureShadowRatio). Counting the flips themselves would
// still be luck: an 8% roll, only on rioting cities, at most one per turn in the world.

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
			near.SetCulture(100);   // 900 > 2x100, so it counts
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

			// Just over the line: 500 x 2 = 1000, above our 900. Expressed against the constant
			// so the boundary moves with the rule rather than needing a hand-edit — this used
			// to read 400, which was outside the shadow at a ratio of 3 and inside it at 2.
			near.SetCulture(500);
			Assert.True(500 * Game.CultureShadowRatio > 900, "fixture no longer straddles the line");

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

		// The target is drawn from the claimant's OWN reach, not from the map.
		//
		// It used to be 6 * (Map.WIDTH / 80), which is why this test used to assert 6 on an
		// 80-wide world. That scaling was backwards: reach comes from crowding, and a wider
		// map spreads civilizations apart, so the epic map raised the bar while lowering the
		// jump. A measured 10-civ epic game had the culture leader on reach 4 against a
		// target of 24.
		//
		// Above the floor the target must never exceed the reach it came from, or the rule
		// re-closes itself the moment a civ is isolated.
		[Theory]
		[InlineData(10, 6)]    // floor holds: 3/5 of 10 is 6
		[InlineData(20, 12)]
		[InlineData(42, 25)]   // the crowded 16-civ game, which is the one that ever cleared it
		public void TheTargetIsThreeFifthsOfReach(int reach, int expected)
		{
			Assert.Equal(expected, Game.CulturalShadowTarget(reach));
		}

		// Anything at or above the floor must be winnable in principle. This is the property
		// the old rule violated, and the reason the path never once resolved.
		[Theory]
		[InlineData(6)]
		[InlineData(7)]
		[InlineData(21)]
		[InlineData(42)]
		[InlineData(255)]
		public void TheTargetIsNeverBeyondReach(int reach)
		{
			Assert.True(Game.CulturalShadowTarget(reach) <= reach,
				$"reach {reach} demands {Game.CulturalShadowTarget(reach)} — arithmetically closed");
		}

		// Below the floor the path IS shut, deliberately: two neighbours in range is not a
		// world that can admire you. Pinned so the exclusion stays a decision.
		[Theory]
		[InlineData(0)]
		[InlineData(2)]
		[InlineData(5)]
		public void TooFewNeighboursMeansNoCulturalWin(int reach)
		{
			Assert.True(Game.CulturalShadowTarget(reach) > reach);
			Assert.Equal(Game.CulturalShadowFloor, Game.CulturalShadowTarget(reach));
		}

		// A neighbour at 40% of your culture is in your shadow; one at 60% is not.
		//
		// The ratio was 3, and no measured game ever produced a neighbour under a third: best
		// dominance was 26% of cities in range against a 60% target, with peak shadow of 1 in
		// a 13-civ game and 0 in a 3-civ one. The field these games grow is flat.
		// Driven through CulturalShadow itself rather than arithmetic on the constant, so it
		// proves the RULE and not the number: 400 against our 900 is 44%, shadowed at a ratio
		// of 2 and not at 3, which is exactly the band every measured game lived in.
		[Theory]
		[InlineData(400, 1)]   // 44% — inside
		[InlineData(600, 0)]   // 67% — keeping up, outside
		public void ANeighbourUnderHalfYourCultureIsInYourShadow(int neighbourCulture, int expected)
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();

			near.SetCulture(neighbourCulture);

			Assert.Equal(expected, g.CulturalShadow(us));
		}

		// Defection keeps the harder third. A city changing flags is a headline event and must
		// not become as common as the standing measurement — pinned so the two cannot be
		// silently re-coupled.
		[Fact]
		public void DefectionIsStrictlyHarderThanTheShadow()
		{
			string body = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));

			Assert.Contains("p.Culture >= owner.Culture * 3", body);
			Assert.True(Game.CultureShadowRatio < 3, "the shadow must be looser than defection");
		}

		[Fact]
		public void ANarrowLeadIsNotAdmiration()
		{
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
			g.Progress(g.PlayerNumber(g.HumanPlayer)).CultureStreak = 7;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "cultstreak.cos");

			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(7u, Game.Instance.Progress(Game.Instance.PlayerNumber(Game.Instance.HumanPlayer)).CultureStreak);
		}
	}
}
