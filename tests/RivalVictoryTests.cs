// CivOne tests
//
// Victories are decided for EVERY civilization, not just the human.
//
// Economic dominance, cultural ascendancy and Diaspora all tested HumanPlayer alone, so an
// AI could hold every condition for centuries and nothing would happen. Measured in a
// finished 2200 AD game: the Others held 64.7% of the world's cities, 4.25x the culture of
// the best rival and 47% of world output, and could win by none of it. In another, the
// Lakota landed a colony at Alpha Centauri 98 turns before the human and it counted for
// nothing — the human launched later and won.
//
// The four streak counters are per player now, and a rival taking a victory ends the run as
// a loss, the way the 2100 score ending already did.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;

namespace CivOne.Tests
{
	public class RivalVictoryTests
	{
		private static (Game g, Player human, Player rival) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player human = g.HumanPlayer;
			Player rival = g.Players.First(p => p is not null && p != human && g.PlayerNumber(p) != 0);
			Sim.ClearTasks();
			return (g, human, rival);
		}

		// ── per-civ state ────────────────────────────────────────────────────────

		// The streaks are independent. One civ's progress must not read as another's — which
		// is exactly what a single shared counter did.
		[Fact]
		public void StreaksAreHeldPerCivilization()
		{
			(Game g, Player human, Player rival) = AWorld();
			byte h = g.PlayerNumber(human), r = g.PlayerNumber(rival);

			g.EconStreak[r] = 7;
			g.CultureStreak[r] = 11;
			g.DiasporaStreak[r] = 3;
			g.ColonyFounded[r] = true;

			Assert.Equal(0u, g.EconStreak[h]);
			Assert.Equal(0u, g.CultureStreak[h]);
			Assert.Equal(0u, g.DiasporaStreak[h]);
			Assert.False(g.ColonyFounded[h]);
		}

		// War initiation is recorded for everyone, because the aggression clause on both
		// streak victories now applies to everyone. The alternative was giving the AI a
		// different rule from the human, which is a thumb on the scale.
		[Fact]
		public void WarInitiationIsRecordedForEveryCivilization()
		{
			(Game g, Player human, Player rival) = AWorld();
			byte h = g.PlayerNumber(human), r = g.PlayerNumber(rival);

			rival.DeclareWar(human);

			Assert.True(g.StartedWarWith(r, h), "the rival's own war was not recorded");
			Assert.False(g.StartedWarWith(h, r), "the human was blamed for a war it did not start");
		}

		// ...and forgotten on peace, from both directions.
		[Fact]
		public void MakingPeaceClearsTheAggressorRecord()
		{
			(Game g, Player human, Player rival) = AWorld();
			byte h = g.PlayerNumber(human), r = g.PlayerNumber(rival);
			rival.DeclareWar(human);

			rival.MakePeace(human);

			Assert.False(g.StartedWarWith(r, h));
		}

		// ── the first-mover premium ──────────────────────────────────────────────

		// Being first to another star is the achievement. The fifth ship to make the same
		// crossing has proved nothing new — and in one measured game five civs launched.
		[Theory]
		[InlineData(1, 400)]
		[InlineData(2, 200)]
		[InlineData(3, 100)]
		[InlineData(4, 50)]
		[InlineData(9, 50)]
		public void LaterColoniesAreWorthLess(int arrivalOrder, int expected)
		{
			Assert.Equal(expected, Game.DiasporaAward(arrivalOrder));
		}

		// A colony with no recorded order is the only one we know of, so it is treated as
		// first rather than as worthless.
		[Fact]
		public void AnUnrecordedColonyCountsAsTheFirst()
		{
			Assert.Equal(Game.DiasporaAward(1), Game.DiasporaAward(0));
		}

		// The premium must actually diminish, not merely differ.
		[Fact]
		public void TheAwardNeverIncreasesWithArrivalOrder()
		{
			for (int i = 1; i < 8; i++)
				Assert.True(Game.DiasporaAward(i) >= Game.DiasporaAward(i + 1),
					$"colony {i + 1} was worth more than colony {i}");
		}

		// ── persistence ──────────────────────────────────────────────────────────

