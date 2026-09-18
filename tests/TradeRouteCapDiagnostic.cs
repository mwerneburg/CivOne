// CivOne diagnostic (not an assertion)
//
// What a trade-route cap would actually change, measured on a finished save.
//
// Two different uncapped things live in City, and only one of them has ever been capped:
//
//   MONEY  TradeTotal = BaseTrade + TradeRouteBonus — every route, internal and external,
//          full value, no cap. This is gold, science and luxuries: the treasury.
//   SCORE  ScoringTrade = BaseTrade + ScoringRouteBonus — external only, best
//          City.ScoringRoutes per city. This is Pax Mercatoria and the Output graph.
//
// Reported from a real game (CIVIL1, turn 355): tax rate permanently 0%, treasury over
// 100,000, the whole Dome sequence bought in five turns, and 59.4% of world output — the
// last of those already measured UNDER the scoring cap. So the two complaints are two
// knobs, and this prints both before either is turned.
//
//   CIVONE_ENDGAME_SAVE=/path/to.cos dotnet test --filter TradeRouteCap -l "console;verbosity=detailed"

using System;
using System.Linq;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class TradeRouteCapDiagnostic
	{
		private readonly ITestOutputHelper _out;
		public TradeRouteCapDiagnostic(ITestOutputHelper output) => _out = output;

		private static readonly int[] Caps = { 1, 2, 3, 4, 5, 8, 99 };

		[Fact]
		public void WhatACapWouldDo()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			Player[] live = g.Players
				.Where(p => p is not null && !p.IsDestroyed() && g.PlayerNumber(p) != 0).ToArray();

			_out.WriteLine($"{path}  turn {g.GameTurn} ({Common.YearString(g.GameTurn)})");
			_out.WriteLine($"current ScoringRoutes = {City.ScoringRoutes}");
			_out.WriteLine("");

			// ── 1. Where the money comes from ────────────────────────────────────────
			_out.WriteLine("MONEY (TradeTotal = BaseTrade + every route, uncapped)");
			_out.WriteLine("civ                cities   base   routes   total  route%   gold  routes/city max");
			foreach (Player p in live.OrderByDescending(p => Money(p)))
			{
				City[] cs = p.Cities.Where(c => c.Size > 0).ToArray();
				int bas = cs.Sum(c => c.BaseTrade);
				int rte = cs.Sum(c => c.TradeRoutes.Sum(r => r.Value));
				int tot = bas + rte;
				int n = cs.Sum(c => c.TradeRouteCount);
				int max = cs.Length == 0 ? 0 : cs.Max(c => c.TradeRouteCount);
				_out.WriteLine($"{p.TribeNamePlural,-18} {cs.Length,5} {bas,7} {rte,8} {tot,7} "
					+ $"{(tot > 0 ? 100.0 * rte / tot : 0),5:F1}% {p.Gold,7} "
					+ $"{(cs.Length == 0 ? 0 : (double)n / cs.Length),8:F1} {max,4}");
			}
			_out.WriteLine("");

			// ── 2. The money knob: cap the per-city route bonus ───────────────────────
			// Same shape the scoring cap already uses (best N), applied to the money side.
			_out.WriteLine("MONEY under a per-city cap on how many routes PAY (best N, all routes):");
			_out.WriteLine("civ                " + string.Join("", Caps.Select(c => $"{(c == 99 ? "none" : c.ToString()),9}")));
			foreach (Player p in live.OrderByDescending(p => Money(p)))
			{
				string row = string.Join("", Caps.Select(cap =>
					$"{p.Cities.Where(c => c.Size > 0).Sum(c => c.BaseTrade + CappedRoutes(c, cap, externalOnly: false)),9}"));
				_out.WriteLine($"{p.TribeNamePlural,-18}{row}");
			}
			_out.WriteLine("");

			// ── 3. The score knob: vary ScoringRoutes ────────────────────────────────
			// Share of world output, which is the Pax Mercatoria bar (>50%). Multipliers are
			// applied the way EconomicOutput applies them so the shares stay comparable.
			_out.WriteLine("SHARE OF WORLD OUTPUT under different ScoringRoutes (external only):");
			_out.WriteLine("civ                " + string.Join("", Caps.Select(c => $"{(c == 99 ? "none" : c.ToString()),9}")));
			foreach (int cap in new[] { 0 })   // header alignment only
				_ = cap;
			var byCap = Caps.ToDictionary(cap => cap,
				cap => live.ToDictionary(p => p, p => Output(p, cap)));
			foreach (Player p in live.OrderByDescending(p => Output(p, City.ScoringRoutes)))
			{
				string row = string.Join("", Caps.Select(cap =>
				{
					int world = byCap[cap].Values.Sum();
					return $"{(world > 0 ? 100.0 * byCap[cap][p] / world : 0),8:F1}%";
				}));
				_out.WriteLine($"{p.TribeNamePlural,-18}{row}");
			}
			_out.WriteLine("");
			_out.WriteLine("Pax Mercatoria needs >50% of world output. A cap only helps if it moves the");
			_out.WriteLine("LEADER down relative to the field — a cap that cuts everyone equally changes");
			_out.WriteLine("the treasury but not the share.");

			// ── 4. Internal vs external, for the leader ──────────────────────────────
			Player top = live.OrderByDescending(Money).First();
			City[] tc = top.Cities.Where(c => c.Size > 0).ToArray();
			int intern = tc.Sum(c => c.TradeRoutes.Where(r => r.Partner.Owner == c.Owner).Sum(r => r.Value));
			int extern_ = tc.Sum(c => c.TradeRoutes.Where(r => r.Partner.Owner != c.Owner).Sum(r => r.Value));
			int internN = tc.Sum(c => c.TradeRoutes.Count(r => r.Partner.Owner == c.Owner));
			int externN = tc.Sum(c => c.TradeRoutes.Count(r => r.Partner.Owner != c.Owner));
			_out.WriteLine("");
			_out.WriteLine($"{top.TribeNamePlural}: {internN} internal routes worth {intern}, "
				+ $"{externN} external worth {extern_}");
			_out.WriteLine("(internal routes are already halved by RouteBonus and score nothing, "
				+ "but they pay the treasury in full)");

			// ── 5. Concentration: how many routes point at the SAME partner city? ────
			// The reported strategy is "target one foreign city for trade". RouteBonus scales
			// with (BaseTrade of both ends), so one very rich partner makes every route to it
			// enormous — and nothing limits how many of your cities may anchor on it.
			_out.WriteLine("");
			_out.WriteLine($"{top.TribeNamePlural} external routes grouped by PARTNER city:");
			_out.WriteLine("partner (owner)            routes    value   avg");
			var byPartner = tc.SelectMany(c => c.TradeRoutes.Where(r => r.Partner.Owner != c.Owner))
				.GroupBy(r => r.Partner)
				.Select(grp => (City: grp.Key, N: grp.Count(), Value: grp.Sum(r => r.Value)))
				.OrderByDescending(x => x.Value)
				.ToArray();
			foreach (var x in byPartner.Take(12))
				_out.WriteLine($"{x.City.Name + " (" + g.GetPlayer(x.City.Owner).TribeName + ")",-26} "
					+ $"{x.N,5} {x.Value,8} {(double)x.Value / Math.Max(1, x.N),5:F0}");
			_out.WriteLine($"... {byPartner.Length} distinct partners in total");
			int topFive = byPartner.Take(5).Sum(x => x.Value);
			_out.WriteLine($"top 5 partners carry {topFive} of {extern_} external value "
				+ $"({(extern_ > 0 ? 100.0 * topFive / extern_ : 0):F0}%)");

			// What an EMPIRE-WIDE per-partner cap would leave: the best N routes anchored on
			// any one foreign city, summed over partners.
			_out.WriteLine("");
			_out.WriteLine("Leader's external route value under a per-PARTNER cap (best N per partner city):");
			foreach (int cap in Caps)
				_out.WriteLine($"  cap {(cap == 99 ? "none" : cap.ToString()),-4} "
					+ byPartner.Sum(x => x.N <= cap ? x.Value
						: tc.SelectMany(c => c.TradeRoutes.Where(r => r.Partner == x.City && r.Partner.Owner != c.Owner))
						    .Select(r => r.Value).OrderByDescending(v => v).Take(cap).Sum()));

			// ── 6. The candidate rule, across the whole field ────────────────────────
			// Per (civilization, partner city): your empire may anchor at most N routes on any
			// one foreign city. Scoped to the claiming civ so one civ's caravans can never
			// evict another's — the eviction pathology that got Civ 1's cap removed.
			_out.WriteLine("");
			_out.WriteLine("SHARE OF WORLD OUTPUT under a per-(civ,partner) cap:");
			_out.WriteLine("civ                " + string.Join("", Caps.Select(c => $"{(c == 99 ? "none" : c.ToString()),9}")));
			var byPartnerCap = Caps.ToDictionary(cap => cap,
				cap => live.ToDictionary(p => p, p => OutputPerPartner(p, cap)));
			foreach (Player p in live.OrderByDescending(p => byPartnerCap[3][p]))
			{
				string row = string.Join("", Caps.Select(cap =>
				{
					int world = byPartnerCap[cap].Values.Sum();
					return $"{(world > 0 ? 100.0 * byPartnerCap[cap][p] / world : 0),8:F1}%";
				}));
				_out.WriteLine($"{p.TribeNamePlural,-18}{row}");
			}
			// ── 7. Soft alternative: diminishing returns per partner ─────────────────
			// nth route anchored on the same foreign city pays 1/n. No cliff, no wasted
			// caravan, no eviction — the 20th route to Coatepec is worth 5% of the first
			// rather than nothing, and the player is pushed to find new partners without
			// ever being told a delivery was pointless.
			_out.WriteLine("");
			_out.WriteLine("SHARE and TREASURY under DIMINISHING RETURNS (nth route to a partner pays 1/n):");
			_out.WriteLine("civ                  share   treasury     (now: share / treasury)");
			int worldDim = live.Sum(p => OutputDiminishing(p));
			foreach (Player p in live.OrderByDescending(p => OutputDiminishing(p)))
			{
				int nowWorld = live.Sum(q => Output(q, City.ScoringRoutes));
				_out.WriteLine($"{p.TribeNamePlural,-18} "
					+ $"{(worldDim > 0 ? 100.0 * OutputDiminishing(p) / worldDim : 0),6:F1}% "
					+ $"{MoneyDiminishing(p),10}     "
					+ $"{(nowWorld > 0 ? 100.0 * Output(p, City.ScoringRoutes) / nowWorld : 0),5:F1}% / {Money(p)}");
			}

			_out.WriteLine("");
			_out.WriteLine("Also the TREASURY side (TradeTotal) under the same per-(civ,partner) cap:");
			_out.WriteLine("civ                " + string.Join("", Caps.Select(c => $"{(c == 99 ? "none" : c.ToString()),9}")));
			foreach (Player p in live.OrderByDescending(Money))
				_out.WriteLine($"{p.TribeNamePlural,-18}"
					+ string.Join("", Caps.Select(cap => $"{MoneyPerPartner(p, cap),9}")));
		}

		// What the SHIPPED code now produces, read straight off City rather than reimplemented
		// here — the check that the rule in City.DiminishedRoutes matches the model that was
		// used to choose it.
		[Fact]
		public void WhatTheImplementedCurveDoes()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			Player[] live = g.Players
				.Where(p => p is not null && !p.IsDestroyed() && g.PlayerNumber(p) != 0).ToArray();
			int world = live.Sum(p => p.Cities.Where(c => c.Size > 0).Sum(c => c.EconomicOutput));

			_out.WriteLine($"{path}  turn {g.GameTurn} ({Common.YearString(g.GameTurn)})  AS IMPLEMENTED");
			_out.WriteLine("civ                 output   share   treasury(TradeTotal)   raw routes");
			foreach (Player p in live
			         .OrderByDescending(p => p.Cities.Where(c => c.Size > 0).Sum(c => c.EconomicOutput)))
			{
				City[] cs = p.Cities.Where(c => c.Size > 0).ToArray();
				int outp = cs.Sum(c => c.EconomicOutput);
				int money = cs.Sum(c => c.TradeTotal);
				int raw = cs.Sum(c => c.BaseTrade + c.TradeRoutes.Sum(r => r.Value));
				_out.WriteLine($"{p.TribeNamePlural,-18} {outp,7} {(world > 0 ? 100.0 * outp / world : 0),6:F1}% "
					+ $"{money,20} {raw,12}");
			}
			_out.WriteLine("'raw routes' is what the treasury WOULD be with every route at full value.");
		}

		private static int Money(Player p) =>
			p.Cities.Where(c => c.Size > 0).Sum(c => c.BaseTrade + c.TradeRoutes.Sum(r => r.Value));

		// The best `cap` routes by value, mirroring ScoringRouteBonus's shape.
		private static int CappedRoutes(City c, int cap, bool externalOnly) => c.TradeRoutes
			.Where(r => !externalOnly || r.Partner.Owner != c.Owner)
			.Select(r => r.Value)
			.OrderByDescending(v => v)
			.Take(cap)
			.Sum();

		// EconomicOutput's arithmetic with ScoringRoutes swapped for `cap`: sequential +50%
		// for Marketplace and Bank. Taxmen are omitted — they are the same for every cap and
		// only shift shares by a rounding step.
		// Which of a civ's routes SURVIVE a per-(civ,partner) cap: group every route the civ
		// holds by the partner city, keep the best `cap` by value. Returns the survivors keyed
		// by their home city, since the commerce multipliers are per city.
		private static System.Collections.Generic.Dictionary<City, int> SurvivorsPerPartner(
			Player p, int cap, bool externalOnly)
		{
			var all = p.Cities.Where(c => c.Size > 0)
				.SelectMany(c => c.TradeRoutes
					.Where(r => !externalOnly || r.Partner.Owner != c.Owner)
					.Select(r => (Home: c, r.Partner, r.Value)))
				.ToArray();
			var kept = all.GroupBy(x => x.Partner)
				.SelectMany(grp => grp.OrderByDescending(x => x.Value).Take(cap));
			var byHome = p.Cities.Where(c => c.Size > 0).ToDictionary(c => c, _ => 0);
			foreach (var x in kept) byHome[x.Home] += x.Value;
			return byHome;
		}

		// Diminishing returns: routes anchored on one partner are ranked by value and the nth
		// pays value/n. Returns survivors keyed by home city.
		private static System.Collections.Generic.Dictionary<City, int> DiminishedByHome(
			Player p, bool externalOnly)
		{
			var byHome = p.Cities.Where(c => c.Size > 0).ToDictionary(c => c, _ => 0);
			var all = p.Cities.Where(c => c.Size > 0)
				.SelectMany(c => c.TradeRoutes
					.Where(r => !externalOnly || r.Partner.Owner != c.Owner)
					.Select(r => (Home: c, r.Partner, r.Value)));
			foreach (var grp in all.GroupBy(x => x.Partner))
			{
				int n = 0;
				foreach (var x in grp.OrderByDescending(x => x.Value))
					byHome[x.Home] += x.Value / ++n;
			}
			return byHome;
		}

		private static int MoneyDiminishing(Player p)
		{
			var kept = DiminishedByHome(p, externalOnly: false);
			return p.Cities.Where(c => c.Size > 0).Sum(c => c.BaseTrade + kept[c]);
		}

		private static int OutputDiminishing(Player p)
		{
			var kept = DiminishedByHome(p, externalOnly: true);
			int total = 0;
			foreach (City c in p.Cities.Where(c => c.Size > 0))
			{
				int o = c.BaseTrade + kept[c];
				if (c.HasBuilding<Buildings.MarketPlace>()) o += o / 2;
				if (c.HasBuilding<Buildings.Bank>()) o += o / 2;
				total += o;
			}
			return total;
		}

		private static int MoneyPerPartner(Player p, int cap)
		{
			var kept = SurvivorsPerPartner(p, cap, externalOnly: false);
			return p.Cities.Where(c => c.Size > 0).Sum(c => c.BaseTrade + kept[c]);
		}

		private static int OutputPerPartner(Player p, int cap)
		{
			var kept = SurvivorsPerPartner(p, cap, externalOnly: true);
			int total = 0;
			foreach (City c in p.Cities.Where(c => c.Size > 0))
			{
				int o = c.BaseTrade + kept[c];
				if (c.HasBuilding<Buildings.MarketPlace>()) o += o / 2;
				if (c.HasBuilding<Buildings.Bank>()) o += o / 2;
				total += o;
			}
			return total;
		}

		private static int Output(Player p, int cap)
		{
			int total = 0;
			foreach (City c in p.Cities.Where(c => c.Size > 0))
			{
				int o = c.BaseTrade + CappedRoutes(c, cap, externalOnly: true);
				if (c.HasBuilding<Buildings.MarketPlace>()) o += o / 2;
				if (c.HasBuilding<Buildings.Bank>()) o += o / 2;
				total += o;
			}
			return total;
		}
	}
}
