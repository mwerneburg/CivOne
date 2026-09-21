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

		// ─── what the intended station is worth ──────────────────────────────
		//
		// Trade, science, and a warning that turns a super-typhoon into an ordinary
		// hurricane. All three belong to the OWNER's whole empire, and all three belong to
		// the intended station only: a free port is not running a telescope.

		// Two equal cities on equal ground, one per civ, with roads so there is trade to
		// multiply. Grassland yields no trade bare, and 15% of nothing is nothing — the
		// mistake this fixture exists to avoid.
		private static (Game g, Player owner, City home, Player rival, City theirs) TwoTradingCities()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 50; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Road = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player owner = ps[0], rival = ps[1];
			// Explore, or the citizens work nothing and the whole fixture yields 2 raw trade
			// — 15% of which rounds to zero, and the bonus tests pass on a deleted bonus.
			// Monarchy for the same reason GreysTests uses it: Despotism docks rich tiles.
			foreach (Player p in new[] { owner, rival })
				p.Government = new CivOne.Governments.Monarchy();
			owner.Explore(25, 25, range: 20);
			rival.Explore(40, 25, range: 20);

			City home   = g.AddCity(owner, 0, 25, 25)!;
			City theirs = g.AddCity(rival, 1, 40, 25)!;
			home.Size = 12; theirs.Size = 12;
			Sim.ClearTasks();
			return (g, owner, home, rival, theirs);
		}

		private static void GrantStarlab(Game g, City city, StarlabQuality quality)
		{
			// Quality FIRST: AddWonder invalidates the city's cached trade, and setting the
			// quality afterwards would leave a stale number behind.
			g.StarlabQuality = quality;
			city.AddWonder(new Starlab());
		}

		[Fact]
		public void TheIntendedStationLiftsTradeAcrossTheEmpire()
		{
			(Game g, _, City home, _, City theirs) = TwoTradingCities();
			int before = home.RawTradeForAi, control = theirs.RawTradeForAi;
			// 15% of nothing is nothing: without this the test passes on a broken bonus.
			Assert.True(before >= 10, $"fixture has only {before} raw trade to multiply");

			GrantStarlab(g, home, StarlabQuality.Intended);

			Assert.Equal(before + (int)(before * CivOne.Wonders.Starlab.TradeBonus), home.RawTradeForAi);
			Assert.Equal(control, theirs.RawTradeForAi);   // a rival's economy is untouched
		}

		[Fact]
		public void TheIntendedStationLiftsScience()
		{
			(Game g, _, City home, _, City theirs) = TwoTradingCities();
			int before = home.Science, control = theirs.Science;
			Assert.True(before >= 4, $"fixture produces only {before} science to multiply");

			GrantStarlab(g, home, StarlabQuality.Intended);

			// MORE than the trade bonus alone would explain. Science is derived from trade,
			// so a +15% trade bonus lifts science by +15% on its own — and "science went up"
			// therefore passed with the science bonus deleted outright. A negative check
			// caught it. The floor below is what separates 1.15x from 1.15 x 1.25.
			Assert.True(home.Science > before * (1 + CivOne.Wonders.Starlab.TradeBonus),
				$"science went {before} -> {home.Science}, which the trade bonus alone covers");
			Assert.Equal(control, theirs.Science);
		}

		// The free port is the whole reason the quality is drawn at all. If it paid the same
		// dividends, the verdict would be flavour text.
		[Fact]
		public void AFreePortPaysNoDividend()
		{
			(Game g, _, City home, _, _) = TwoTradingCities();
			int trade = home.RawTradeForAi, science = home.Science;

			GrantStarlab(g, home, StarlabQuality.FreePort);

			Assert.Equal(trade, home.RawTradeForAi);
			Assert.Equal(science, home.Science);
		}

		// ─── what the free port costs ────────────────────────────────────────

		// An empire of two cities, and a rival to prove the skim is not simply global.
		// Spaced 8 apart so their work radii never overlap and the two baselines are
		// independent.
		private static (Game g, Player owner, City a, City b, City theirs) AnEmpireAndARival()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Road = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player owner = ps[0], rival = ps[1];
			foreach (Player p in new[] { owner, rival })
				p.Government = new CivOne.Governments.Monarchy();
			owner.Explore(25, 25, range: 12);
			owner.Explore(33, 25, range: 12);
			rival.Explore(50, 25, range: 12);

			City a      = g.AddCity(owner, 0, 25, 25)!;
			City b      = g.AddCity(owner, 1, 33, 25)!;
			City theirs = g.AddCity(rival, 2, 50, 25)!;
			foreach (City c in new[] { a, b, theirs }) c.Size = 24;
			Sim.ClearTasks();
			return (g, owner, a, b, theirs);
		}

		// The page promises CORRUPTION "in every city it holds", and every city is the point:
		// a skim that only bit the host city would be a tax on one town, not a verdict on an
		// empire.
		[Fact]
		public void AFreePortSkimsEveryCityTheOwnerHolds()
		{
			(Game g, _, City a, City b, City theirs) = AnEmpireAndARival();
			int baseA = a.Corruption, baseB = b.Corruption, baseRival = theirs.Corruption;
			int tradeA = a.RawTradeForAi, tradeB = b.RawTradeForAi;
			// INTEGER division: a fixture below 10 raw trade skims zero and the test passes
			// on deleted code. This is the trap GreysTests fell into with RawTrade / 5.
			Assert.True(tradeA >= 10 && tradeB >= 10,
				$"fixture trade is {tradeA}/{tradeB}; the skim would round to nothing");

			GrantStarlab(g, a, StarlabQuality.FreePort);
			// AddWonder invalidates only ITS city. Every other city keeps a stale cache until
			// something else clears it — the same shape as The Internet and the Human Genome
			// Project, both of which are empire-wide and invalidate one city. Cleared here so
			// this test is about the skim rather than about cache timing.
			b.InvalidateCache();
			theirs.InvalidateCache();

			int skim = CivOne.Wonders.Starlab.FreePortSkimDivisor;
			Assert.Equal(baseA + tradeA / skim, a.Corruption);
			Assert.Equal(baseB + tradeB / skim, b.Corruption);   // the city that built nothing
			Assert.Equal(baseRival, theirs.Corruption);          // and not the neighbours
		}

		// The other half of the verdict: the good station is not merely better, it is clean.
		[Fact]
		public void TheIntendedStationSkimsNothing()
		{
			(Game g, _, City a, City b, _) = AnEmpireAndARival();
			int baseB = b.Corruption;

			int skim = b.RawTradeForAi / CivOne.Wonders.Starlab.FreePortSkimDivisor;
			// The skim has to be big enough to SEE. At size 12 this fixture skimmed 1 — which
			// is exactly what the intended station's own +15% trade adds in ordinary graft, so
			// the two outcomes came to the same number and the test could not tell them apart.
			// A bigger city separates them: 4 either way here, against 6 for the free port.
			Assert.True(skim >= 2, $"the free port would skim only {skim} here — too small to see");

			GrantStarlab(g, a, StarlabQuality.Intended);
			b.InvalidateCache();

			Assert.Equal(baseB, b.Corruption);
		}

		// ─── the storm warning ───────────────────────────────────────────────
		//
		// HurricaneCheck's thresholds are linear in warming, and a large enough value
		// saturates all of them: strikePct reaches 100 so a storm always lands, and catThresh
		// falls below zero so it is always Catastrophic. That turns a probabilistic rule into
		// a deterministic one for the length of a test, with no seeding and no statistics —
		// the severity demotion is what is under test, not the dice in front of it.
		private const int StormCertain = 99;

		private static (Game g, Player owner, City port) ACoastalCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 0;  x <= 19; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player owner = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0);
			// x=20 puts open sea in the next column; y=25 is the tropical band on a 50-high map.
			City port = g.AddCity(owner, 0, 20, 25)!;
			port.Size = 12;
			Sim.ClearTasks();
			return (g, owner, port);
		}

		[Fact]
		public void WithoutTheStationASuperTyphoonLandsInFull()
		{
			(Game g, _, City port) = ACoastalCity();

			Assert.True(port.HurricaneCheck(StormCertain), "fixture: no storm landed at all");

			// Catastrophic takes half the city; Major takes a third.
			Assert.Equal(6, port.Size);
		}

		[Fact]
		public void TheIntendedStationDemotesASuperTyphoon()
		{
			(Game g, _, City port) = ACoastalCity();
			GrantStarlab(g, port, StarlabQuality.Intended);

			Assert.True(port.HurricaneCheck(StormCertain), "fixture: no storm landed at all");

			Assert.Equal(8, port.Size);   // a third, not a half
			// The advisor also says "Starlab warned the coast.", but that line is not
			// asserted here: AdvisorMessage renders its text to bitmaps in the constructor
			// and keeps no string field, so Sim.PendingMessageLines cannot see it. The size
			// is the behaviour that matters and it is checked above.
		}

		// ...and the free port does not forecast the weather.
		[Fact]
		public void AFreePortDoesNotWarnTheCoast()
		{
			(Game g, _, City port) = ACoastalCity();
			GrantStarlab(g, port, StarlabQuality.FreePort);

			Assert.True(port.HurricaneCheck(StormCertain), "fixture: no storm landed at all");

			Assert.Equal(6, port.Size);
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
