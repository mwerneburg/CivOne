// CivOne tests
//
// AssessCharacter — one civilization's conduct on a single scale, positive for enlightened
// and negative for harsh.
//
// It was a local function inside SelectVisitorArchetype, which asks it of every civ and
// averages the answers to judge the SPECIES. Starlab asks it of ONE civ, its builder,
// because a great work reflects the state that raised it: a confident republic lands a man
// on the moon and digs a canal, and the same country in its decline cannot re-roof its own
// palace.
//
// Two questions, one rubric. These tests exist so the rubric cannot quietly become two.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class CharacterAssessmentTests
	{
		private static (Game game, Player a, Player b) TwoCivs()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player a = ps[0], b = ps[1];
			int x0 = 25;
			foreach (Player p in new[] { a, b })
			{
				Assert.NotNull(g.AddCity(p, (byte)(x0 - 25), x0, 25));
				x0 += 8;
			}
			Sim.ClearTasks();
			return (g, a, b);
		}

		// The headline claim, and the one Starlab's quality rests on: a well-governed civ
		// outscores a harsh one on the same board.
		[Fact]
		public void ADemocracyWithTemplesOutscoresADespotism()
		{
			(Game g, Player good, Player bad) = TwoCivs();

			good.Government = new Democracy();
			foreach (City c in good.Cities) c.AddBuilding(new Temple());

			bad.Government = new Despotism();

			Assert.True(g.AssessCharacter(good) > g.AssessCharacter(bad),
				$"democracy scored {g.AssessCharacter(good)}, despotism {g.AssessCharacter(bad)}");
		}

		// Each clause has to actually move the number, or the rubric is decoration. Government
		// is the biggest single term, so it gets its own check against a fixed baseline: same
		// civ, same board, one thing changed.
		[Fact]
		public void GovernmentMovesTheScoreOnItsOwn()
		{
			(Game g, Player p, _) = TwoCivs();

			p.Government = new Despotism();
			int harsh = g.AssessCharacter(p);

			p.Government = new Democracy();
			int free = g.AssessCharacter(p);

			Assert.Equal(5, free - harsh);   // +3 against -2
		}

		// A war of any kind costs, and the penalty is capped at three so a general war does
		// not swamp everything else on the sheet.
		[Fact]
		public void WarsCostButAreCapped()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player[] ps = g.Players.Where(x => x is not null && g.PlayerNumber(x) != 0).ToArray();
			Player p = ps[0];
			p.Government = new Democracy();
			Sim.ClearTasks();

			int peace = g.AssessCharacter(p);

			Player[] foes = ps.Where(x => x != p).Take(5).ToArray();
			Assert.True(foes.Length >= 4, "fixture: not enough rivals to exceed the cap");
			foreach (Player foe in foes) p.DeclareWar(foe);
			Sim.ClearTasks();

			Assert.Equal(3, peace - g.AssessCharacter(p));   // five wars, three points
		}

		// ─── the expedition reads a narrower sheet ───────────────────────────
		//
		// There were two copies of this rubric and they had drifted: the visitor draw weighed
		// government, wars, temples, culture and pollution; the South Pole expedition's curse
		// roll weighed government, wars and pollution alone. Unified Sept 2026 with the
		// difference made explicit (CharacterClauses.Expedition) rather than left as an
		// accident of copy-and-paste.
		//
		// These two tests are the proof that unifying did not MOVE anything. That is the whole
		// risk of the change: the expedition's odds are balance, and a rubric that silently
		// grew two clauses would have made a well-templed civ measurably safer from the Thing.
		[Fact]
		public void TemplesDoNotMoveTheExpeditionScore()
		{
			(Game g, Player p, _) = TwoCivs();
			p.Government = new Democracy();

			int bare = g.AssessCharacter(p, CharacterClauses.Expedition);
			foreach (City c in p.Cities) c.AddBuilding(new Temple());
			int templed = g.AssessCharacter(p, CharacterClauses.Expedition);

			Assert.Equal(bare, templed);
			// ...while the WHOLE sheet does move, so the clause works and is merely excluded.
			Assert.True(g.AssessCharacter(p) > g.AssessCharacter(p, CharacterClauses.Expedition),
				"temples are worth nothing on either sheet — the clause is broken, not narrowed");
		}

		// The expedition's sheet is exactly the three clauses, arithmetically: govern, wars,
		// pollution. Pinned as a sum so a clause added to Whole cannot leak into it.
		[Fact]
		public void TheExpeditionSheetIsGovernmentWarsAndPollution()
		{
			(Game g, Player p, _) = TwoCivs();
			p.Government = new Republic();

			int govOnly   = g.AssessCharacter(p, CharacterClauses.Government);
			int warsOnly  = g.AssessCharacter(p, CharacterClauses.Wars);
			int pollOnly  = g.AssessCharacter(p, CharacterClauses.Pollution);

			Assert.Equal(govOnly + warsOnly + pollOnly,
				g.AssessCharacter(p, CharacterClauses.Expedition));
		}

		// Story factions are not humanity and get no vote. They are things that HAPPENED to
		// the species rather than choices it made, and a Registry sitting on half the world
		// must not be able to drag the average.
		//
		// A story faction has to be SEATED for this to mean anything. The obvious version of
		// this test asserted DoesNotContain against a fresh game — which has no story
		// factions in it at all, so it passed with the filter deleted from HumanityNations.
		[Fact]
		public void StoryFactionsAreNotCountedAsNations()
		{
			(Game g, Player ordinary, _) = TwoCivs();

			var skynetCiv = Common.Civilizations.First(c => c is CivOne.Civilizations.Skynet);
			var skynet = new Player(skynetCiv, "Skynet");
			g.AddPlayer(skynet);
			Assert.Contains(g.Players, p => p == skynet);   // it really is in the game

			// ...and it must HOLD something. HumanityNations also drops the destroyed, and a
			// freshly seated faction with no cities and no units is destroyed by definition
			// (Player.IsDestroyed) — so without this the filter under test never gets asked,
			// and the assertion below passed with the story-faction clause deleted.
			Assert.NotNull(g.AddCity(skynet, 9, 41, 25));
			Assert.False(skynet.IsDestroyed(), "fixture: the faction has to be alive to be excluded on purpose");

			Assert.DoesNotContain(g.HumanityNations(), p => p == skynet);
			// ...while an ordinary civ on the same board is counted, so the filter is
			// discriminating rather than simply returning nothing.
			Assert.Contains(g.HumanityNations(), p => p == ordinary);
			// The barbarians are not a nation either.
			Assert.DoesNotContain(g.HumanityNations(), p => g.PlayerNumber(p) == 0);
		}
	}
}
