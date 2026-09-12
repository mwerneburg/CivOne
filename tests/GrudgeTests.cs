// CivOne tests
//
// Goodwill got a negative half, and Gandhi got his bug back.
//
// Player._attitudeBonus was a goodwill TIMER with no opposite sign, so a civ could be paid
// into friendship and never earn a grievance — the nuclear condemnation could do no more than
// zero somebody's goodwill, which left the detonator a stranger rather than an enemy. The
// scale is now signed: positive is turns of goodwill, negative is turns of grudge, and decay
// walks toward zero from either side.
//
// On top of that sits the homage. Civ 1 stored aggression in an unsigned byte; Gandhi sat at
// the bottom, Democracy subtracted two, and the counter wrapped to 255. Here the counter that
// overflows is the one the player drives: fill his goodwill to the cap and keep giving, and
// it inverts to a grievance that never decays. Gandhi alone, by name — the other seven
// Friendly leaders reach the same cap and simply stay pleased.

using System.IO;
using System.Linq;
using CivOne;
using CivOne.Civilizations;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class GrudgeTests
	{
		private static (Game game, Player me, Player them) AWorld()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			Player me = g.HumanPlayer;
			byte num = g.PlayerNumber(me);
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != num
			                                                 && g.PlayerNumber(p) != 0);
			Sim.ClearTasks();
			return (g, me, them);
		}

		// ── 1. the scale is signed ───────────────────────────────────────────
		[Fact]
		public void AGrudgeIsTheNegativeHalfOfTheSameScale()
		{
			var (g, me, them) = AWorld();

			them.AddGrudge(me, 30);

			Assert.True(them.HasGrudge(me));
			Assert.False(them.HasAttitudeBonus(me));
			Assert.Equal(-30, them.AttitudeToward(me));
		}

		// It decays toward zero like goodwill does, one turn at a time, and must never
		// overshoot into friendship.
		//
		// The intermediate assertions are the point. The first version of this test only
		// checked the end state, and the OLD tick — `if (--v <= 0) Remove(k)` — passes that
		// trivially: it drives -3 to -4, sees a value at or below zero, and deletes the
		// entry. A grudge that evaporated after one turn and a grudge that ran its three
		// look identical from the far side.
		[Fact]
		public void AGrudgeDecaysOneTurnAtATimeAndStopsAtZero()
		{
			var (g, me, them) = AWorld();
			them.AddGrudge(me, 3);

			them.NewTurn();
			Assert.Equal(-2, them.AttitudeToward(me));
			them.NewTurn();
			Assert.Equal(-1, them.AttitudeToward(me));
			them.NewTurn();

			Assert.False(them.HasGrudge(me));
			Assert.False(them.HasAttitudeBonus(me));
			Assert.Equal(0, them.AttitudeToward(me));
		}

		// An apology counts: gifts work a grievance off before they buy anything.
		[Fact]
		public void GenerosityWorksAGrudgeOff()
		{
			var (g, me, them) = AWorld();
			them.AddGrudge(me, 50);

			them.AddAttitudeBonus(me, 30);

			Assert.Equal(-20, them.AttitudeToward(me));
		}

		// ── 2. it survives a save ────────────────────────────────────────────
		// The save wrote `.Where(e => e.Value > 0)`, which would have kept the goodwill and
		// silently dropped every grievance — a load forgave everything and nothing said so.
		[Fact]
		public void AGrudgeSurvivesASaveAndLoad()
		{
			var (g, me, them) = AWorld();
			byte theirNum = g.PlayerNumber(them);
			them.AddGrudge(me, 42);
			string path = Path.Combine(Settings.Instance.SavesDirectory, "grudge.cos");
			Game.Instance.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			Player reloaded = Game.Instance.GetPlayer(theirNum);
			Assert.Equal(-42, reloaded.AttitudeToward(Game.Instance.HumanPlayer));
		}

		[Fact]
		public void ImplacabilitySurvivesASaveAndLoad()
		{
			var (g, me, them) = AWorld();
			byte theirNum = g.PlayerNumber(them);
			them.SetImplacable(me);
			string path = Path.Combine(Settings.Instance.SavesDirectory, "implacable.cos");
			Game.Instance.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "LoadCos should succeed");

			Assert.True(Game.Instance.GetPlayer(theirNum)
				.IsImplacableToward(Game.Instance.HumanPlayer));
		}

		// ── 5. the overflow ──────────────────────────────────────────────────
		// Fill the scale and keep giving. Three lavish gifts, because a large city is worth
		// 100 and the cap is 200: the third is the one that arrives with nowhere to go.
		// Gandhi leads the Indians. Rolling games until he turns up took forty seconds and
		// often failed, so the player is built directly from the civilization and added to
		// the game the way the story factions are (Game.ExecuteSkynetUprising does the same).
		private static (Game, Player me, Player gandhi) AWorldWithGandhi()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			Player? gandhi = g.Players.FirstOrDefault(p => p is not null && p != g.HumanPlayer
				&& p.Civilization is CivOne.Civilizations.Indian);
			if (gandhi is null)
			{
				ICivilization india = Common.Civilizations.First(c => c is CivOne.Civilizations.Indian);
				gandhi = new Player(india);
				g.AddPlayer(gandhi);
			}
			Assert.True(gandhi.Civilization?.Leader is CivOne.Leaders.Gandhi,
				"fixture: the Indians are supposed to be led by Gandhi");
			Sim.ClearTasks();
			return (g, g.HumanPlayer, gandhi);
		}

		[Fact]
		public void LavishingGandhiOverturnsHim()
		{
			var (g, me, gandhi) = AWorldWithGandhi();

			gandhi.AddAttitudeBonus(me, 100);
			gandhi.AddAttitudeBonus(me, 100);
			Assert.False(gandhi.IsImplacableToward(me), "two gifts only fill the scale");

			gandhi.AddAttitudeBonus(me, 100);

			Assert.True(gandhi.IsImplacableToward(me), "the third gift should have overflowed");
			Assert.True(gandhi.HasGrudge(me));
			Assert.False(gandhi.HasAttitudeBonus(me));
		}

		// ...and it is permanent. Split in two: decay-immunity needs turns driven, which is
		// cheap on an ordinary player and unsafe on the one the fixture above adds by hand
		// (the AI reaches PlayerDestroyed through it and indexes a table it is not in).
		[Fact]
		public void AnImplacableGrudgeDoesNotDecay()
		{
			var (g, me, them) = AWorld();
			them.SetImplacable(me);

			for (int i = 0; i < 5; i++) them.NewTurn();

			Assert.True(them.IsImplacableToward(me));
			Assert.Equal(-Player.AttitudeCap, them.AttitudeToward(me));
		}

		// ...and it cannot be bought back, however lavish the apology.
		[Fact]
		public void AnImplacableCivTakesNoMoreGifts()
		{
			var (g, me, gandhi) = AWorldWithGandhi();
			for (int i = 0; i < 3; i++) gandhi.AddAttitudeBonus(me, 100);
			Assert.True(gandhi.IsImplacableToward(me), "fixture: he should have flipped");

			gandhi.AddAttitudeBonus(me, 100);

			Assert.False(gandhi.HasAttitudeBonus(me));
			Assert.Equal(-Player.AttitudeCap, gandhi.AttitudeToward(me));
		}

		// Everybody else who reaches the cap simply stays pleased. This is the half that
		// fails if the name check is loosened to "any Friendly leader".
		[Fact]
		public void EveryOtherLeaderJustStaysPleased()
		{
			var (g, me, them) = AWorld();
			Assert.False(them.Civilization?.Leader is CivOne.Leaders.Gandhi,
				"fixture: this test needs somebody who is not Gandhi");

			for (int i = 0; i < 5; i++) them.AddAttitudeBonus(me, 100);

			Assert.False(them.IsImplacableToward(me));
			Assert.True(them.HasAttitudeBonus(me));
			Assert.Equal(Player.AttitudeCap, them.AttitudeToward(me));
		}

		// ── 4. earning one ───────────────────────────────────────────────────
		[Fact]
		public void ANuclearVictimHoldsTheFullGrievance()
		{
			var (g, me, them) = AWorld();
			Player witness = g.Players.First(p => p is not null && p != me && p != them
			                                                   && g.PlayerNumber(p) != 0);

			g.CondemnNuclearStrike(me, them);

			Assert.Equal(-Player.AttitudeCap, them.AttitudeToward(me));
			Assert.Equal(-Game.GrudgeNuclearWitness, witness.AttitudeToward(me));
		}
	}
}
