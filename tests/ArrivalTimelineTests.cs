// CivOne tests
//
// The visitors come from the Oort Cloud now, and a peaceful winner can stay to meet them.
//
// Measured in play, 23 Sep 2026: a cultural win at ~turn 394, the Olvir due at 426, their
// fuel at 446 and the first spaceship near 600. The winner never met the aliens. Changed
// together, on the user's design:
//
//   - the signal is picked up already in the outer Oort Cloud; Tau Ceti is where it CAME from
//   - the approach warning to landfall is Game.ApproachTurns (30), not 80
//   - the Olvir share their fuel on landing, not twenty turns later
//   - a peaceful win with the visitors still coming is BANKED, and the player may stay to
//     meet them (+Game.WitnessBonus), then play on to Alpha Centauri or 2200

using System.IO;
using System.Linq;
using System.Reflection;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Persistence;
using CivOne.Screens;

namespace CivOne.Tests
{
	public class ArrivalTimelineTests
	{
		private static void PlayRounds(Game g, int rounds)
		{
			uint target = g.GameTurn + (uint)rounds;
			while (g.GameTurn < target)
			{
				Sim.ClearTasks();
				g.EndTurn();
			}
		}

		private static string[] Lines(object screen) =>
			(string[])typeof(TerminalScreen).GetField("_lines", BindingFlags.NonPublic | BindingFlags.Instance)!
				.GetValue(screen)!;

		private static string ApproachText(bool starlab) => string.Join("\n",
			(string[])typeof(TauCetiApproachWarning)
				.GetMethod("BuildLines", BindingFlags.NonPublic | BindingFlags.Static)!
				.Invoke(null, new object[] { "1635 AD", VisitorArchetype.Refugees, false, 0, "1665 AD", starlab })!);

		// ── the text ─────────────────────────────────────────────────────────

		[Fact]
		public void TheSignalComesFromTheOortCloudAndTracesBackToTauCeti()
		{
			Sim.NewGame(width: 80, height: 50);
			string text = string.Join("\n", Lines(new SETISignalTransmission("1630 AD", broadcasting: false)));

			Assert.Contains("Oort Cloud", text);
			Assert.Contains("origin: Tau Ceti", text);
		}

		// The signal can come before anyone holds Electronics; the briefing may not credit
		// broadcasts nobody has made.
		[Fact]
		public void OurBroadcastsAreMentionedOnlyOnceSomebodyIsBroadcasting()
		{
			Sim.NewGame(width: 80, height: 50);
			string quiet = string.Join("\n", Lines(new SETISignalTransmission("1630 AD", broadcasting: false)));
			string loud  = string.Join("\n", Lines(new SETISignalTransmission("1630 AD", broadcasting: true)));

			Assert.DoesNotContain("broadcasts", quiet);
			Assert.Contains("broadcasts", loud);
		}

		// The warning named no position and a hard-coded "80 YEARS", whatever the turn length.
		[Fact]
		public void TheApproachWarningGivesTheRealArrivalYear()
		{
			string text = ApproachText(starlab: false);

			Assert.Contains("ARRIVAL ESTIMATE: 1665 AD.", text);
			Assert.DoesNotContain("80 YEARS", text);
			Assert.Contains("OORT CLOUD", text);
		}

		// It told a player who already had a Starlab to raise one.
		[Fact]
		public void AStarlabAlreadyWatchingIsNotRecommended()
		{
			Assert.Contains("RAISE STARLAB", ApproachText(starlab: false));
			Assert.DoesNotContain("RAISE STARLAB", ApproachText(starlab: true));
			Assert.Contains("STARLAB — ACTIVE", ApproachText(starlab: true));
		}

		// ── the clock ────────────────────────────────────────────────────────

		[Fact]
		public void LandfallIsApproachTurnsAfterTheWarning()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			g.SETISignalReceived = true;
			g.VisitorType = VisitorArchetype.Refugees;
			uint warning = (uint)(g.GameTurn + 1);
			g.TauCetiEscalationTurn = warning;

			for (int i = 0; i < 5 && g.TauCetiEscalationTurn != 0; i++) PlayRounds(g, 1);

			Assert.Equal(0u, g.TauCetiEscalationTurn);
			Assert.Equal(warning + (uint)Game.ApproachTurns, g.OlvirArrivalTurn);
			Assert.Equal(30, Game.ApproachTurns);
		}

