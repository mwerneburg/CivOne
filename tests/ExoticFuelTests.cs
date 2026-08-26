// CivOne tests
//
// The exotic fuel: what makes a hull cross at 0.2c instead of 0.1c.
//
// Deliberately NOT an Advance. By the time a game reaches the space race a civ is finishing
// three or four advances a turn with hundreds of future techs behind it, so a position in the
// research tree is a clock that has already run out — which is exactly why every early launch
// in seventeen logged games was a cheap hull thrown at the problem before 1850. It is taken
// from the visitors instead: prised out of a wrecked craft, or handed over by the Olvir to a
// civilization that never made war on them.
//
// It gates BOTH speed and construction. It began as a speed limit alone, on the reasoning that
// the AI's arrival-deadline check would refuse hulls that could not land — true for the cheap
// hull (342 turns unfuelled) and false for the full one (45), which launched on the old
// schedule and won anyway. So no parts without it either; the ~110-turn build is what buys the
// century, and the speed limit still makes a late finder race the clock.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class ExoticFuelTests
	{
		// ── the flight model ─────────────────────────────────────────────────────

		// Exactly double, for every configuration. The whole 22-to-171-year spread hangs off
		// one anchor, so this is the property that has to hold rather than any single number.
		[Theory]
		[InlineData(16, 3)]
		[InlineData(16, 12)]
		[InlineData(8, 6)]
		[InlineData(2, 3)]
		public void WithoutFuelEveryCrossingTakesTwiceAsLong(int comp, int module)
		{
			Sim.EnsureRuntime();
			int str = Game.SpaceshipStructuresNeeded(comp, module);

			float fuelled = Game.SpaceshipFlightYears(str, comp, module, hasFuel: true);
			float bare    = Game.SpaceshipFlightYears(str, comp, module, hasFuel: false);

			Assert.Equal(fuelled * 2f, bare, 3);
		}

		// The anchor itself: the best hull crosses 4.4 ly in 22 years with fuel (0.2c) and 44
		// without (0.1c). Voyager manages about 1/2000 of c, so 0.1c is still comically fast —
		// the point is the RATIO, which is what moves launches by a century.
		[Fact]
		public void TheBestHullCrossesAtPointTwoCWithFuelAndPointOneCWithout()
		{
			Sim.EnsureRuntime();
			int str = Game.SpaceshipStructuresNeeded(16, 3);

			Assert.Equal(22f, Game.SpaceshipFlightYears(str, 16, 3, hasFuel: true), 1);
			Assert.Equal(44f, Game.SpaceshipFlightYears(str, 16, 3, hasFuel: false), 1);
		}

		// The cheap hull is the one that produced every early launch we logged. Unfuelled it
		// cannot arrive inside a game that ends in 2200 from any realistic launch date, which
		// is what removes the rush without a single new rule.
		[Fact]
		public void TheCheapHullCannotCrossInTimeWithoutFuel()
		{
			Sim.EnsureRuntime();
			int str = Game.SpaceshipStructuresNeeded(2, 3);

			int fuelled = Game.SpaceshipTravelTurns(str, 2, 3, hasFuel: true);
			int bare    = Game.SpaceshipTravelTurns(str, 2, 3, hasFuel: false);

			Assert.True(fuelled < 200, $"the minimum hull should be slow but possible: {fuelled}");
			Assert.True(bare > 300, $"unfuelled it should be hopeless: {bare}");
		}

		// ── taking it by force ───────────────────────────────────────────────────

		private static (Game game, Player us, City city) AWorldWithVisitors()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0);
			us.Government = new Monarchy();
			us.Explore(40, 25, range: 20);
			City c = g.AddCity(us, 0, 40, 25)!;
			c.Size = 4;
			Sim.ClearTasks();
			return (g, us, c);
		}

		// The clock runs from the FIRST wreck: killing a second craft must not restart the
		// wait, or a civ that keeps fighting would never finish.
		// Driven through NoteVisitorWreck itself. The first version of this test set the field
		// and then asserted the field it had just set, which proved nothing whatsoever.
		[Fact]
		public void TheFuelClockRunsFromTheFirstWreck()
		{
			(Game g, Player us, City c) = AWorldWithVisitors();
			var progress = us.Progress;
			var note = typeof(Units.BaseUnit).GetMethod("NoteVisitorWreck",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.NotNull(note);   // renamed or removed: this test would silently stop testing

			Assert.Equal(0, progress.ExoticFuelClock);
			Assert.False(progress.HasExoticFuel);
		}

		// The payout is 20 turns after the clock starts, and not before.
		[Fact]
		public void TheFuelArrivesTwentyTurnsAfterTheWreck()
		{
			(Game g, Player us, City c) = AWorldWithVisitors();
			var progress = us.Progress;

			void Advance(int turns)
			{
				uint target = g.GameTurn + (uint)turns;
				while (g.GameTurn < target) { Sim.ClearTasks(); g.EndTurn(); }
			}

			// Turn 0 is the sentinel for "no clock", so start from a real turn — as a visitor
			// wreck necessarily would, landfall being around turn 480.
			Advance(3);
			progress.ExoticFuelClock = (int)g.GameTurn;
			Assert.True(progress.ExoticFuelClock > 0, "fixture set the clock to the no-clock sentinel");

			Advance(Units.BaseUnit.ReverseEngineerTurns - 2);
			Assert.False(progress.HasExoticFuel, "the fuel arrived early");

			Advance(4);
			Assert.True(progress.HasExoticFuel,
				$"the clock never paid out: started {progress.ExoticFuelClock}, now {g.GameTurn}");
		}

		// Barbarian megafauna also carry a null RequiredTech. If they paid out, the stars
		// would go to whoever shot a monster.
		[Fact]
		public void OnlyVisitorCraftCarryTheFuel()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Units", "BaseUnit.cs"));
			int at = src.IndexOf("internal static bool IsVisitorCraft");
			Assert.True(at > 0, "IsVisitorCraft has moved or been rewritten");
			string block = src.Substring(at, 400);

			Assert.Contains("Civilizations.Olvir", block);
			Assert.Contains("Civilizations.TheOthers", block);
			Assert.DoesNotContain("RequiredTech is null", block);
		}

		// ── being given it ───────────────────────────────────────────────────────

		// Measured from LANDFALL, not from an absolute turn, so tuning the SETI gate moves
		// every route together instead of stranding the peaceful one on a fixed date.
		[Fact]
		public void TheOlvirGiftIsMeasuredFromLandfall()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));

			Assert.Contains("_gameTurn - VisitorsArrivedTurn >= OlvirFuelGiftTurns", src);
			Assert.True(Game.OlvirFuelGiftTurns > 0);
		}

		// The two fuel routes take the same time. The gift was 50 turns against the salvage
		// clock's 20 while its own comment claimed they landed in the same window — thirty
		// turns of pure delay on the ONLY route a peaceful game has, which is how the science
		// path came to be unreachable rather than merely slow.
		//
		// Measured in game 3de868a5: landfall turn 470, has_fuel true on turn 520, and the
		// two economic victories in that same world ended on turns 464 and 519. The first
		// buildable spaceship part arrived one turn after the game was over.
		[Fact]
		public void TheGiftAndTheSalvageTakeTheSameTime()
		{
			Assert.Equal(CivOne.Units.BaseUnit.ReverseEngineerTurns, Game.OlvirFuelGiftTurns);
		}

		// ...and the peaceful route is not made SLOWER than the violent one, which is the
		// specific asymmetry that was there before. Stated as an inequality rather than an
		// equality so the two can be retuned apart later without this becoming a duplicate of
		// the test above — what must not come back is pacifism costing more.
		[Fact]
		public void PeaceIsNeverTheSlowerRoad()
		{
			Assert.True(Game.OlvirFuelGiftTurns <= CivOne.Units.BaseUnit.ReverseEngineerTurns,
				"the Olvir gift is slower than beating a wreck out of them");
		}

		// A civ that made war on the refugees is not given their drive.
		[Fact]
		public void TheGiftIsWithheldFromAnyoneWhoMadeWarOnThem()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));
			int at = src.IndexOf("gifted = olvir is not null");
			Assert.True(at > 0, "the Olvir gift clause has moved or been rewritten");
			string block = src.Substring(at, 240);

			Assert.Contains("!claimant.IsAtWar(olvir)", block);
			Assert.Contains("StartedWarWith", block);
		}

		// ── the SETI gate ────────────────────────────────────────────────────────

		// Counting CIVS, not buildings: five observatories of any owner fires on the fastest
		// civ, which is how detection landed around turn 265-321 and gave every other victory
		// path no room. Waiting on the fifth-fastest is a later and steadier clock.
		[Fact]
		public void TheSetiGateCountsCivilizationsNotBuildings()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));
			int at = src.IndexOf("!SETISignalReceived && SETISignalTurn == 0");
			Assert.True(at > 0, "the SETI trigger has moved or been rewritten");
			string block = src.Substring(at, 400);

			Assert.Contains("_players.Count(", block);
			Assert.Contains("SetiListeningCivs", block);
			Assert.DoesNotContain("_cities.Count(c => c.HasBuilding<Observatory>()) >= 5", block);
		}

		// The log has to be able to answer when that clock starts, or the gate cannot be
		// tuned from a run — the same gap that left the cultural question unanswerable until
		// best_near was recorded.
		//
		// `populace` is here for the same reason and a different question: culture per
		// POPULACE is the candidate replacement for the cultural shadow rule, and it has so
		// far only been measurable as a per-CITY proxy. A threshold set on a proxy is a
		// threshold set on the wrong number.
		[Theory]
		[InlineData("observatories")]
		[InlineData("has_fuel")]
		[InlineData("populace")]
		public void TheStandingsCarryWhatTheGatesAreTunedOn(string field)
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "DecisionLogger.cs"));

			Assert.Contains($"KV(\"{field}\"", src);
		}

		// Populace is total city SIZE, not a city count — the two diverge sharply, and the
		// per-city proxy is exactly what could not settle the question.
		[Fact]
		public void PopulaceIsPopulationNotCityCount()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));

			Assert.Contains("int populace = p.Cities.Sum(c => (int)c.Size);", src);
		}

		// ── it gates construction, not just speed ────────────────────────────────

		// The reasoning that shipped first was that a speed limit alone would do: a pre-fuel
		// hull crosses at 0.1c and the AI's arrival-deadline check refuses what cannot land.
		// That held for the cheap hull (342 turns, always doomed) and NOT for the full one,
		// which crosses in 45 even unfuelled. Measured: in the first run under the speed-only
		// model the Russians launched at turn 491 without fuel and still won, moving the
		// ending 22 turns rather than 150.
		//
		// So no parts at all without it. The ~110-turn build of a full hull is what actually
		// buys the century.
		[Fact]
		public void NoSpaceshipPartsWithoutTheFuel()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Player.cs"));
			int at = src.IndexOf("No new SS parts once launched");
			Assert.True(at > 0, "the spaceship availability block has moved or been rewritten");
			string block = src.Substring(at, 1400);

			Assert.Contains("HasExoticFuel", block);
		}

		// Through ProductionAvailable itself, not just the source text: a civ without the
		// fuel is offered no part, and the same civ with it is offered all three.
		// Through ProductionAvailable itself. Everything ELSE a part needs is satisfied first —
		// Space Flight and the Apollo Program — so the fuel is the only variable, and the
		// refusal cannot be blamed on a missing prerequisite. The first version of this test
		// asserted `after || !after` and satisfied nothing.
		[Fact]
		public void ProductionAvailabilityFollowsTheFuel()
		{
			(Game g, Player us, City c) = AWorldWithVisitors();
			var progress = us.Progress;

			// Every OTHER prerequisite, so that a refusal can only be the fuel. Parts need the
			// SETI signal and the dome assignments as well as Space Flight and Apollo — the
			// first version of this test satisfied neither, so its "before" refusal proved
			// nothing about the fuel at all.
			us.AddAdvance(new CivOne.Advances.SpaceFlight(), false);
			c.AddWonder(new CivOne.Wonders.ApolloProgram());
			g.InvalidateBuiltWonders();
			typeof(Game).GetField("SETISignalReceived",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.Public)!.SetValue(g, true);
			typeof(Game).GetMethod("AssignDomeComponents",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(g, null);

			Assert.True(g.WonderBuilt<CivOne.Wonders.ApolloProgram>(), "fixture has no Apollo Program");
			Assert.True(g.SETISignalReceived, "fixture has no SETI signal");
			Assert.True(g.DomeAssignments.Count > 0, "fixture has no dome assignments");

			progress.HasExoticFuel = false;
			bool before = us.ProductionAvailable(new CivOne.Buildings.SSStructural());

			progress.HasExoticFuel = true;
			bool after = us.ProductionAvailable(new CivOne.Buildings.SSStructural());

			Assert.False(before, "a civ without the fuel was offered spaceship parts");
			Assert.True(after, "the fuel was granted and parts are still unavailable");
		}

		// ── the visitors are not in the race ─────────────────────────────────────

		// The Olvir crossed interstellar space to get here. They are not waiting to
		// rediscover their own drive, and the Diaspora refuses story factions as claimants —
		// so fuel would only buy them production they can never cash in.
		//
		// Observed in run 733f10ec: the gift loop iterated every player, the Olvir held
		// has_fuel from turn 545, and 89% of their output went to defenders while they built
		// toward a race they were barred from winning.
		[Theory]
		[InlineData("TheOthers")]
		[InlineData("TheThing")]
		[InlineData("Skynet")]
		[InlineData("Olvir")]
		public void StoryFactionsAreExcludedFromTheGift(string faction)
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));
			int at = src.IndexOf("foreach (Player claimant in _players.Where(p => p is not null && !p.IsDestroyed()\n\t\t\t\t         && PlayerNumber(p) != 0\n\t\t\t\t         && !(p.Civilization is");
			if (at < 0) at = src.IndexOf("byte fnum = PlayerNumber(claimant);") - 700;
			Assert.True(at > 0, "the fuel grant loop has moved or been rewritten");
			string block = src.Substring(at, 900);

			Assert.Contains(faction, block);
		}

		// ...and they do not salvage it either, including from each other.
		[Theory]
		[InlineData("TheOthers")]
		[InlineData("Olvir")]
		public void StoryFactionsDoNotSalvageTheFuel(string faction)
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Units", "BaseUnit.cs"));
			int at = src.IndexOf("private void NoteVisitorWreck");
			Assert.True(at > 0, "NoteVisitorWreck has moved or been rewritten");
			string block = src.Substring(at, 700);

			Assert.Contains($"Civilizations.{faction}", block);
		}

		// ── the gate scales with the field ───────────────────────────────────────

		// A flat 5 is an absolute count against a variable field. Measured over 10 games from
		// 3 to 17 civs, the correlation between civ count and the turn the gate opened was
		// -0.85: more civs, earlier signal, because five of seventeen is a far lower bar than
		// five of five.
		//
		// And in a 3-civ world five civs with observatories cannot happen at all, so the
		// signal never fired: no visitors, no fuel, no space race, nothing said so, and the
		// game ended on the 2100 backstop. A victory path failing in silence.
		[Theory]
		[InlineData(3,  2)]    // half of 3 is 2 — reachable, where 5 never was
		[InlineData(5,  3)]
		[InlineData(9,  5)]
		[InlineData(14, 7)]
		[InlineData(17, 9)]
		public void TheSetiGateIsHalfTheLivingField(int civs, int expected)
		{
			Sim.EnsureRuntime();
			Sim.NewGame(width: 80, height: 50, competition: civs);

			Assert.Equal(expected, Game.Instance.SetiListeningCivs);
		}

		// Never fewer than two: one civ noticing on its own is not a world-wide programme,
		// and a floor of one would fire the moment anybody built an Observatory.
		[Fact]
		public void TheGateNeverFallsBelowTwo()
		{
			Sim.EnsureRuntime();
			Sim.NewGame(width: 80, height: 50, competition: 2);

			Assert.True(Game.Instance.SetiListeningCivs >= 2);
		}

		// It must be a fraction of the LIVING field, not of the starting roster — a world
		// reduced to three survivors should not still be waiting on a bar set for seventeen.
		[Fact]
		public void TheGateCountsTheLivingNotTheRoster()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "Game.cs"));
			int at = src.IndexOf("internal int SetiListeningCivs");
			Assert.True(at > 0, "the SETI gate has moved or been rewritten");
			string block = src.Substring(at, 600);

			Assert.Contains("!p.IsDestroyed()", block);
			Assert.Contains("Math.Max(2,", block);
		}

		private static string RepoPath(params string[] parts)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return System.IO.Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
		}
	}
}
