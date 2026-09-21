// CivOne tests
//
// The synthetic alien in Starlab: woken by the world's fifth Xenolab, five koans four
// turns apart, then silence, then twenty turns in which the cities that built the labs
// carry the grief.
//
// It is the gentlest thing in the endgame — it seizes nothing and attacks nobody — which
// makes it the easiest to break without noticing. Nothing on the map changes when it goes
// wrong; the broadcasts simply stop, or never start, and no other test in the suite has an
// opinion about that.

using System.IO;
using System.Linq;
using System.Reflection;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Screens;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class KoanTests
	{
		// Phase B of EndTurn runs only when the player clock wraps, so the round has to come
		// all the way round. The assertion that the turn moved is the point.
		private static void AdvanceOneRound(Game g)
		{
			uint turn = g.GameTurn;
			for (int i = 0; i < 64 && g.GameTurn == turn; i++) g.EndTurn();
			Assert.True(g.GameTurn > turn, "the round never wrapped, so phase B never ran");
		}

		// A world with Starlab standing and `labs` Xenolabs in it. Cities are spaced 4 apart
		// so their work radii do not overlap and each one's citizens are its own.
		private static (Game g, Player p, City[] cities) AWorldWithXenolabs(int labs,
			bool starlab = true)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			p.Government = new CivOne.Governments.Monarchy();

			// Six, so a lab can be left out of one of them as a control.
			var cities = new City[6];
			for (int i = 0; i < cities.Length; i++)
			{
				int x = 24 + i * 5;
				p.Explore(x, 25, range: 6);
				cities[i] = g.AddCity(p, (byte)i, x, 25)!;
				cities[i].Size = 6;
				if (i < labs) cities[i].AddBuilding(new Xenolab());
			}
			if (starlab)
			{
				g.StarlabQuality = StarlabQuality.Intended;
				cities[cities.Length - 1].AddWonder(new Starlab());
			}
			Sim.ClearTasks();
			return (g, p, cities);
		}

		private static int Koans(Game g) =>
			g.Transmissions.Count(t => t.Type.StartsWith("Koan") && t.Type != "KoanSilence");

		// ── waking ───────────────────────────────────────────────────────────

		[Fact]
		public void FourXenolabsWakeNothing()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake - 1);

			AdvanceOneRound(g);

			Assert.False(g.AlienAwake);
			Assert.Equal(0, Koans(g));
		}

		[Fact]
		public void TheFifthXenolabWakesIt()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake);

			AdvanceOneRound(g);

			Assert.True(g.AlienAwake, "five laboratories and nobody came");
			Assert.Equal(1, Koans(g));   // and it speaks at once
		}

		// It lives in the station. No station, no tenant — and the plates show that ring
		// specifically, so firing without it would illustrate a thing that is not there.
		[Fact]
		public void WithoutStarlabThereIsNowhereForItToLive()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake, starlab: false);

			for (int i = 0; i < 3; i++) AdvanceOneRound(g);

			Assert.False(g.AlienAwake);
			Assert.Equal(0, Koans(g));
		}

		// ── the clock ────────────────────────────────────────────────────────

		[Fact]
		public void ItSpeaksFiveTimesAndNoMore()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake);

			// Well past the whole sequence: 5 koans, 4 turns apart, is 16 turns of talking.
			for (int i = 0; i < 40; i++) AdvanceOneRound(g);

			Assert.Equal(Game.KoansTotal, Koans(g));
			Assert.Equal(Game.KoansTotal, g.KoansSent);
		}

		// Four turns apart, not five in a rush. Measured as the span from the first koan to
		// the last rather than turn by turn, which is the property that actually matters and
		// does not care where in the round the tick lands.
		[Fact]
		public void TheyComeFourTurnsApart()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake);

			AdvanceOneRound(g);
			Assert.Equal(1, Koans(g));
			uint first = g.GameTurn;

			for (int i = 0; i < 40 && g.KoansSent < Game.KoansTotal; i++) AdvanceOneRound(g);
			uint last = g.GameTurn;

			// Four intervals between five koans, give or take the turn the round wraps on.
			int span = (int)(last - first);
			int expected = Game.KoanInterval * (Game.KoansTotal - 1);
			Assert.InRange(span, expected - 1, expected + 1);
		}

		[Fact]
		public void ItDiesWhenItRunsOutOfQuestions()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake);

			for (int i = 0; i < 40; i++) AdvanceOneRound(g);

			Assert.Contains(g.Transmissions, t => t.Type == "KoanSilence");
			Assert.True(g.MourningUntilTurn > g.GameTurn - Game.MourningTurns,
				"the mourning was never scheduled");
		}

		// ── the mourning ─────────────────────────────────────────────────────

		// The refinement that makes this grief rather than undirected anger: it lands only
		// where a Xenolab stands. The town that built the laboratory listened to the thing
		// die; the town down the road did not.
		[Fact]
		public void OnlyXenolabTownsMourn()
		{
			(Game g, _, City[] cities) = AWorldWithXenolabs(Game.XenolabsToWake);
			City withLab = cities[0], without = cities[Game.XenolabsToWake];
			Assert.True(withLab.HasBuilding<Xenolab>(), "fixture: no lab in the mourning city");
			Assert.False(without.HasBuilding<Xenolab>(), "fixture: the control has a lab");

			int labBefore = withLab.UnhappyCitizens, controlBefore = without.UnhappyCitizens;
			g.MourningUntilTurn = g.GameTurn + (uint)Game.MourningTurns;
			foreach (City c in cities) c.InvalidateCache();

			Assert.Equal(labBefore + 1, withLab.UnhappyCitizens);
			Assert.Equal(controlBefore, without.UnhappyCitizens);
		}

		// The counterplay, and it costs something: the building the player wanted.
		[Fact]
		public void SellingTheLabEndsTheMourning()
		{
			(Game g, _, City[] cities) = AWorldWithXenolabs(Game.XenolabsToWake);
			City c = cities[0];
			g.MourningUntilTurn = g.GameTurn + (uint)Game.MourningTurns;
			c.InvalidateCache();
			int grieving = c.UnhappyCitizens;

			c.RemoveBuilding<Xenolab>();
			c.InvalidateCache();

			Assert.Equal(grieving - 1, c.UnhappyCitizens);
		}

		// ...and it ends on its own. A grief with no end state is a punishment, which is not
		// what this is (docs/cursed_wonders.md rule 2).
		[Fact]
		public void TheMourningLapses()
		{
			(Game g, _, City[] cities) = AWorldWithXenolabs(Game.XenolabsToWake);
			City c = cities[0];
			// Off turn zero FIRST. A fresh game is on turn 0, so "the window closed" and "no
			// window was ever opened" were the same number — and this test passed with the
			// expiry replaced by a plain `> 0`. A negative check caught it.
			AdvanceOneRound(g);
			Assert.True(g.GameTurn > 0, "fixture: still on turn zero, so expiry is untestable");

			g.MourningUntilTurn = g.GameTurn + (uint)Game.MourningTurns;
			c.InvalidateCache();
			int grieving = c.UnhappyCitizens;

			g.MourningUntilTurn = g.GameTurn;   // a real window, now closed
			c.InvalidateCache();

			Assert.Equal(grieving - 1, c.UnhappyCitizens);
		}

		// ── the words, and the pictures ──────────────────────────────────────

		private static string Render(int koan) =>
			string.Join("\n", (string[])typeof(KoanTransmission)
				.GetMethod("BuildLines", BindingFlags.NonPublic | BindingFlags.Static)!
				.Invoke(null, new object[] { "1635 AD", koan })!);

		[Fact]
		public void EveryKoanIsItsOwnKoan()
		{
			Sim.EnsureRuntime();
			var seen = new System.Collections.Generic.HashSet<string>();

			for (int i = 1; i <= Game.KoansTotal; i++)
				Assert.True(seen.Add(Render(i)), $"koan {i} repeats an earlier one");
		}

		// The fifth does not end, it stops. If a rewrite ever gives it a tidy sign-off, the
		// death stops reading as a death.
		[Fact]
		public void TheLastOneStopsMidSentence()
		{
			Sim.EnsureRuntime();
			string last = Render(Game.KoansTotal);

			Assert.Contains("CARRIER TONE", last);
			Assert.DoesNotContain("TRANSMISSION ENDS", last);
			// The line the mourning is for.
			Assert.Contains("there was someone here", last);
		}

		// A missing plate degrades to text only (EventArtScreen.FindPath returns null rather
		// than substituting), which is silent — so demand the files.
		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(5)]
		public void EveryKoanPlateIsShipped(int koan)
		{
			string path = Path.Combine(Sim.RepoRoot(), "runtime", "sdl", "Resources",
				"defaults", "data", "event_art", $"Koan{koan}.png");

			Assert.True(File.Exists(path), $"koan art is missing: {path}");
		}

		// ── persistence ──────────────────────────────────────────────────────

		[Fact]
		public void TheAliensClocksSurviveASaveAndLoad()
		{
			(Game g, _, _) = AWorldWithXenolabs(Game.XenolabsToWake);
			AdvanceOneRound(g);
			Assert.True(g.AlienAwake, "fixture: it never woke");

			int koans = g.KoansSent;
			uint next = g.NextKoanTurn;

			string path = Path.Combine(Settings.Instance.SavesDirectory, "koans.cos");
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.True(Game.Instance.AlienAwake, "it forgot it was awake");
			Assert.Equal(koans, Game.Instance.KoansSent);
			Assert.Equal(next, Game.Instance.NextKoanTurn);
		}
	}
}
