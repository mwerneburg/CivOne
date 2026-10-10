// CivOne tests
//
// Fascism: the AI-only war government of a warmonger who senses he is losing (the user, Oct
// 2026). The last centuries of game d721724c were silent because every AI was a Democracy;
// this is a way out of the Senate that costs the regime its science and its culture.

using System.Linq;
using System.Reflection;
using Conscription = CivOne.Advances.Conscription;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class FascismTests : System.IDisposable
	{
		private readonly bool _auto;
		public FascismTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		private static bool Offered(Player p) => p.AvailableGovernments.Any(g => g is Fascism);

		private static Player AI(Game g, bool warlike) => g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			&& p != g.HumanPlayer && CivOne.AI.IsWarMinded(p.Civilization.Leader) == warlike);

		private static (Game g, Player warmonger, Player[] others) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 15; x <= 65; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player w = AI(g, warlike: true);
			w.AddAdvance(new Conscription(), false);
			Player[] others = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != w).ToArray();
			Sim.ClearTasks();
			return (g, w, others);
		}

		// ── who may have it ──────────────────────────────────────────────────

		[Fact]
		public void AWarmongerWithConscriptionIsOfferedIt()
		{
			(Game _, Player w, Player[] _) = AWorld();
			Assert.True(Offered(w));
		}

		[Fact]
		public void ACivilisedLeaderIsNot()
		{
			(Game g, Player _, Player[] _) = AWorld();
			Player calm = AI(g, warlike: false);
			calm.AddAdvance(new Conscription(), false);
			Assert.False(Offered(calm));
		}

		// Never the human: not a warmonger with the advance, not under autopilot, not with the
		// Pyramids. The warmonger is seated at the controls, so the leader gate cannot be what
		// refuses it.
		[Fact]
		public void TheHumanIsNeverOfferedIt()
		{
			(Game g, Player human, Player[] _) = AWorld();
			Assert.True(Offered(human), "fixture: the warmonger is not offered it even as an AI");
			g.HumanPlayer = human;
			g.AddCity(human, 0, 40, 25)!.AddWonder(new CivOne.Wonders.Pyramids());

			Assert.False(Offered(human));
			Settings.Instance.Autopilot = true;
			Assert.False(Offered(human));
		}

		// ── what it does ─────────────────────────────────────────────────────

		private static City ACity(Game g, Player p, int x = 40, int y = 25)
		{
			p.Explore(x, y, range: 3);
			City c = g.AddCity(p, 0, x, y)!;
			c.Size = 8;
			c.AddBuilding(new CivOne.Buildings.Temple());
			c.AddBuilding(new CivOne.Buildings.Library());
			c.ResetResourceTiles();
			return c;
		}

		[Fact]
		public void ItHalvesScience()
		{
			(Game g, Player w, Player[] _) = AWorld();
			for (int y = 21; y <= 29; y++)
			for (int x = 36; x <= 44; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.River);   // grassland yields no trade
			City c = ACity(g, w);
			w.TaxesRate = 0; w.LuxuriesRate = 0;
			w.Government = new Monarchy(); c.InvalidateCache();
			int monarchy = c.Science;
			w.Government = new Fascism(); c.InvalidateCache();

			Assert.True(monarchy >= 4, $"fixture: only {monarchy} science to halve");
			Assert.Equal(monarchy / 2, c.Science);
		}

		[Fact]
		public void ItsCultureCountsForNothing()
		{
			(Game g, Player w, Player[] _) = AWorld();
			City c = ACity(g, w);
			w.Government = new Monarchy();
			Assert.True(c.CultureRate > 0, "fixture: a Temple and Library earn nothing");
			w.Government = new Fascism();
			Assert.Equal(0, c.CultureRate);
		}

		// Take no prisoners: the city it captures loses half its people. Against the same
		// capture by a Monarchy, so the halving is Fascism's and not capture's own.
		private static int SizeAfterCapture(IGovernment taker)
		{
			(Game g, Player w, Player[] others) = AWorld();
			Player victim = others.First(p => p != g.HumanPlayer);
			w.Government = taker;
			victim.Government = new Monarchy();
			City c = g.AddCity(victim, 0, 40, 25)!;
			c.Size = 8;
			IUnit legion = g.CreateUnit(UnitType.Legion, 41, 25, g.PlayerNumber(w))!;
			w.DeclareWar(victim);
			Sim.ClearTasks();

			legion.MoveTo(-1, 0);
			Sim.Settle();

			Assert.Equal(g.PlayerNumber(w), c.Owner);   // fixture: it really was taken
			return c.Size;
		}

		[Fact]
		public void ItTakesNoPrisoners()
		{
			int monarchy = SizeAfterCapture(new Monarchy());
			int fascist = SizeAfterCapture(new Fascism());
			Assert.True(monarchy >= 4, $"fixture: capture alone left {monarchy}");
			Assert.Equal(monarchy / 2, fascist);
		}

		// ── how it fights ────────────────────────────────────────────────────

		// The UN holder and a rich trading partner: a Monarchy leaves it alone, a Fascist does not.
		private static bool DeclaresOnAProtectedPartner(IGovernment gov, bool un = true, bool theyAreStronger = false)
		{
			(Game g, Player w, Player[] others) = AWorld();
			Player them = others.First(p => p != g.HumanPlayer);
			w.Government = gov;
			them.Government = new Monarchy();
			int id = 0;
			City[] ours = Enumerable.Range(0, 12).Select(i => g.AddCity(w, id++, 18 + (i % 6) * 4, 18 + (i / 6) * 4)!).ToArray();
			City[] theirs = Enumerable.Range(0, 3).Select(i => g.AddCity(them, id++, 22 + i * 6, 32)!).ToArray();
			for (int y = 15; y <= 35; y++)
			for (int x = 15; x <= 65; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.River);   // grassland carries no trade
			foreach (City c in ours.Concat(theirs)) { c.Size = 10; c.ResetResourceTiles(); }
			if (un) theirs[0].AddWonder(new CivOne.Wonders.UnitedNations());
			if (theyAreStronger) for (int i = 0; i < 8; i++) g.CreateUnit(UnitType.Armor, 22, 32, g.PlayerNumber(them));
			foreach (City c in ours) foreach (City t in theirs) { c.AddTradeRoute(t, "Silk"); t.AddTradeRoute(c, "Silk"); }
			for (int i = 0; i < 4; i++) g.CreateUnit(UnitType.Armor, 18, 18, g.PlayerNumber(w));
			Sim.ClearTasks();
			byte wn = g.PlayerNumber(w), tn = g.PlayerNumber(them);
			int trade = ours.SelectMany(c => c.TradeRoutes).Where(r => r.Partner.Owner == tn).Sum(r => r.Value)
			          + theirs.SelectMany(c => c.TradeRoutes).Where(r => r.Partner.Owner == wn).Sum(r => r.Value);
			Assert.True(trade / 2 >= 20, $"fixture: routes worth only {trade}, the penalty is not at its cap");

			CivOne.AI ai = CivOne.AI.Instance(w)!;
			for (int t = 0; t < 60 && !w.IsAtWar(them); t++) ai.ConsiderWar();
			return w.IsAtWar(them);
		}

		// Trade on its own: a Fascist that is the WEAKER side rolls 8 + 20 - 20 = 8%, which only
		// the trade penalty, at its full 20, would take to nothing.
		[Fact]
		public void TradeDoesNotRestrainIt() =>
			Assert.True(DeclaresOnAProtectedPartner(new Fascism(), un: false, theyAreStronger: true));

		[Fact]
		public void NeitherTradeNorTheUnitedNationsRestrainsIt()
		{
			Assert.False(DeclaresOnAProtectedPartner(new Monarchy()), "fixture: a Monarchy attacked the UN holder");
			Assert.True(DeclaresOnAProtectedPartner(new Fascism()));
		}

		// ── how it rises ─────────────────────────────────────────────────────

		private static int Ambition(CivOne.AI ai) =>
			(int)typeof(CivOne.AI).GetField("_warAmbition", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(ai)!;

		// A warmonger democracy, with a rival half way to Pax Mercatoria: it reaches for the
		// war ambition, and the government it then rates best is Fascism.
		[Fact]
		public void AWarmongerWhoSensesDefeatTurnsToFascism()
		{
			(Game g, Player w, Player[] others) = AWorld();
			w.Government = new Democracy();
			g.Progress(g.PlayerNumber(others.First(p => p != g.HumanPlayer))).EconStreak = Game.EconomicHoldTurns / 2;
			CivOne.AI ai = CivOne.AI.Instance(w)!;
			Assert.True(ai.SensesDefeat(), "fixture: not losing");

			for (int t = 0; t < 100 && Ambition(ai) == 0; t++) ai.ConsiderWar();

			Assert.True(Ambition(ai) > 0, "never reached for war");
			Assert.Equal(nameof(Fascism), ai.BestGovernmentName());
		}

		// ...and one who is winning keeps to the ordinary war governments.
		[Fact]
		public void AWarmongerWhoIsWinningDoesNot()
		{
			(Game g, Player w, Player[] _) = AWorld();
			w.Government = new Democracy();
			w.AddAdvance(new CivOne.Advances.Monarchy(), false);
			CivOne.AI ai = CivOne.AI.Instance(w)!;
			typeof(CivOne.AI).GetField("_warAmbition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ai, 25);
			Assert.False(ai.SensesDefeat(), "fixture: losing");

			Assert.NotEqual(nameof(Fascism), ai.BestGovernmentName());
		}

		// Holding: a Fascist state still losing and at war keeps its ambition, rather than
		// letting it lapse and revolting through Anarchy mid-war.
		[Fact]
		public void AFascistStateAtWarAndLosingHoldsItsCourse()
		{
			(Game g, Player w, Player[] others) = AWorld();
			w.Government = new Fascism();
			Player rival = others.First(p => p != g.HumanPlayer);
			g.Progress(g.PlayerNumber(rival)).EconStreak = Game.EconomicHoldTurns / 2;
			w.DeclareWar(rival);
			CivOne.AI ai = CivOne.AI.Instance(w)!;
			typeof(CivOne.AI).GetField("_warAmbition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ai, 1);

			for (int t = 0; t < 5; t++) ai.ConsiderWar();

			Assert.True(Ambition(ai) > 0, "the ambition lapsed mid-war");
			// null is "already optimal": it stays as it is.
			Assert.Equal(nameof(Fascism), ai.BestGovernmentName() ?? nameof(Fascism));
		}

		// Rising: every other nation holds a grudge, and the world is told.
		[Fact]
		public void TheWorldHoldsAGrudgeAndHearsOfIt()
		{
			(Game g, Player w, Player[] others) = AWorld();
			w.Government = new Anarchy();
			g.Progress(g.PlayerNumber(others.First(p => p != g.HumanPlayer))).EconStreak = Game.EconomicHoldTurns / 2;
			CivOne.AI ai = CivOne.AI.Instance(w)!;
			typeof(CivOne.AI).GetField("_warAmbition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ai, 25);
			Sim.ClearTasks();

			ai.ChooseGovernment();

			Assert.IsType<Fascism>(w.Government);
			Assert.All(others.Where(p => !(p.Civilization is Civilizations.Olvir)), p => Assert.True(p.HasGrudge(w), $"{p.TribeName} bears no grudge"));
			Assert.Contains("Message:Newspaper", Probes());
		}

		// A Fascist state that loses a quarter of its peacetime cities collapses into anarchy.
		[Fact]
		public void LosingAQuarterOfItsCitiesCollapsesIt()
		{
			(Game g, Player w, Player[] _) = AWorld();
			w.Government = new Fascism();
			for (int i = 0; i < 3; i++) g.AddCity(w, i, 20 + i * 4, 20);
			CivOne.AI ai = CivOne.AI.Instance(w)!;
			typeof(CivOne.AI).GetField("_peacetimeCities", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ai, 4);
			// At war, so the baseline holds instead of resetting to today's count.
			w.DeclareWar(g.Players.First(p => p is not null && p != w && g.PlayerNumber(p) != 0 && p != g.HumanPlayer));
			Sim.ClearTasks();

			ai.ConsiderWar();

			Assert.IsType<Anarchy>(w.Government);
		}

		private static string[] Probes() =>
			((System.Collections.IList)typeof(GameTask).GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!)
				.Cast<GameTask>().Select(t => t.ProbeName).ToArray();
	}
}
