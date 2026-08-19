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

		// ── the lead is local ────────────────────────────────────────────────────

		// The Mongol shape, from the 13-civ run that motivated this rule.
		//
		// A civ that dominates every neighbour it has, while a far stronger civ sits on the
		// other side of the world. Under the old global clause this scored 0.78x and lost;
		// the shadow said total ascendancy and the lead said also-ran, because the two were
		// measured over different geographies. Now both are local.
		[Fact]
		public void ADistantTitanDoesNotBlockALocalAscendancy()
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();

			// The far civ is out of reach and enormous — four times our culture.
			far.SetCulture(3600);
			us.SetCulture(900);
			near.SetCulture(100);

			(int reach, int shadow, long bestNeighbour) = g.CulturalReachAndShadow(us);

			Assert.Equal(1, reach);
			Assert.Equal(1, shadow);
			Assert.Equal(100, bestNeighbour);   // the titan is NOT our yardstick
			Assert.True(us.Culture >= bestNeighbour * Game.CultureLeadMultiple,
				"a civ dominating every neighbour it has should clear its own bar");
		}

		// ...and the clause still bites. A strong neighbour IN range sets the bar, so the
		// local rule is not merely the shadow clause under another name.
		[Fact]
		public void AStrongNeighbourInRangeStillBlocksIt()
		{
			(Game g, Player us, Player near, Player far) = AWorldWithNeighbours();

			near.SetCulture(600);   // in range, and over half of our 900

			(int reach, int shadow, long bestNeighbour) = g.CulturalReachAndShadow(us);

			Assert.Equal(600, bestNeighbour);
			Assert.False(us.Culture >= bestNeighbour * Game.CultureLeadMultiple,
				"a neighbour this strong must still deny the ascendancy");
		}

		// End to end, through EndTurn: the streak must actually accrue with a titan abroad.
		//
		// The two tests above prove CulturalReachAndShadow REPORTS the right neighbour, but a
		// negative check showed they pass just as happily when the victory block ignores it —
		// they assert the comparison themselves rather than driving the rule. This one stages


		// The victory block must read the LOCAL figure. Pinned at the source because staging a
		// full 20-turn streak with a distant titan is a heavy fixture, and because the failure


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

		// ── the rule that replaced the shadow ────────────────────────────────────

		// Rank, not ratio. Culture per head converges as a game runs — everyone builds the
		// same things — so measured late leads ran 1.02-1.21x and any ratio bar is
		// unclearable. Being FIRST is the achievable form, and it is what the code asks.
		[Fact]
		public void TheVictoryIsAFirstRankNotARatio()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));

			Assert.Contains("bool foremost = claimant.Culture > 0 && densityRivals.All(p =>", src);
			// A rank with a modest margin, NOT a dominance ratio. The distinction is the whole
			// design: late leads run 1.02-1.21x, so a 2x-style bar is unclearable, while rank
			// alone froze into a coronation once the ordering settled.
			Assert.Contains("cultPerHead >= (double)p.Culture / rp * CultureLeadMargin", src);
			Assert.True(Game.CultureLeadMargin < Game.CultureLeadMultiple,
				"the margin has grown into a dominance bar");
			// the retired clauses
			Assert.DoesNotContain("bool reach   = shadow >= CulturalShadowTarget(inRange);", src);
			Assert.DoesNotContain("claimant.Culture >= bestNeighbour * CultureLeadMultiple", src);
		}

		// The populace floor. Culture is a cumulative stock and population is not, so a
		// stunted civ's culture per head climbs forever: the frozen one-city Japanese of run
		// 733f10ec led every density measure in that game. Relative to the field, so it scales
		// with the map rather than needing a new number per world size.
		//
		// Driven through the real helper rather than pinned on the source: the old version of
		// this test asserted a line of code, which said nothing about what the line computed.
		[Fact]
		public void AStuntedCivCannotRank()
		{
			// One relic among ordinary nations.
			long floor = Game.CulturalPopulaceFloor(new long[] { 3, 200, 250, 300, 400, 500 });

			Assert.True(3 < floor, $"the one-city relic ranks against a floor of {floor}");
			Assert.True(200 >= floor, $"an ordinary nation is excluded by a floor of {floor}");
		}

		// ...and the floor follows the MEDIAN, not the largest empire in the world. Run
		// 6da02a4d is the measurement: the Lakota on 1,718 populace put a quarter-of-the-
		// largest floor at 421, which refused Persia (297), the Khmer (244) and Japan (204) —
		// every civilization on the Culture path in that game, all three buying artists, none
		// of them able to rank. The Ascendancy went to a Conquest civ with no artists at all.
		[Fact]
		public void OneVastEmpireDoesNotDisqualifyTheField()
		{
			// The final populations of run 6da02a4d, Aztec rump and Lakota giant included.
			long[] world = { 3, 155, 204, 244, 277, 297, 333, 413, 430, 537, 568, 1072, 1317, 1718 };
			long floor = Game.CulturalPopulaceFloor(world);

			Assert.True(204 >= floor, $"Japan, on 15 cities, cannot rank against a floor of {floor}");
			Assert.True(244 >= floor, $"the Khmer, on 27 cities, cannot rank against {floor}");
			Assert.True(297 >= floor, $"Persia, on 35 cities, cannot rank against {floor}");
			Assert.True(3 < floor, $"the three-population Aztec rump ranks against {floor}");

			// The old rule, stated as the thing this must no longer do.
			Assert.True(floor < 1718 / 4, "the floor is still being drawn from the largest empire");
		}

		// A world of equals still has a floor — half of everyone is not a special case.
		[Fact]
		public void TheFloorSurvivesAFlatField()
		{
			long floor = Game.CulturalPopulaceFloor(new long[] { 400, 400, 400, 400 });

			Assert.True(floor > 0 && floor <= 400, $"floor {floor} is unusable in a flat field");
			Assert.True(400 >= floor, "nobody can rank in a world where every civ is identical");
		}

		// The date gate. At turn 200 of run 1ac32cee the leader held 31.9 against 14.3 — a
		// fine ratio over almost no culture — and a hold alone would have handed them the game
		// around turn 310. A golden age is sustained into the modern era, not seized in
		// antiquity.
		[Fact]
		public void TheClockCannotStartBeforeTheGateYear()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));

			Assert.Contains("bool modern = Common.TurnToYear(_gameTurn) >= CultureGateYear;", src);
			Assert.True(Game.CultureGateYear >= 1500, "a gate this early gates nothing");
		}

		// The hold has to be long enough to be a contest. Leads changed hands a median of 12
		// times a game across 21 measured runs; a short hold would make this a coronation.
		[Fact]
		public void TheHoldIsLongEnoughToBeContested()
		{
			Assert.True(Game.CultureHoldTurns >= 50,
				$"a {Game.CultureHoldTurns}-turn hold is not a contest");
		}

		// The whole point of the change: geography no longer decides who may compete. An
		// isolated civ — the Maori reached ZERO foreign cities across a whole game, the
		// Guarani one — is now judged on the same measure as everyone else.
		[Fact]
		public void GeographyNoLongerGatesThePath()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("bool admired = populous && foremost && modern;");
			Assert.True(at > 0, "the cultural clause has moved or been rewritten");
			string block = src.Substring(at, 200);

			Assert.Contains("geography no longer gates this path", block);
		}

		// ── driven through EndTurn, not read off the source ──────────────────────

		// The source assertions above pin the clause LINE; dropping any one clause from it
		// killed the same single test, which is not coverage of a victory condition. These
		// drive the rule and watch the streak.
		private static (Game game, Player us) AWorldReadyForAscendancy()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player[] rivals = g.Players
				.Where(p => p is not null && p != us && g.PlayerNumber(p) != 0).Take(3).ToArray();
			foreach (Player p in rivals.Append(us))
			{
				p.Government = new Monarchy();
				p.Explore(40, 25, range: 20);
			}
			us.AddAdvance(new Advances.Philosophy(), false);

			// Equal populations, so culture per head is decided by culture alone.
			g.AddCity(us, 0, 40, 25)!.Size = 6;
			int id = 1;
			foreach (Player r in rivals) g.AddCity(r, id, 34 + id++ * 3, 30)!.Size = 6;

			us.SetCulture(6000);
			foreach (Player r in rivals) r.SetCulture(600);

			g.GameTurn = (ushort)(400 + (Game.CultureGateYear - 1850) + 5);
			Sim.ClearTasks();
			return (g, us);
		}

		// A full round, not one EndTurn: EndTurn ends the CURRENT player's turn, and the
		// victory checks run once the turn itself advances. A single call left the streak at
		// zero and looked like the rule failing.
		private static uint StreakAfterATurn(Game g, Player us)
		{
			// Bounded: an unbounded wait on GameTurn hangs the suite when a fixture cannot
			// advance, which it did here before this guard.
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }
			Assert.True(g.GameTurn >= target, $"the fixture could not advance a turn (stuck at {g.GameTurn})");
			return g.Progress(g.PlayerNumber(us)).CultureStreak;
		}

		[Fact]
		public void TheStreakAccruesForTheForemostCulture()
		{
			(Game g, Player us) = AWorldReadyForAscendancy();

			Assert.True(StreakAfterATurn(g, us) > 0, "the leading culture earned no streak");
		}

		// Second place earns nothing — it is a rank, and only one civ holds it.
		[Fact]
		public void ASecondPlaceCultureEarnsNothing()
		{
			(Game g, Player us) = AWorldReadyForAscendancy();
			Player rival = g.Players.First(p => p is not null && p != us && g.PlayerNumber(p) != 0
			                                 && p.Cities.Any(c => c.Size > 0));
			rival.SetCulture(60000);   // now far ahead of us per head

			Assert.Equal(0u, StreakAfterATurn(g, us));
		}

		// Before the gate year, nothing accrues however dominant the culture.
		[Fact]
		public void NothingAccruesBeforeTheGateYear()
		{
			(Game g, Player us) = AWorldReadyForAscendancy();
			g.GameTurn = 300;
			Assert.True(Common.TurnToYear(g.GameTurn) < Game.CultureGateYear, "fixture is past the gate");

			Assert.Equal(0u, StreakAfterATurn(g, us));
		}

		// ...and a civ too small to rank earns nothing, however cultured per head. This is the
		// stunted-civ case: culture accumulates, population does not.
		[Fact]
		public void ACivBelowThePopulaceFloorEarnsNothing()
		{
			(Game g, Player us) = AWorldReadyForAscendancy();
			foreach (City c in us.Cities) c.Size = 1;   // a relic beside its neighbours
			us.SetCulture(60000);

			Assert.Equal(0u, StreakAfterATurn(g, us));
		}

		// A lead of a nose must not run out a hundred-turn clock. Rank alone gave culture 6 of
		// 8 games in a batch across 3-16 civs, five at turn 499-501 — the earliest the gate and
		// hold allow — with ZERO lead changes after 1850 in four of them. The ordering freezes
		// once culture per head converges, so the gate certified the 1850 leader rather than
		// opening a contest.
		[Fact]
		public void ANarrowLeadDoesNotAccrue()
		{
			(Game g, Player us) = AWorldReadyForAscendancy();
			Player rival = g.Players.First(p => p is not null && p != us && g.PlayerNumber(p) != 0
			                                 && p.Cities.Any(c => c.Size > 0));
			// Equal populations in this fixture, so culture is the whole ratio. Just inside
			// the margin: 6000 vs 5600 is 1.07x, under the 1.10x required.
			rival.SetCulture(5600);

			Assert.Equal(0u, StreakAfterATurn(g, us));
		}

		// ...and a clear lead does.
		[Fact]
		public void AClearLeadStillAccrues()
		{
			(Game g, Player us) = AWorldReadyForAscendancy();
			Player rival = g.Players.First(p => p is not null && p != us && g.PlayerNumber(p) != 0
			                                 && p.Cities.Any(c => c.Size > 0));
			rival.SetCulture(4000);   // 1.5x, comfortably over

			Assert.True(StreakAfterATurn(g, us) > 0, "a clear cultural lead earned no streak");
		}

		// The margin is deliberately modest — it separates the dominant from the marginal, it
		// is not a second dominance bar. Late leads measured 1.02-1.21x, so anything much
		// higher closes the path again.
		[Fact]
		public void TheMarginIsModest()
		{
			Assert.InRange(Game.CultureLeadMargin, 1.05, 1.30);
		}
	}
}