		// A world a few turns from landfall. `banked` stages a player who stayed on.
		private static (Game g, Player human, Player peaceful) OnTheEveOfLandfall(string? banked)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			Player peaceful = g.Players.First(p => p is not null && p != human && g.PlayerNumber(p) != 0);
			g.AddCity(human, 0, 40, 25);
			g.AddCity(peaceful, 1, 20, 25);
			g.SETISignalReceived = true;
			g.VisitorType = VisitorArchetype.Refugees;
			g.OlvirArrivalTurn = (uint)(g.GameTurn + 1);
			g.BankedVictory = banked;
			Sim.ClearTasks();
			return (g, human, peaceful);
		}

		private static void PlayThroughLandfall(Game g)
		{
			for (int i = 0; i < 5 && !g.VisitorsArrived; i++) PlayRounds(g, 1);
			Assert.True(g.VisitorsArrived, "the fixture never reached landfall");
		}

		[Fact]
		public void TheOlvirShareTheirFuelOnLanding()
		{
			(Game g, Player _, Player peaceful) = OnTheEveOfLandfall(banked: null);

			PlayThroughLandfall(g);
			PlayRounds(g, 1);

			Assert.True(g.Progress(g.PlayerNumber(peaceful)).HasExoticFuel,
				$"no fuel {g.GameTurn - g.VisitorsArrivedTurn} turn(s) after landfall");
		}

		// Salvage keeps its own clock; only the gift moved.
		[Fact]
		public void SalvageStillTakesTwentyTurns()
		{
			Assert.Equal(20, CivOne.Units.BaseUnit.ReverseEngineerTurns);
		}

		// The Dome is humanity's; a story faction handed a piece of it never builds it. The
		// Machines hold every advance, so they sorted first and were always given one.
		[Fact]
		public void NoDomeComponentGoesToTheMachines()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			var skynet = new Player(Common.Civilizations.First(c => c is CivOne.Civilizations.Skynet), "Skynet");
			g.AddPlayer(skynet);
			Assert.NotNull(g.AddCity(skynet, 9, 41, 25));
			foreach (IAdvance a in Common.Advances) skynet.AddAdvance(a, false);   // first in the old ordering
			Assert.False(skynet.IsDestroyed(), "fixture: the faction must be alive to be excluded");

			typeof(Game).GetMethod("AssignDomeComponents", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(g, null);

			Assert.NotEmpty(g.DomeAssignments);
			Assert.False(g.DomeAssignments.ContainsKey(g.PlayerNumber(skynet)), "the Machines were handed a Dome component");
		}

		// ── the encore ───────────────────────────────────────────────────────

		// The Cultural Ascendancy fixture from VictoryLatchTests, one turn from the win.
		private static Player OneTurnFromACulturalWin(Game g)
		{
			// The encore is never offered under Autopilot, which other tests leave on.
			Settings.Instance.Autopilot = false;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player human = g.HumanPlayer;
			Player[] rivals = g.Players
				.Where(p => p is not null && p != human && g.PlayerNumber(p) != 0).Take(3).ToArray();
			foreach (Player p in rivals.Append(human))
			{
				p.Government = new CivOne.Governments.Monarchy();
				p.Explore(40, 25, range: 20);
			}
			human.AddAdvance(new Philosophy(), false);
			g.AddCity(human, 0, 40, 25)!.Size = 6;
			int id = 1;
			foreach (Player r in rivals) g.AddCity(r, id, 34 + id++ * 3, 30)!.Size = 6;
			human.SetCulture(6000);
			foreach (Player r in rivals) { r.SetCulture(600); human.EstablishEmbassy(r); }
			rivals[0].AddAdvance(new Electronics(), false);
			g.Progress(g.PlayerNumber(human)).CultureStreak = Game.CultureHoldTurns - 1;
			Sim.ClearTasks();
			return human;
		}

		[Fact]
		public void AWinWithTheVisitorsComingIsBankedAndTheGamePlaysOn()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player human = OneTurnFromACulturalWin(g);
			g.SETISignalReceived = true;
			g.VisitorType = VisitorArchetype.Refugees;
			g.OlvirArrivalTurn = (uint)(g.GameTurn + 30);
			int famed = HallOfFame.Load().Count;

			PlayRounds(g, 3);

			Assert.Equal("Cultural Ascendancy", g.BankedVictory);
			Assert.Equal(famed, HallOfFame.Load().Count);   // written at the true end, not now
		}

		// The fixture is honest: with nobody coming, the same win ends the game as before.
		[Fact]
		public void AWinWithNobodyComingEndsTheGameAsBefore()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			OneTurnFromACulturalWin(g);
			int famed = HallOfFame.Load().Count;

			PlayRounds(g, 3);

			Assert.Null(g.BankedVictory);
			Assert.Equal(famed + 1, HallOfFame.Load().Count);
		}

		// Whatever ends the encore — defeat included — the Hall of Fame keeps the win.
		[Fact]
		public void TheBankedWinIsWhatTheHallOfFameKeeps()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			g.BankedVictory = "Cultural Ascendancy";

			int at = EndSequence.SaveAndGetIndex(g.HumanPlayer, "Defeated");

			Assert.True(at >= 0, "the entry was not written");
			Assert.Equal("Cultural Ascendancy", HallOfFame.Load()[at].Victory);
		}

		[Fact]
		public void TheWitnessIsPaidOnceAtLandfall()
		{
			(Game g, Player human, Player _) = OnTheEveOfLandfall(banked: "Cultural Ascendancy");
			int before = human.MilestoneScore;

			PlayThroughLandfall(g);
			PlayRounds(g, 3);

			Assert.Equal(before + Game.WitnessBonus, human.MilestoneScore);
		}

		[Fact]
		public void NoWinBankedNoWitnessBonus()
		{
			(Game g, Player human, Player _) = OnTheEveOfLandfall(banked: null);
			int before = human.MilestoneScore;

			PlayThroughLandfall(g);

			Assert.Equal(before, human.MilestoneScore);
		}

		[Fact]
		public void TheBankedWinSurvivesASave()
		{
			Sim.NewGame(width: 80, height: 50);
			Game.Instance.BankedVictory = "Economic Dominance";
			string path = Path.Combine(Settings.Instance.SavesDirectory, "banked.cos");
			Game.Instance.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));

			Assert.Equal("Economic Dominance", Game.Instance.BankedVictory);
		}
	}
}