		// Per-civ progress has to survive a save, or a reloaded game hands the first-landing
		// prize out a second time.
		[Fact]
		public void PerCivilizationProgressRoundTripsThroughASave()
		{
			(Game g, Player human, Player rival) = AWorld();
			byte r = g.PlayerNumber(rival);
			g.EconStreak[r] = 9;
			g.CultureStreak[r] = 4;
			g.DiasporaStreak[r] = 6;
			g.ColonyFounded[r] = true;
			g.ColonyOrder[r] = 2;
			g.RecordWarStart(r, g.PlayerNumber(human));

			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "rivalvictory.cos");
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");
			Game g2 = Game.Instance;

			Assert.Equal(9u, g2.EconStreak[r]);
			Assert.Equal(4u, g2.CultureStreak[r]);
			Assert.Equal(6u, g2.DiasporaStreak[r]);
			Assert.True(g2.ColonyFounded[r]);
			Assert.Equal(2, g2.ColonyOrder[r]);
			Assert.True(g2.StartedWarWith(r, g2.PlayerNumber(g2.HumanPlayer)));
		}

		// ── who may claim ────────────────────────────────────────────────────────

		// Story factions are excluded as CLAIMANTS on both streak victories, not merely as
		// rivals. The Registry empties cities rather than running them; letting the occupier
		// win Pax Mercatoria contradicts the rule that its economy counts toward the total
		// precisely because an occupied world has no commercial hegemon. Pinned at the source
		// because staging a Registry invasion to prove it would test the invasion.
		[Theory]
		[InlineData("EconStreak.Length")]
		[InlineData("CultureStreak.Length")]
		public void NeitherStreakVictoryCanBeClaimedByAStoryFaction(string anchor)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "Game.cs"));

			int at = src.IndexOf(anchor);
			Assert.True(at > 0, $"the {anchor} loop has moved or been rewritten");
			// The exclusion must sit on the claimant filter just above the guard.
			string loop = src.Substring(System.Math.Max(0, at - 700), 700);
			Assert.Contains("Civilizations.TheOthers", loop);
			Assert.Contains("Civilizations.Skynet", loop);
		}

		// ── the ending ───────────────────────────────────────────────────────────

		// A rival completing the Diaspora fires the ending — and does NOT award the human.
		//
		// Driven through EndTurn the way DiasporaTests does: Sim.RunTurns plays the game, and
		// this fixture gives it almost nothing to play. Asserted on the latch and on the
		// human's score rather than on a logging hook, because adding a hook to production
		// code purely to observe a test is the wrong trade.
		[Fact]
		public void ARivalDiasporaFiresTheEndingWithoutRewardingTheHuman()
		{
			(Game g, Player human, Player rival) = AWorld();
			byte r = g.PlayerNumber(rival);
			rival.AddAdvance(new SpaceFlight(), false);
			City hq = g.AddCity(rival, 0, 40, 25)!;
			hq.Size = 4;
			hq.AddBuilding(new MissionControl());
			g.ColonyFounded[r] = true;
			g.DiasporaStreak[r] = Game.DiasporaStreakTarget - 1;
			int humanBefore = human.MilestoneScore;
			Sim.ClearTasks();

			uint target = g.GameTurn + 3u;
			while (g.GameTurn < target) { Sim.ClearTasks(); g.EndTurn(); }

			var fired = typeof(Game).GetField("_diasporaFired",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.True((bool)fired!.GetValue(g)!, "a rival reached the target and no ending fired");
			Assert.Equal(humanBefore, human.MilestoneScore);
		}

		// The human's own Diaspora still works — the symmetry must not cost the human its win.
		[Fact]
		public void TheHumanStillWinsItsOwnDiaspora()
		{
			(Game g, Player human, Player rival) = AWorld();
			byte h = g.PlayerNumber(human);
			human.AddAdvance(new SpaceFlight(), false);
			City hq = g.AddCity(human, 0, 40, 25)!;
			hq.Size = 4;
			hq.AddBuilding(new MissionControl());
			g.ColonyFounded[h] = true;
			g.DiasporaStreak[h] = Game.DiasporaStreakTarget - 1;
			int before = human.MilestoneScore;
			Sim.ClearTasks();

			uint target = g.GameTurn + 3u;
			while (g.GameTurn < target) { Sim.ClearTasks(); g.EndTurn(); }

			Assert.True(human.MilestoneScore > before,
				"the human held the colony for the full term and was awarded nothing");
		}
	}
}
