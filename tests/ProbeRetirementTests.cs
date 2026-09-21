// CivOne tests
//
// The Interstellar Probe is retired. Starlab takes its slot at Space Flight, and the
// observatories now resolve the visitors' archetype on their own — which was the probe's
// one unique job.
//
// Retired, NOT deleted: the wonder class, its Civilopedia entry and its five .cos fields
// all stay, because a game already in flight may have a probe en route and its reports
// must still arrive. These tests pin both halves of that — no new ones, and old ones
// still fly.

using System.Linq;
using System.Reflection;
using CivOne;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Screens;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class ProbeRetirementTests
	{
		private static (Game g, Player p) AWorldThatCouldHaveBuiltIt()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			foreach (IAdvance a in Common.Advances) p.AddAdvance(a, false);
			// The two things the old gate asked for. With both satisfied, an un-retired probe
			// WOULD be offered — which is what makes the assertion below mean anything.
			g.SETISignalReceived = true;
			Sim.ClearTasks();
			return (g, p);
		}

		[Fact]
		public void TheProbeIsNoLongerOffered()
		{
			(Game g, Player p) = AWorldThatCouldHaveBuiltIt();

			Assert.False(p.ProductionAvailable(new InterstellarProbe()),
				"the probe is still on the build list");
		}

		// The control: the same player, the same tech, the wonder that replaced it. Without
		// this, the assertion above passes on a ProductionAvailable that refuses everything.
		[Fact]
		public void StarlabTookItsPlace()
		{
			(Game g, Player p) = AWorldThatCouldHaveBuiltIt();

			Assert.True(p.ProductionAvailable(new Starlab()),
				"Starlab is not available either — the gate refuses everything");
		}

		// It stays in the reference book. A player loading a save with a probe en route can
		// still look up what it is, and the entry is history either way.
		[Fact]
		public void ItIsStillInTheCivilopedia()
		{
			Sim.EnsureRuntime();

			Assert.Contains(Reflect.GetWonders(), w => w is InterstellarProbe);
		}

		// The half that matters for saves in flight: the mission clocks are untouched, so a
		// probe already dispatched still reports. Retiring a wonder must not strand a game.
		[Fact]
		public void AProbeAlreadyEnRouteStillReports()
		{
			(Game g, Player p) = AWorldThatCouldHaveBuiltIt();
			g.VisitorType = VisitorArchetype.Refugees;
			g.ProbeDispatched = true;
			g.ProbeDispatchTurn = g.GameTurn + 1u;
			g.ProbeInterimPhase = 0;

			// Phase 1 lands 8 turns after dispatch.
			for (int i = 0; i < 12 && g.ProbeInterimPhase == 0; i++)
			{
				uint turn = g.GameTurn;
				for (int j = 0; j < 64 && g.GameTurn == turn; j++) g.EndTurn();
			}

			Assert.True(g.ProbeInterimPhase > 0,
				"a probe already en route stopped transmitting when the wonder was retired");
		}

		// ...and the screen must stop advertising it. A briefing that tells the player to
		// dispatch a probe they cannot build is the reference-book failure this project
		// writes tests for: they read it, act on it, and find nothing on the build list.
		[Fact]
		public void TheApproachBriefingNoLongerTellsYouToBuildOne()
		{
			Sim.EnsureRuntime();
			string text = Render(VisitorArchetype.Refugees, probeDispatched: false);

			Assert.DoesNotContain("DISPATCH PROBE", text);
			Assert.Contains("STARLAB", text);
		}

		// But a game with one already flying still gets its status line, for the same reason
		// the clocks stay: that player's probe is real and they are owed the report.
		[Fact]
		public void AGameWithAProbeEnRouteStillSeesItsStatus()
		{
			Sim.EnsureRuntime();
			string text = Render(VisitorArchetype.Refugees, probeDispatched: true);

			Assert.Contains("PROBE IS EN ROUTE", text);
		}

		private static string Render(VisitorArchetype archetype, bool probeDispatched) =>
			string.Join("\n", (string[])typeof(TauCetiApproachWarning)
				.GetMethod("BuildLines", BindingFlags.NonPublic | BindingFlags.Static)!
				.Invoke(null, new object[] { "1635 AD", archetype, probeDispatched, 2 })!);
	}
}
