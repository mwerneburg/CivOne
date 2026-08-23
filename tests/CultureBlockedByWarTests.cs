// CivOne tests
//
// A culture leader who is told nothing.
//
// Cultural Ascendancy has four clauses. Three are visible on the score screen — populace,
// first rank on culture per head, and the 1850 date gate. The fourth is not: a war the
// claimant STARTED and never settled zeroes the streak every turn.
//
// The advisory that existed only spoke on the way down — `else if (CultureStreak > 0)`, "the
// streak is broken". A civilization blocked from ever STARTING one heard nothing at all.
//
// Observed in game 3de868a5: the human held every visible clause from turn 485 to the end at
// 587 — populous, foremost by 1.24-1.37x against a bar of 1.10, a century past the gate —
// against a required hold of 75. The streak never left zero. One unresolved war they had
// declared on Tokugawa, whose civilization was alive on eleven cities, did it. The player
// speed-bought a fortune in culture buildings, took the lead, and never learned why nothing
// happened.
//
// The rule itself is not on trial here. Being the aggressor SHOULD cost the peaceful
// victory, and peace clears it (Player.MakePeace calls Game.ForgetWarStart). What was wrong
// is that the game never said so.

using System.Collections.Generic;
using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class CultureBlockedByWarTests
	{
		// A human who clears every VISIBLE clause: equal populations so culture per head is
		// decided by culture alone, ten times the culture of any rival, and a date past the
		// gate. Whether the streak runs is then down to the war clause and nothing else.
		private static (Game game, Player us, Player[] rivals) AWorldWhereWeAreAdmired()
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

			// PHILOSOPHY is a hard prerequisite for this victory, not decoration:
			// Game.cs:2150 zeroes the streak and `continue`s without it, the same shape as
			// the economic path requiring Banking. Costly to discover — the fixture without
			// it fails every assertion below while every visible clause reads true.
			us.AddAdvance(new CivOne.Advances.Philosophy(), false);

			g.AddCity(us, 0, 40, 25)!.Size = 6;
			int id = 1;
			foreach (Player r in rivals) g.AddCity(r, id, 34 + id++ * 3, 30)!.Size = 6;

			us.SetCulture(6000);
			foreach (Player r in rivals) r.SetCulture(600);

			g.GameTurn = (ushort)(400 + (Game.CultureGateYear - 1850) + 5);
			Sim.ClearTasks();
			return (g, us, rivals);
		}

		// A full round. Advisor text is NOT readable from the queue — AdvisorMessage renders
		// straight to Picture[] and keeps no strings, which AutosaveReportingTests:39 already
		// documents — so behaviour is read off the latch and the queue depth, and the wording
		// is pinned at the source in TheAdvisoryNamesTheWarAndTheRemedy below.
		private static void ATurn(Game g)
		{
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) g.EndTurn();
			Assert.True(g.GameTurn >= target, $"the fixture could not advance a turn (stuck at {g.GameTurn})");
		}

		private static uint Streak(Game g, Player us) =>
			g.Progress(g.PlayerNumber(us)).CultureStreak;

		private static bool Warned(Game g) =>
			(bool)typeof(Game).GetField("_cultBlockedNotified",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.GetValue(g)!;

		private static int QueuedMessages() => Sim.PendingTaskTypes().Count(t => t == "Message");

		// The situation, stated directly: admired on every visible measure, and the clock does
		// not move. This is the rule working as designed and is not what changed.
		[Fact]
		public void TheStreakStillCannotStartWhileWeAreTheAggressor()
		{
			(Game g, Player us, Player[] rivals) = AWorldWhereWeAreAdmired();
			us.DeclareWar(rivals[0]);

			ATurn(g);

			Assert.Equal(0u, Streak(g, us));
		}

		// ...and the fixture is honest: without the war the same world DOES run the clock. A
		// fixture that could never earn a streak would make every assertion here vacuous.
		[Fact]
		public void TheSameWorldAtPeaceEarnsTheStreak()
		{
			(Game g, Player us, Player[] _) = AWorldWhereWeAreAdmired();

			ATurn(g);

			Assert.True(Streak(g, us) > 0, "the fixture cannot earn a streak even at peace");
		}

		// The fifth clause, and the other one a player cannot see. Found while building this
		// fixture: without Philosophy the streak is zeroed and `continue`d before any visible
		// measure is computed, so a civ can lead culture per head tenfold, decades past the
		// gate, at peace, and every readout on the score screen looks correct.
		[Fact]
		public void WithoutPhilosophyThereIsNoAscendancyAtAll()
		{
			(Game g, Player us, Player[] _) = AWorldWhereWeAreAdmired();
			var ids = (System.Collections.Generic.List<byte>)typeof(Player)
				.GetField("_advances", System.Reflection.BindingFlags.NonPublic
				                     | System.Reflection.BindingFlags.Instance)!.GetValue(us)!;
			ids.Remove(new CivOne.Advances.Philosophy().Id);
			Assert.False(us.HasAdvance<CivOne.Advances.Philosophy>(), "could not remove it");

			ATurn(g);

			Assert.Equal(0u, Streak(g, us));
		}

		// The fix. Silence is what made this a hundred-turn mistake instead of a decision.
		[Fact]
		public void TheHumanIsToldThatTheWarIsWhatStopsThem()
		{
			(Game g, Player us, Player[] rivals) = AWorldWhereWeAreAdmired();
			us.DeclareWar(rivals[0]);
			Sim.ClearTasks();

			ATurn(g);

			Assert.True(Warned(g), "nothing told the player the war is what stops them");
			Assert.True(QueuedMessages() > 0, "the advisory never reached the queue");
		}

		// Not said to a civ that is simply behind on culture — this is the "you would be
		// winning" message, and it is worthless if it fires for everyone at war.
		[Fact]
		public void NothingIsSaidWhenCultureItselfIsTheProblem()
		{
			(Game g, Player us, Player[] rivals) = AWorldWhereWeAreAdmired();
			us.SetCulture(1);                      // not admired at all
			us.DeclareWar(rivals[0]);

			ATurn(g);

			Assert.False(Warned(g), "told a civ with no culture that a war is what holds it back");
		}

		// ...nor to one that is admired and at peace, which is simply winning.
		[Fact]
		public void NothingIsSaidWhenNoWarOfOursIsRunning()
		{
			(Game g, Player us, Player[] _) = AWorldWhereWeAreAdmired();

			ATurn(g);

			Assert.False(Warned(g));
		}

		// Once. It is a standing condition checked every turn, and an advisor that opens every
		// turn for a hundred turns is not a warning, it is a reason to stop reading them.
		[Fact]
		public void ItIsSaidOnceAndNotEveryTurn()
		{
			(Game g, Player us, Player[] rivals) = AWorldWhereWeAreAdmired();
			us.DeclareWar(rivals[0]);
			Sim.ClearTasks();
			ATurn(g);
			int first = QueuedMessages();

			Sim.ClearTasks();
			ATurn(g);

			Assert.True(first > 0, "never said at all");
			Assert.Equal(0, QueuedMessages());
		}

		// ...but it re-arms. Make peace, start another war, and the player is told again —
		// otherwise one message in a 600-turn game covers every war they will ever declare.
		[Fact]
		public void ItIsSaidAgainAfterPeaceAndAFreshWar()
		{
			(Game g, Player us, Player[] rivals) = AWorldWhereWeAreAdmired();
			us.DeclareWar(rivals[0]);
			ATurn(g);
			Assert.True(Warned(g), "not said the first time");

			us.MakePeace(rivals[0]);
			ATurn(g);
			Assert.False(Warned(g), "the latch did not re-arm when the war ended");

			us.DeclareWar(rivals[1]);
			Sim.ClearTasks();
			ATurn(g);

			Assert.True(Warned(g), "a second war of our making passed in silence");
			Assert.True(QueuedMessages() > 0, "re-armed but said nothing");
		}

		// The wording, pinned where it lives, because the queue cannot be read for it. Both
		// halves matter: naming the civilization (a player with four rivals must know WHICH
		// peace to buy) and naming the remedy.
		[Fact]
		public void TheAdvisoryNamesTheWarAndTheRemedy()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(Sim.RepoRoot(), "src", "Game.cs"));
			int at = src.IndexOf("_cultBlockedNotified = true;");
			Assert.True(at > 0, "the advisory has moved");
			string block = src.Substring(at, 600);

			Assert.Contains("cultVictim!.TribeNamePlural", block);
			Assert.Contains("There is no ascendancy without peace.", block);
		}

	}
}
