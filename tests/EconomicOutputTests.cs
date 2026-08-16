// CivOne tests
//
// What the game means by "economic output".
//
// Two things were asked for and neither had been built. GrossOutput — which the Pax
// Mercatoria victory judges and the Economic Output page graphs — summed TradeTotal, the raw
// tile trade less corruption plus route bonuses. Marketplace and Bank multiply taxes and
// luxuries DOWNSTREAM of that, so a civ holding its entire commerce chain read identically to
// one holding none of it. And a large city was worth exactly its tiles: nothing represented
// the internal market a metropolis is.
//
// Both are victory-relevant, so both are pinned here rather than left to the AI tests, which
// only ever asked whether the buildings got BUILT.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class EconomicOutputTests
	{
		// A city with real trade to multiply. Bare grassland yields none, which is how an
		// earlier multiplier test in this repo managed to measure nothing at all.
		private static (Game game, Player player, City city) ATradingCity(int size = 4,
			IGovernment? government = null, bool distantCapital = false)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 32; x <= 48; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Road = true;      // roads are what put trade on grassland
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Government = government ?? new Republic();   // Republic: +1 trade per producing tile
			p.Explore(40, 25, range: 20);

			// Corruption is distance from the Palace, and the FIRST city founded takes it —
			// so a lone city is always the capital and always incorruptible. Found a capital
			// in the far corner when the test needs graft.
			if (distantCapital) g.AddCity(p, 0, 33, 19)!.Size = 4;

			City c = g.AddCity(p, distantCapital ? 1 : 0, 47, 31)!;
			c.Size = (byte)size;
			Sim.ClearTasks();
			return (g, p, c);
		}

		// ── the commerce chain ───────────────────────────────────────────────────

		[Fact]
		public void ACityWithNoCommerceBuildingsIsWorthItsTrade()
		{
			(Game g, Player p, City c) = ATradingCity();

			Assert.True(c.TradeTotal > 0, "fixture has no trade to multiply");
			Assert.Equal(c.TradeTotal, c.EconomicOutput);
		}

		// Each building must move the figure, and by the same sequential +50% the Taxes and
		// Luxuries getters use — 1.5x, then 2.25x. A second convention here would mean the
		// victory measured something the city screen does not.
		[Fact]
		public void TheCommerceChainMultipliesEconomicOutput()
		{
			(Game g, Player p, City c) = ATradingCity();
			int bare = c.EconomicOutput;

			c.AddBuilding(new MarketPlace());
			int withMarket = c.EconomicOutput;
			Assert.Equal(bare + bare / 2, withMarket);
			Assert.True(withMarket > bare, "a Marketplace must be worth something");

			c.AddBuilding(new Bank());
			int withBank = c.EconomicOutput;
			Assert.Equal(withMarket + withMarket / 2, withBank);
			Assert.True(withBank > withMarket, "a Bank must be worth something");
		}

		// A University is not commerce. Science has its own multiplier chain and must not leak
		// into the economic figure.
		[Fact]
		public void TheScienceChainDoesNotCountAsCommerce()
		{
			(Game g, Player p, City c) = ATradingCity();
			int bare = c.EconomicOutput;

			c.AddBuilding(new Library());
			c.AddBuilding(new CivOne.Buildings.University());

			Assert.Equal(bare, c.EconomicOutput);
		}

		// The whole point: the figure the VICTORY reads has to move too. GrossOutputOf and the
		// Pax Mercatoria check share one implementation, so pinning this pins both.
		[Fact]
		public void TheEmpireWideFigureCountsTheCommerceChain()
		{
			(Game g, Player p, City c) = ATradingCity();
			int before = g.GrossOutputOf(p);

			c.AddBuilding(new MarketPlace());

			Assert.True(g.GrossOutputOf(p) > before,
				$"gross output ignored the Marketplace: {before} -> {g.GrossOutputOf(p)}");
			Assert.Equal(c.EconomicOutput, g.GrossOutputOf(p));
		}

		// ── the circular economy ─────────────────────────────────────────────────

		// +1 at size 5, +2 at 10, and so on. Below 5 there is no internal market to speak of.
		[Theory]
		[InlineData(4,  0)]
		[InlineData(5,  1)]
		[InlineData(9,  1)]
		[InlineData(10, 2)]
		[InlineData(20, 4)]
		public void CitySizeAddsTrade(int size, int expectedBonus)
		{
			// ONE game. The first version built three and then read a city from the first,
			// which Sim.NewGame had already replaced — every case failed for that reason
			// rather than for the rule under test.
			(Game g, Player p, City c) = ATradingCity(size: size);

			int tilesOnly = c.ResourceTiles.Sum(t => c.TradeValue(t));

			Assert.True(tilesOnly > 0, "fixture has no tile trade");
			Assert.Equal(expectedBonus, c.RawTradeForAi - tilesOnly);
		}

		// It is trade, not a gift: corruption takes its cut like everything else a city earns.
		// Added to the total instead of the raw figure, it would have been incorruptible
		// income for exactly the distant cities that should be losing most to graft.
		[Fact]
		public void TheSizeBonusIsSubjectToCorruption()
		{
			// Despotism from the start, rather than switched afterwards: corruption is cached
			// per city and a late switch measures the cache, not the rule.
			(Game g, Player p, City c) = ATradingCity(size: 20, government: new Despotism(),
				distantCapital: true);

			Assert.True(c.Corruption > 0, "fixture has no corruption to apply");

			// No trade routes in this fixture, so TradeTotal is exactly RawTrade less
			// corruption. The bonus is inside RawTrade, so corruption is taken off the whole
			// figure including it.
			Assert.Equal(4, c.Size / 5);
			Assert.Equal(System.Math.Max(0, c.RawTradeForAi - c.Corruption), c.TradeTotal);
			Assert.True(c.TradeTotal < c.RawTradeForAi, "corruption took nothing");
		}
	}
}
