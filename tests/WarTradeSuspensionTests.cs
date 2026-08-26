// CivOne tests
//
// A war used to delete trade routes. Now it interrupts them.
//
// Player.DeclareWar removed every route between the two civilizations outright, and peace did
// not bring them back — each one had to be re-established by a fresh caravan. That was a fair
// price when a route was worth a few trade. Under Civ 1's formula it is not: measured on a
// developed Frankish empire, route income was 11,305 of 13,491 total trade, 84% of the whole
// economy. One declaration was a permanent amputation rather than a cost of war, and on
// autopilot the game will make it on the player's behalf.
//
// Observed in game 3de868a5: a war around turn 473 took world output from 53,324 to 17,260 in
// five turns, with every civilization's cities, population and culture untouched, and nothing
// ever recovered — the Franks ended the run at 8,728 against 30,086 before it.
//
// City.RouteBonus already paid nothing while the owners were at war, so suspension needed no
// new mechanism. It needed DeclareWar to stop deleting, and PruneWorthlessRoutes to stop
// mistaking a wartime zero for a worthless route.
//
// And wars are in the decision log now. They were the one major event it never recorded,
// which is why two runs in a row had to be reconstructed from the shape of the hole.

using System.IO;
using System.Linq;
using System.Text.Json;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class WarTradeSuspensionTests
	{
		// Two civilizations, a city each on separate continents, and a route between them.
		private static (Game game, Player us, Player them, City ours, City theirs) TwoTraders()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 0; y < 50; y++)
			for (int x = 0; x < 80; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 18; y <= 30; y++)
			{
				for (int x = 8; x <= 18; x++)  Map.Instance.ChangeTileType(x, y, Terrain.River);
				for (int x = 58; x <= 68; x++) Map.Instance.ChangeTileType(x, y, Terrain.River);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != us);
			us.Explore(12, 24, range: 40);
			them.Explore(64, 24, range: 40);

			City ours = g.AddCity(us, 0, 12, 24)!;
			City theirs = g.AddCity(them, 1, 64, 24)!;
			ours.Size = 16;
			theirs.Size = 16;
			ours.InvalidateCache();
			theirs.InvalidateCache();
			ours.AddTradeRoute(theirs, "Silk");
			theirs.AddTradeRoute(ours, "Silk");
			Sim.ClearTasks();
			return (g, us, them, ours, theirs);
		}

		// The fixture must have a route worth something, or "it survived the war" is vacuous.
		[Fact]
		public void TheRouteIsWorthSomethingToBeginWith()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();

			Assert.Equal(1, ours.TradeRouteCount);
			Assert.True(ours.TradeRoutes.Single().Value > 0, "the fixture route pays nothing");
		}

		// The report: war no longer deletes the route.
		[Fact]
		public void WarSuspendsTheRouteInsteadOfDeletingIt()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();

			us.DeclareWar(them);
			Sim.ClearTasks();

			Assert.Equal(1, ours.TradeRouteCount);
			Assert.Equal(1, theirs.TradeRouteCount);
		}

		// ...and it still costs you everything it used to cost, for as long as the war lasts.
		// Suspension must not become a way to trade with an enemy.
		[Fact]
		public void ASuspendedRoutePaysNothing()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			int before = ours.TradeTotal;

			us.DeclareWar(them);
			Sim.ClearTasks();
			ours.InvalidateCache();

			Assert.Equal(0, ours.TradeRoutes.Single().Value);
			Assert.True(ours.TradeTotal < before, "the war cost the city nothing");
		}

		// The pruner runs every turn from City.NewTurn and deletes routes worth nothing. It
		// must not take the suspended ones — that is the deletion coming back by the side door,
		// and it is what makes peace unable to restore anything.
		[Fact]
		public void ThePrunerLeavesSuspendedRoutesAlone()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			us.DeclareWar(them);
			Sim.ClearTasks();

			ours.PruneWorthlessRoutes();
			theirs.PruneWorthlessRoutes();

			Assert.Equal(1, ours.TradeRouteCount);
			Assert.Equal(1, theirs.TradeRouteCount);
		}

		// The whole point: peace brings the trade back with no caravan required.
		[Fact]
		public void PeaceRestoresTheTrade()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			int before = ours.TradeRoutes.Single().Value;
			us.DeclareWar(them);
			Sim.ClearTasks();

			us.MakePeace(them);
			Sim.ClearTasks();
			ours.InvalidateCache();

			Assert.Equal(before, ours.TradeRoutes.Single().Value);
		}

		// A route that is worthless for an ordinary reason must still be pruned — the
		// exemption is for war, not for every zero. A razed partner is the clearest case.
		[Fact]
		public void ARouteToADeadCityIsStillPruned()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			theirs.X = 255;
			theirs.Y = 255;
			ours.InvalidateCache();

			ours.PruneWorthlessRoutes();

			Assert.Equal(0, ours.TradeRouteCount);
		}

		// ── the log ──────────────────────────────────────────────────────────

		private static string LogPath =>
			Path.Combine(Settings.Instance.DataDirectory, "decisions.jsonl");

		// Every test in the suite appends to ONE decisions.jsonl, and several of them declare
		// wars of their own. Reading "the last war record in the file" passed in isolation and
		// failed in the full run, which is the classic shape of this mistake — so each test
		// marks where the file ended before it acts and reads only what it wrote.
		private static int LogMark() => File.Exists(LogPath) ? File.ReadLines(LogPath).Count() : 0;

		private static JsonElement[] WarsSince(int mark) =>
			File.ReadLines(LogPath).Skip(mark)
				.Where(l => l.Contains("\"type\":\"war\""))
				.Select(l => JsonDocument.Parse(l).RootElement).ToArray();

		// The event that explains a run is now IN the run's record.
		[Fact]
		public void ADeclarationIsLogged()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			int mark = LogMark();

			us.DeclareWar(them);
			Sim.ClearTasks();

			JsonElement rec = Assert.Single(WarsSince(mark));
			Assert.Equal(us.Civilization.NamePlural, rec.GetProperty("aggressor").GetString());
			Assert.Equal(them.Civilization.NamePlural, rec.GetProperty("defender").GetString());
			Assert.True(rec.GetProperty("is_human").GetBoolean());
			Assert.False(rec.GetProperty("honouring_pact").GetBoolean());
		}

		// routes_cut is what makes the record worth reading: it says how much of the economy
		// the declaration just switched off, which is the number that explained the collapse.
		[Fact]
		public void TheRecordCountsWhatTheWarInterrupted()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			int mark = LogMark();

			us.DeclareWar(them);
			Sim.ClearTasks();

			Assert.Equal(1, Assert.Single(WarsSince(mark)).GetProperty("routes_cut").GetInt32());
		}

		// A war joined by treaty is not a war you started — only the latter breaks a victory
		// streak (Game.RecordWarStart reads the same flag), so a log that conflated them would
		// answer the question wrongly rather than not at all.
		[Fact]
		public void APactHonouredIsRecordedAsSuch()
		{
			(Game g, Player us, Player them, City ours, City theirs) = TwoTraders();
			Player ally = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                                && p != us && p != them);
			ally.SetDefensePact(them, 40);
			them.SetDefensePact(ally, 40);
			Sim.ClearTasks();
			int mark = LogMark();

			us.DeclareWar(them);
			Sim.ClearTasks();

			// Two records: the declaration, and the ally joining by treaty.
			JsonElement[] wars = WarsSince(mark);
			Assert.Equal(2, wars.Length);
			JsonElement joined = wars.Single(w => w.GetProperty("aggressor").GetString()
			                                    == ally.Civilization.NamePlural);

			Assert.True(joined.GetProperty("honouring_pact").GetBoolean());
			Assert.Equal(us.Civilization.NamePlural, joined.GetProperty("defender").GetString());
		}
	}
}
