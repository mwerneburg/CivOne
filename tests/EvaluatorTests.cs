// CivOne tests
//
// The Evaluators (built Sep 2026 with the user): an automated construct that takes orbit,
// lands nothing, and grades HUMANITY for Game.EvaluationTurns.
//
//   COHESION  — the share of human civ pairs at peace AND in contact (embassy, route, pact)
//   INTENT    — no nuclear strike and no city taken by force during the window
//   The Dome finished is an immediate pass.
//
//   ADMISSION — the drive is shared, the Olvir are sent later, the win banked with "play on"
//   RESET     — quarantine (nothing leaves), and the worst offender over ALL of history is
//               corrected: advances unlearned, a quarter of every city gone

using System.IO;
using System.Linq;
using System.Reflection;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class EvaluatorTests : System.IDisposable
	{
		public EvaluatorTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; Settings.Instance.Autopilot = false; }
		private readonly bool _auto;
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		private static void Call(Game g, string method) =>
			typeof(Game).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(g, null);

		// Four human civs with a city each, none in contact. Evaluators chosen, signal heard.
		private static (Game g, Player[] civs) FourCivs()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 15; x <= 65; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			g.GameTurn = Sim.TurnPastCultureGate();
			Player[] civs = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).Take(4).ToArray();
			foreach (Player p in g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).Skip(4).ToArray())
				foreach (IUnit u in g.GetUnits().Where(u => u.Owner == g.PlayerNumber(p)).ToArray()) g.DisbandUnit(u);
			int id = 0;
			foreach ((Player p, int i) in civs.Select((p, i) => (p, i)))
			{
				p.Explore(20 + i * 10, 25, range: 4);
				g.AddCity(p, id++, 20 + i * 10, 25)!.Size = 8;
			}
			g.SETISignalReceived = true;
			g.VisitorType = VisitorArchetype.Evaluators;
			Sim.ClearTasks();
			return (g, civs);
		}

		private static void ConnectAll(Player[] civs)
		{
			foreach (Player a in civs) foreach (Player b in civs) if (a != b) a.EstablishEmbassy(b);
		}

		// Open the window now and close it at once, so the verdict is due.
		private static void VerdictNow(Game g)
		{
			g.EvaluationStartTurn = g.GameTurn;
			g.EvaluationEndTurn = g.GameTurn;
			Call(g, "ProcessEvaluation");
		}

		// ── the draw ─────────────────────────────────────────────────────────

		[Fact]
		public void TheyComeMostlyForAWorldTheyCannotClassify()
		{
			Assert.Equal(0.30, Game.EvaluatorOdds(0));
			Assert.Equal(0.30, Game.EvaluatorOdds(-2));
			Assert.Equal(0.10, Game.EvaluatorOdds(5));
			Assert.Equal(0.10, Game.EvaluatorOdds(-6));
		}

		[Fact]
		public void TheDrawCanChooseThem()
		{
			int seen = 0;
			for (short seed = 1; seed <= 60 && seen == 0; seed++)
			{
				Sim.NewGame(width: 80, height: 50);
				Common.SetRandomSeed(seed);
				object v = typeof(Game).GetMethod("SelectVisitorArchetype", BindingFlags.NonPublic | BindingFlags.Instance)!
					.Invoke(Game.Instance, null)!;
				if ((VisitorArchetype)v == VisitorArchetype.Evaluators) seen++;
			}
			Assert.True(seen > 0, "sixty draws and never the Evaluators");
		}

		// ── the arrival ──────────────────────────────────────────────────────

		[Fact]
		public void TheyTakeOrbitAndOpenTheWindowAndLandNothing()
		{
			(Game g, Player[] _) = FourCivs();
			g.OlvirArrivalTurn = (uint)(g.GameTurn + 1);

			for (int i = 0; i < 400 && !g.VisitorsArrived; i++) { Sim.ClearTasks(); g.EndTurn(); }

			Assert.True(g.VisitorsArrived);
			Assert.Equal(g.VisitorsArrivedTurn + (uint)Game.EvaluationTurns, g.EvaluationEndTurn);
			Assert.DoesNotContain(g.Players, p => p is not null && p.Civilization is Civilizations.Olvir);
			Assert.Contains(g.Transmissions, t => t.Type == "EvaluatorArrival");
		}

		[Fact]
		public void TheirArtShips()
		{
			Assert.True(File.Exists(Path.Combine(Sim.RepoRoot(), "runtime", "sdl", "Resources", "defaults",
				"data", "event_art", "EvaluatorConstruct.png")));
		}

		// ── what they measure ────────────────────────────────────────────────

		[Fact]
		public void CohesionIsPeaceAndContactAcrossEveryPair()
		{
			(Game g, Player[] civs) = FourCivs();
			Assert.Equal(0.0, g.Cohesion());

			civs[0].EstablishEmbassy(civs[1]);
			civs[2].EstablishEmbassy(civs[3]);
			Assert.Equal(2.0 / 6, g.Cohesion(), 3);

			civs[0].DeclareWar(civs[1]);
			Assert.Equal(1.0 / 6, g.Cohesion(), 3);
		}

		// Liberating a city from the Machines or the barbarians is not a crime.
		[Fact]
		public void OnlyConquestsBetweenPeoplesAreRecorded()
		{
			(Game g, Player[] civs) = FourCivs();
			g.RecordConquest(civs[0], g.GetPlayer(0));
			Assert.Empty(g.Conquests);

			g.RecordConquest(civs[0], civs[1]);
			Assert.Single(g.Conquests);
		}

		// ── the verdict ──────────────────────────────────────────────────────

		[Fact]
		public void ACohesiveCleanWorldIsAdmitted()
		{
			(Game g, Player[] civs) = FourCivs();
			ConnectAll(civs);

			VerdictNow(g);

			Assert.Equal("Admission", g.BankedVictory);
			Assert.False(g.Quarantined);
			Assert.All(civs, p => Assert.True(g.Progress(g.PlayerNumber(p)).HasExoticFuel, "the drive was not shared"));
			Assert.True(g.LeagueRefugeesTurn > 0, "nobody is being sent");
		}

		[Fact]
		public void AWorldThatDoesNotTalkIsReset()
		{
			(Game g, Player[] _) = FourCivs();

			VerdictNow(g);

			Assert.True(g.Quarantined);
			Assert.Null(g.BankedVictory);
		}

		// Cohesion is not enough: a strike during the window fails the species.
		[Fact]
		public void ANuclearStrikeWhileTheyWatchFailsEvenAConnectedWorld()
		{
			(Game g, Player[] civs) = FourCivs();
			ConnectAll(civs);
			g.NuclearStrikes.Add(new[] { (int)g.GameTurn, (int)g.PlayerNumber(civs[2]) });

			VerdictNow(g);

			Assert.True(g.Quarantined);
		}

		[Fact]
		public void TheDomeIsAPass()
		{
			(Game g, Player[] civs) = FourCivs();
			City c = civs[0].Cities.Single();
			foreach (IWonder w in Game.DomeFiveComponents) c.AddWonder(w);
			Assert.True(g.DomeComplete, "fixture: the Dome is not complete");

			VerdictNow(g);

			Assert.Equal("Admission", g.BankedVictory);
		}

		// The League sends the Olvir some turns after admission.
		[Fact]
		public void AfterAdmissionTheOlvirAreSent()
		{
			(Game g, Player[] civs) = FourCivs();
			ConnectAll(civs);
			VerdictNow(g);

			g.GameTurn = (ushort)g.LeagueRefugeesTurn;
			Call(g, "ProcessEvaluation");

			Assert.Contains(g.Players, p => p is not null && p.Civilization is Civilizations.Olvir && !p.IsDestroyed());
		}

		// ── the reset ────────────────────────────────────────────────────────

		// History counts: a civ destroyed long before they came still marks its destroyer.
		[Fact]
		public void TheWorstOffenderIsJudgedOnAllOfHistory()
		{
			(Game g, Player[] civs) = FourCivs();
			g.AddReplayEvent(new ReplayData.CivilizationDestroyed(10, 99, civs[2].Civilization.Id));   // long ago
			g.RecordConquest(civs[1], civs[3]);                                                        // one city

			Assert.Same(civs[2], g.WorstOffender());
		}

		// A civ that destroyed itself (collapse) is nobody's crime.
		[Fact]
		public void CollapseIsNotAnOffence()
		{
			(Game g, Player[] civs) = FourCivs();
			g.AddReplayEvent(new ReplayData.CivilizationDestroyed(10, civs[2].Civilization.Id, civs[2].Civilization.Id));

			Assert.Equal(0, g.Offences(civs[2]));
		}

		[Fact]
		public void TheOffenderIsCorrected()
		{
			(Game g, Player[] civs) = FourCivs();
			Player offender = civs[2];
			foreach (IAdvance a in Common.Advances.Take(20)) offender.AddAdvance(a, false);
			g.AddReplayEvent(new ReplayData.CivilizationDestroyed(10, 99, offender.Civilization.Id));
			int advances = offender.Advances.Length;

			VerdictNow(g);

			Assert.True(offender.Advances.Length <= advances - Game.ResetAdvancesMin, "no advances were unlearned");
			Assert.Equal(6, offender.Cities.Single().Size);   // 8 less a quarter
			Assert.Equal(8, civs[0].Cities.Single().Size);   // only the offender
		}

		// Quarantine uses the Owners' interception: no colony ship arrives.
		[Fact]
		public void QuarantineInterceptsTheShips()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(), "src", "Game.cs"));
			Assert.Contains("VisitorType == VisitorArchetype.Owners || Quarantined", src);
		}

		[Fact]
		public void TheVerdictAndItsRecordSurviveASave()
		{
			(Game g, Player[] civs) = FourCivs();
			g.EvaluationStartTurn = 300; g.EvaluationEndTurn = 350;
			g.Quarantined = true; g.LeagueRefugeesTurn = 400;
			g.NuclearStrikes.Add(new[] { 310, 2 });
			g.RecordConquest(civs[0], civs[1]);
			string path = Path.Combine(Settings.Instance.SavesDirectory, "evaluators.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));
			Game h = Game.Instance;

			Assert.Equal((300u, 350u, true, 400u), (h.EvaluationStartTurn, h.EvaluationEndTurn, h.Quarantined, h.LeagueRefugeesTurn));
			Assert.Single(h.NuclearStrikes);
			Assert.Single(h.Conquests);
		}
	}
}
