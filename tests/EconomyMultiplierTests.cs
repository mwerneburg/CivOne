// CivOne tests
//
// Bank and University were never passed to Consider(). Nothing gated them out — EarnsItsKeep
// would have let them through — they simply were not in the production chain, so the only way
// into a city was the last-resort fallback that fires when everything else is exhausted.
// Exactly the shape of the spaceship-parts omission, and with the same fingerprint in a
// finished world: 8% of 562 cities held a Bank, and the two civs with real coverage
// (Babylonians 44%, Guarani 30%) were the two small enough to run out of things to build.
//
// It matters because both are the SECOND tier of a multiplier chain — a further 50% on taxes
// and luxuries, a further 50% on science — so an AI without them is structurally capped no
// matter how well it plays. A human builds both, which is most of the gap between a human's
// 59% share of world output and the best AI's 13%.

using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Governments;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class EconomyMultiplierTests
	{
		// A city rich enough that EarnsItsKeep has something to multiply — the gate is
		// TradeTotal >= 2x maintenance, which is 6 for both of these.
		private static (Game g, Player p, City c) ATradingCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(45, 25, range: 30);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 12;

			// Roads, and they are the whole fixture. Bare grassland produces NO trade, so
			// EarnsItsKeep correctly refuses every multiplier and the plan comes back with
			// Militia, Temple and a wonder — which is what the first version of these tests
			// measured while appearing to prove the chain was still broken. A trade multiplier
			// test needs a city with trade to multiply.
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (System.Math.Abs(dx) == 2 && System.Math.Abs(dy) == 2) continue;
				ITile t = Map.Instance[40 + dx, 25 + dy];
				if (t is not null && !t.IsOcean) t.Road = true;
			}
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static List<IProduction> PlanFor(Player p, City c)
		{
			var plan = new List<IProduction>();
			var method = typeof(AI).GetMethod("PlanProductionInto",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.NotNull(method);
			var stanceType = typeof(AI).GetNestedType("StrategyStance",
				System.Reflection.BindingFlags.NonPublic);
			Assert.NotNull(stanceType);
			// Whatever the civ's own stance is — the chain under test is the one that runs in
			// every stance, so the default is the honest input.
			object stance = System.Enum.Parse(stanceType!, "Develop");
			return (List<IProduction>)method!.Invoke(AI.Instance(p), new object[] { plan, c, stance })!;
		}

		private static void Give(Player p, params IAdvance[] advances)
		{
			foreach (IAdvance a in advances) p.AddAdvance(a, false);
		}

		// The bug, stated directly: with Banking known and a marketplace already standing, a
		// trading city must plan a Bank.
		[Fact]
		public void ATradingCityWithBankingPlansABank()
		{
			(Game g, Player p, City c) = ATradingCity();
			Give(p, new Currency(), new Trade(), new Banking());
			c.AddBuilding(new MarketPlace());

			Assert.Contains(PlanFor(p, c), x => x is Bank);
		}

		// Same for the science chain.
		[Fact]
		public void ATradingCityWithUniversityTechPlansAUniversity()
		{
			(Game g, Player p, City c) = ATradingCity();
			Give(p, new Writing(), new Literacy(), new CivOne.Advances.University());
			c.AddBuilding(new Library());

			Assert.Contains(PlanFor(p, c), x => x is CivOne.Buildings.University);
		}

		// Cheaper half first. The plan is ordered and the city builds its head, so a city with
		// neither must reach for the Marketplace before the Bank — same +50%, two thirds of the
		// price. This is what makes an explicit prerequisite unnecessary.
		[Fact]
		public void TheMarketplaceIsPlannedAheadOfTheBank()
		{
			(Game g, Player p, City c) = ATradingCity();
			Give(p, new Currency(), new Trade(), new Banking());

			List<IProduction> plan = PlanFor(p, c);
			int market = plan.FindIndex(x => x is MarketPlace);
			int bank = plan.FindIndex(x => x is Bank);

			// Both must be present, not just correctly ordered. The first version allowed
			// "bank < 0" as a pass, which meant it went green against the very code that omits
			// the Bank entirely — the negative check caught it sitting there proving nothing.
			Assert.True(market >= 0, "no marketplace planned at all");
			Assert.True(bank >= 0, "no bank planned at all");
			Assert.True(market < bank,
				$"bank (index {bank}) was planned ahead of the marketplace (index {market})");
		}

		// And the existing value gate still bites: a tiny city with nothing to multiply does
		// not take on 3 gold a turn for a 50% share of almost nothing. This is the clause that
		// makes the addition safe to make unconditionally.
		[Fact]
		public void ADestituteCityPlansNeither()
		{
			(Game g, Player p, City c) = ATradingCity();
			Give(p, new Currency(), new Trade(), new Banking(),
				new Writing(), new Literacy(), new CivOne.Advances.University());
			c.Size = 1;

			List<IProduction> plan = PlanFor(p, c);

			Assert.DoesNotContain(plan, x => x is Bank);
			Assert.DoesNotContain(plan, x => x is CivOne.Buildings.University);
		}
	}
}
