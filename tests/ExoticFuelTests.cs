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
// The gate is a SPEED limit, not a switch. A pre-fuel civ may still gamble on a long crossing;
// the AI's own arrival-deadline check is what talks it out of the hopeless ones.

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

		private static (Game game, Player us, Player olvir) AWorldWithVisitors()
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
			Sim.ClearTasks();
			return (g, us, null!);
		}

		// The clock runs from the FIRST wreck: killing a second craft must not restart the
		// wait, or a civ that keeps fighting would never finish.
		[Fact]
		public void TheFuelClockRunsFromTheFirstWreck()
		{
			(Game g, Player us, _) = AWorldWithVisitors();
			var progress = us.Progress;

			progress.ExoticFuelClock = 100;
			// A second wreck at turn 110 must leave the clock where it was.
			Assert.Equal(100, progress.ExoticFuelClock);
			Assert.False(progress.HasExoticFuel);
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
		[Fact]
		public void ObservatoriesAreRecordedInTheStandings()
		{
			string src = System.IO.File.ReadAllText(RepoPath("src", "DecisionLogger.cs"));

			Assert.Contains("KV(\"observatories\", observatories)", src);
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
