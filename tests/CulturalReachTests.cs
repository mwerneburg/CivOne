// CivOne tests
//
// Culture that nobody has met.
//
// A played game (23 Sep 2026) ended in Cultural Ascendancy with no foreign civilization
// ever having sent an ambassador. Two causes, both fixed here:
//
//   REACH. The clause had been `reach = true` since geography was dropped from the path.
//   It is now contact: half the surviving rivals must know the claimant by a trade route
//   (either direction), an embassy (either direction) or a defence pact — Pax Mercatoria's
//   Bound minus tribute.
//
//   AMBASSADORS. The AI diplomat could incite, steal or sabotage, and nothing else; it had
//   no branch for the human's first menu item. At peace and without an embassy, it now
//   opens one before anything else.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CulturalReachTests
	{
		// Admired on every other measure — Philosophy, the Electronics gate open, ten times any
		// rival's culture on equal populations, at peace — and known to NOBODY.
		private static (Game g, Player us, Player[] rivals) AdmiredButUnmet()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player us = g.HumanPlayer;
			Player[] rivals = g.Players
				.Where(p => p is not null && p != us && g.PlayerNumber(p) != 0).ToArray();
			foreach (Player p in rivals.Append(us))
			{
				p.Government = new Monarchy();
				p.Explore(40, 25, range: 20);
			}
			us.AddAdvance(new CivOne.Advances.Philosophy(), false);

			g.AddCity(us, 0, 40, 25)!.Size = 6;
			int id = 1;
			foreach (Player r in rivals) g.AddCity(r, id, 30 + id++ * 3, 30)!.Size = 6;

			us.SetCulture(6000);
			foreach (Player r in rivals) r.SetCulture(600);
			rivals[0].AddAdvance(new CivOne.Advances.Electronics(), false);
			Sim.ClearTasks();
			return (g, us, rivals);
		}

		private static uint StreakAfterATurn(Game g, Player us)
		{
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) g.EndTurn();
			Assert.True(g.GameTurn >= target, $"the fixture could not advance a turn (stuck at {g.GameTurn})");
			return g.Progress(g.PlayerNumber(us)).CultureStreak;
		}

		// Half, rounded up — the same bar Pax Mercatoria's boundHalf uses.
		private static int Half(Player[] rivals) => (rivals.Length + 1) / 2;

		[Fact]
		public void AnAdmiredCultureNobodyHasMetDoesNotAscend()
		{
			(Game g, Player us, Player[] rivals) = AdmiredButUnmet();
			Assert.True(rivals.Length >= 3, "the path needs three rivals; the fixture has too few");

			Assert.Equal(0u, StreakAfterATurn(g, us));
		}

		// The fixture is honest: the same world, met by half, runs the clock. Each kind of
		// contact is used so that none of them is silently ignored — OUR embassy, THEIR
		// embassy, a route in each direction. (Pacts count too, and are not exercised here.)
		[Fact]
		public void MetByHalfTheWorldItDoes()
		{
			(Game g, Player us, Player[] rivals) = AdmiredButUnmet();
			City ours = g.GetCities().First(c => c.Owner == g.PlayerNumber(us));
			var connect = new System.Action<Player>[]
			{
				r => us.EstablishEmbassy(r),
				r => r.EstablishEmbassy(us),
				r => ours.AddTradeRoute(g.GetCities().First(c => c.Owner == g.PlayerNumber(r)), "Silk"),
				r => g.GetCities().First(c => c.Owner == g.PlayerNumber(r)).AddTradeRoute(ours, "Silk"),
			};
			for (int i = 0; i < Half(rivals); i++) connect[i % connect.Length](rivals[i]);

			Assert.True(StreakAfterATurn(g, us) > 0, "met by half the world, the streak still did not run");
		}

		// One short of half is not enough.
		[Fact]
		public void OneShortOfHalfIsNot()
		{
			(Game g, Player us, Player[] rivals) = AdmiredButUnmet();
			for (int i = 0; i < Half(rivals) - 1; i++) us.EstablishEmbassy(rivals[i]);

			Assert.Equal(0u, StreakAfterATurn(g, us));
		}

		// The block is otherwise invisible — every readout on the score screen is green — so
		// the player is told, once, on the same latch the war block uses.
		[Fact]
		public void TheHumanIsToldWhyTheClockDoesNotStart()
		{
			(Game g, Player us, Player[] _) = AdmiredButUnmet();

			StreakAfterATurn(g, us);

			bool warned = (bool)typeof(Game).GetField("_cultBlockedNotified",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.GetValue(g)!;
			Assert.True(warned, "nothing told the player that half the world has never met them");
		}

		// ── the ambassador ───────────────────────────────────────────────────────

		// An AI diplomat beside a foreign AI city that holds an advance it could steal.
		private static (Diplomat dip, Player spy, Player host, City city) ADiplomatAtTheGate()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ais = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player spy = ais[0], host = ais[1];
			spy.Explore(42, 25, range: 12);
			g.AddCity(spy, 0, 38, 25);
			City city = g.AddCity(host, 1, 43, 25)!;
			city.Size = 4;
			host.AddAdvance(new CivOne.Advances.Electronics(), false);
			Assert.False(spy.HasAdvance<CivOne.Advances.Electronics>());

			Diplomat dip = (Diplomat)g.CreateUnit(UnitType.Diplomat, 42, 25, g.PlayerNumber(spy))!;
			dip.MovesLeft = dip.Move;
			Sim.ClearTasks();
			return (dip, spy, host, city);
		}

		private static void Pump() { for (int i = 0; i < 40 && GameTask.Any(); i++) GameTask.Update(); }

		private static bool Alive(IUnit u) => Game.Instance.GetUnits().Contains(u);

		[Fact]
		public void AtPeaceWithNoEmbassyTheDiplomatOpensOneAndStealsNothing()
		{
			(Diplomat dip, Player spy, Player host, City _) = ADiplomatAtTheGate();

			dip.MoveTo(1, 0);
			Pump();

			Assert.True(spy.HasEmbassy(host), "the diplomat did not open an embassy");
			Assert.False(spy.HasAdvance<CivOne.Advances.Electronics>(), "an envoy stole a technology");
			Assert.False(Alive(dip), "the diplomat is spent on its mission, as the human's is");
		}

		// The fixture is honest: with the embassy already open, the same diplomat steals.
		[Fact]
		public void WithAnEmbassyAlreadyOpenEspionageResumes()
		{
			(Diplomat dip, Player spy, Player host, City _) = ADiplomatAtTheGate();
			spy.EstablishEmbassy(host);

			dip.MoveTo(1, 0);
			Pump();

			Assert.True(spy.HasAdvance<CivOne.Advances.Electronics>(), "the fixture cannot steal at all");
		}

		// At war there is no envoy.
		[Fact]
		public void AtWarNoEmbassyIsOpened()
		{
			(Diplomat dip, Player spy, Player host, City _) = ADiplomatAtTheGate();
			spy.DeclareWar(host);

			dip.MoveTo(1, 0);
			Pump();

			Assert.False(spy.HasEmbassy(host));
		}
	}
}
