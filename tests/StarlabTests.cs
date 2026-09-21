// CivOne tests
//
// Starlab, the orbital that takes the Interstellar Probe's slot at SPACE FLIGHT.
//
// Covers the wonder itself, and the draw that decides which Starlab got built: the
// station its founders described, or a free port. That verdict comes from the builder's
// character (Game.AssessCharacter) through the same curve that decides what the visitors
// turn out to be, not from a curse roll.
//
// The EFFECTS of each outcome — trade, science, storm demotion, the free port's
// corruption — arrive in later steps and get their own tests.

using System.IO;
using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class StarlabTests
	{
		private static Starlab Book()
		{
			Sim.EnsureRuntime();
			// Through the registry, not `new Starlab()`: Common.Wonders is built by
			// reflection, so a wonder can compile perfectly and still never appear in the
			// game. That is the failure this resolves.
			return Reflect.GetWonders().OfType<Starlab>().Single();
		}

		private static string Pages(Starlab w) =>
			string.Join(" ", w.GetPageText(1).Concat(w.GetPageText(2)));

		[Fact]
		public void StarlabIsInTheRegistry()
		{
			Starlab w = Book();

			Assert.Equal("Starlab", w.Name);
			Assert.Equal((byte)Wonder.Starlab, w.Id);
		}

		// Its id must not collide with an existing wonder's, because ProductionId is derived
		// from it and a collision would make two wonders the same build order.
		[Fact]
		public void NoTwoWondersShareAnId()
		{
			Sim.EnsureRuntime();
			var ids = Reflect.GetWonders().Select(w => w.Id).ToArray();

			Assert.Equal(ids.Length, ids.Distinct().Count());
		}

		[Fact]
		public void StarlabIsBuiltAtSpaceFlight()
		{
			Assert.IsType<SpaceFlight>(Book().RequiredTech);
		}

		// The actionable fact, and the only one: Starlab's quality is not luck, it is a
		// verdict on the builder (Game.AssessCharacter). A player who does not know that
		// cannot steer it, and steering it is the whole mechanic — so the page has to say
		// so in words, and keep saying so through any rewrite of the prose around them.
		//
		// Pinned on the three levers the rubric actually reads and a player can actually
		// pull. Government and war are on the page as "free, peaceful" against "despotism
		// at war"; pollution as the air they breathe.
		[Fact]
		public void ThePageSaysTheBuilderDecidesWhatGetsBuilt()
		{
			string text = Pages(Book());

			Assert.Contains("depends on who builds", text);
			Assert.Contains("despotism at war", text);
			Assert.Contains("clean air", text);
			// ...and names the bad end, so the player knows what they are steering away from.
			Assert.Contains("CORRUPTION", text);
		}

		// Rule 4 of docs/cursed_wonders.md, applied by analogy: the warning has to be on
		// PAGE ONE, because page 2 is unreachable with "Civipedia Text" off (Civilopedia.cs
		// remaps it back to page-1 text). Starlab is not a cursed wonder — there is no
		// one-in-four roll — but a player can still end up with the seedy station, and the
		// foreshadow is worthless if they cannot turn to it.
		[Fact]
		public void TheWarningIsReadableOnPageOne()
		{
			Assert.Contains("keeps their habits", string.Join(" ", Book().GetPageText(1)));
		}

		// A missing PNG degrades silently to the sprite-sheet icon, so demand the file.
		// Same guard as MissionControlTests.TheArtIsShipped and HarbourTests.
		[Fact]
		public void TheArtIsShipped()
		{
			string path = Path.Combine(Sim.RepoRoot(), "runtime", "sdl", "Resources",
				"defaults", "data", "improvement_art", "starlab.png");

			Assert.True(File.Exists(path), $"starlab art is missing: {path}");
		}

		// ─── the quality draw ────────────────────────────────────────────────
		//
		// Starlab's quality is not a curse roll. It is a verdict on the builder, drawn once
		// at completion through the SAME curve that decides what the visitors turn out to
		// be (Game.CharacterOdds) — a well-run civilization probably gets the station its
		// founders described, and a retrograde one probably gets a free port.

		// Two civs on one board: one a free, peaceful, well-templed republic, the other a
		// despotism at war with everyone and no temple in sight. Deliberately built from the
		// three clauses whose arithmetic CharacterAssessmentTests already pins, rather than
		// by forcing a score in, so this exercises the real rubric.
		private static (Game game, Player good, Player bad) AGoodCivAndABadOne()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player good = ps[0], bad = ps[1];

			int x0 = 25;
			foreach (Player p in new[] { good, bad })
			{
				City c = g.AddCity(p, (byte)(x0 - 25), x0, 25)!;
				// AddCity leaves a city at size 0, which produces nothing — NewTurn never
				// reaches its completion branch, so a wonder paid for in full silently stays
				// unbuilt. Size 4 rather than 8: an 8 riots under the harsher governments
				// here, and disorder resets production and zeroes shields.
				c.Size = 4;
				x0 += 8;
			}

			// Driven to the rubric's actual CEILING (+8), not merely "better": government +3,
			// no wars, temples everywhere +2, culture above twice the world average +2, and
			// clean air +1. The clamp test below depends on reaching the top exactly, because
			// an unclamped 0.5 + 0.07c passes certainty at +8 and nowhere earlier.
			good.Government = new CivOne.Governments.Democracy();
			foreach (City c in good.Cities) c.AddBuilding(new Temple());
			good.SetCulture(10000);

			bad.Government = new CivOne.Governments.Despotism();
			bad.SetCulture(0);
			// Deliberately NOT at war with `good`: the wars clause counts a war however it
			// started, so declaring on the good civ would cost IT a point and put the ceiling
			// out of reach. Four rivals is past the cap of three either way.
			foreach (Player foe in ps.Where(x => x != bad && x != good).Take(4)) bad.DeclareWar(foe);

			Sim.ClearTasks();
			// The fixture has to actually separate them, or every rate test below is
			// measuring one civilization twice.
			Assert.True(g.AssessCharacter(good) > g.AssessCharacter(bad),
				$"fixture: good scored {g.AssessCharacter(good)}, bad {g.AssessCharacter(bad)}");
			return (g, good, bad);
		}

		private static int IntendedOutOf(Game g, Player builder, int trials)
		{
			int intended = 0;
			for (int i = 0; i < trials; i++)
				if (g.DrawStarlabQuality(builder) == StarlabQuality.Intended) intended++;
			return intended;
		}

		[Fact]
		public void AFreshGameHasNoStarlab()
		{
			Sim.NewGame(width: 80, height: 50);

			Assert.Equal(StarlabQuality.NotBuilt, Game.Instance.StarlabQuality);
		}

		// The curve itself, as arithmetic — no board, no randomness. Monotone in character,
		// and clamped at both ends.
		[Theory]
		[InlineData(-100, 0.20)]   // far below anything the rubric can produce
		[InlineData(-9,   0.20)]   // the rubric's actual floor: already clamped
		[InlineData(0,    0.50)]   // an unremarkable civilization is a coin toss
		[InlineData(8,    0.80)]   // the rubric's actual ceiling: already clamped
		[InlineData(100,  0.80)]
		public void TheCurveIsClampedAtBothEnds(int character, double expected)
		{
			Assert.Equal(expected, Game.CharacterOdds(character), 3);
		}

		[Fact]
		public void TheCurveRisesWithCharacter()
		{
			Assert.True(Game.CharacterOdds(-4) < Game.CharacterOdds(0));
			Assert.True(Game.CharacterOdds(0)  < Game.CharacterOdds(4));
		}

		// The claim that matters: the builder decides. Same board, same seed, same number of
		// draws — the only difference is who is asked.
		//
		// A RATE, not a single draw, because the rule is probabilistic. Seeded so this is
		// reproducible rather than occasionally red on a Tuesday.
		[Fact]
		public void AWellRunCivilizationUsuallyGetsTheStationItIntended()
		{
			(Game g, Player good, Player bad) = AGoodCivAndABadOne();

			Common.SetRandomSeed(4711);
			int goodRuns = IntendedOutOf(g, good, 2000);
			Common.SetRandomSeed(4711);
			int badRuns  = IntendedOutOf(g, bad,  2000);

			Assert.True(goodRuns > badRuns + 600,
				$"the builder barely matters: good {goodRuns}/2000 intended, bad {badRuns}/2000");
			// ...and each lands near its own end of the clamp rather than merely differing.
			Assert.InRange(goodRuns, 1450, 1750);   // ~80%
			Assert.InRange(badRuns,   350,  650);   // ~20%
		}

		// The clamp, stated as BEHAVIOUR rather than as arithmetic — and it has to be stated
		// at the ceiling, or it states nothing.
		//
		// An earlier version of this test asserted both ends against the fixture as it stood
		// and passed with the clamp deleted, because that fixture peaked around +6, where an
		// unclamped curve is 92% and still leaves free ports in 2000 draws. 0.5 + 0.07c only
		// reaches certainty at +8, which is exactly the rubric's maximum — so the test is
		// worth nothing unless the builder is standing on it. Hence the assertion on the
		// score itself: if the rubric is ever retuned so +8 is no longer the top, this fails
		// here and says so, rather than quietly going vacuous again.
		//
		// Only the ceiling is checkable this way. The floor needs pollution at 8 smokestacks,
		// which takes a real industrial base to fabricate; TheCurveIsClampedAtBothEnds covers
		// that end arithmetically.
		[Fact]
		public void TheBestCivilizationInTheGameCanStillGetAFreePort()
		{
			(Game g, Player good, _) = AGoodCivAndABadOne();
			Assert.Equal(8, g.AssessCharacter(good));   // standing on the ceiling

			Common.SetRandomSeed(4242);
			int intended = IntendedOutOf(g, good, 2000);

			Assert.True(intended < 2000,
				"a civilization at the top of the rubric was GUARANTEED its good station");
			// ...and it is still the likely outcome, so the clamp bounds it without flattening it.
			Assert.True(intended > 1000, $"only {intended}/2000 — the reward for virtue is gone");
		}

		// ─── the hook, and persistence ───────────────────────────────────────

		// Finish the wonder the way the production loop does (MissionControlTests' pattern).
		private static void Complete(City city, IWonder wonder)
		{
			city.SetProduction(wonder);
			// ProductionCost, not Price * 10: a wonder whose strategic resource the owner
			// lacks costs half again as much, and paying the sticker price silently leaves
			// it unbuilt — which is exactly how this test first failed.
			city.Shields = (short)city.ProductionCost(wonder);
			city.NewTurn();
			Sim.Settle();
			Assert.True(city.HasWonder(wonder.GetType()), $"fixture: {wonder.Name} was not built");
		}

		[Fact]
		public void BuildingStarlabDrawsAQuality()
		{
			(Game g, Player good, _) = AGoodCivAndABadOne();
			City home = good.Cities[0];
			Assert.Equal(StarlabQuality.NotBuilt, g.StarlabQuality);

			Complete(home, new Starlab());

			Assert.NotEqual(StarlabQuality.NotBuilt, g.StarlabQuality);
		}

		// ...and a game where nobody builds it keeps NotBuilt, so the assertion above is
		// about the hook rather than about something else setting the field.
		[Fact]
		public void BuildingSomethingElseDrawsNothing()
		{
			(Game g, Player good, _) = AGoodCivAndABadOne();

			Complete(good.Cities[0], new Pyramids());

			Assert.Equal(StarlabQuality.NotBuilt, g.StarlabQuality);
		}

		// The draw happens ONCE. A station that re-rolled itself on load would let a player
		// save-scum the verdict, which is exactly what the character rubric is there to stop.
		[Fact]
		public void TheQualitySurvivesASaveAndLoad()
		{
			(Game g, _, _) = AGoodCivAndABadOne();
			g.StarlabQuality = StarlabQuality.FreePort;

			string path = Path.Combine(Settings.Instance.SavesDirectory, "starlab.cos");
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(StarlabQuality.FreePort, Game.Instance.StarlabQuality);
		}
	}
}
