// CivOne tests
//
// Diagnostic, not a regression test: load a real finished game and print where every
// civilization stands against every victory threshold. It answers a question the suite
// cannot — not "does the rule work" (CulturalAscendancyTests and EconomicHegemonyTests
// already pin the rules) but "does anybody ever get near it".
//
// Written because three logged 750-turn games all ended the same way: the 2200 AD
// backstop, victory "Endurance", nothing else ever firing. That is consistent with two
// very different worlds — thresholds nobody can reach, or thresholds nobody is allowed
// to reach — and the fix is opposite in each case. This tells them apart.
//
// Opt-in, and skips silently when the save is absent so it never breaks another machine:
//     CIVONE_ENDGAME_SAVE=~/Library/Application\ Support/CivOne/saves/endgame-analysis.cos \
//       dotnet test --filter "FullyQualifiedName~EndgameStandings" --logger "console;verbosity=detailed"

using System;
using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class EndgameStandingsDiagnostic
	{
		private readonly ITestOutputHelper _out;
		public EndgameStandingsDiagnostic(ITestOutputHelper output) => _out = output;

		private static string? SavePath()
		{
			string? env = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (!string.IsNullOrWhiteSpace(env))
			{
				if (env.StartsWith("~"))
					env = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + env.Substring(1);
				return System.IO.File.Exists(env) ? env : null;
			}
			string mac = System.IO.Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				"Library", "Application Support", "CivOne", "saves", "endgame-analysis.cos");
			return System.IO.File.Exists(mac) ? mac : null;
		}

		[Trait("Category", "Diagnostic")]
		[Fact]
		public void WhereEverybodyStandsOnEveryVictoryPath()
		{
			string? path = SavePath();
			if (path is null) { _out.WriteLine("no endgame save present — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), $"load failed: {path}");
			Game g = Game.Instance;

			Player[] live = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && !p.IsDestroyed())
				.ToArray();

			int worldOut = live.Sum(g.GrossOutputOf);
			int bestCulture = live.Max(p => p.Culture);

			_out.WriteLine($"save: {path}");
			_out.WriteLine($"turn {g.GameTurn}  year {Common.YearString((ushort)g.GameTurn)}  "
			             + $"SETI={g.SETISignalReceived}  visitor={g.VisitorType}  colonyFounded={g.ColonyFounded}");
			_out.WriteLine($"world gross output {worldOut}   cultural shadow target {g.CulturalShadowTarget}");
			_out.WriteLine("");
			_out.WriteLine($"{"civ",-14} {"cit",4} {"score",6} {"output",7} {"out%",5} "
			             + $"{"culture",8} {"cult x",7} {"shadow",6} {"SS s/c/m",9} {"bank",4} {"phil",4}");

			foreach (Player p in live.OrderByDescending(p => p.Score))
			{
				byte n = g.PlayerNumber(p);
				int outp = g.GrossOutputOf(p);
				int rivalBest = live.Where(q => q != p).Select(q => q.Culture).DefaultIfEmpty(0).Max();
				double cultMultiple = rivalBest > 0 ? (double)p.Culture / rivalBest : (p.Culture > 0 ? 99 : 0);

				_out.WriteLine($"{p.TribeNamePlural,-14} {p.Cities.Length,4} {p.Score,6} {outp,7} "
				             + $"{(worldOut > 0 ? outp * 100.0 / worldOut : 0),5:F1} "
				             + $"{p.Culture,8} {cultMultiple,7:F2} {g.CulturalShadow(p),6} "
				             + $"{g.SpaceshipStructural[n],3}/{g.SpaceshipComponent[n],2}/{g.SpaceshipModule[n],2} "
				             + $"{(p.HasAdvance<Banking>() ? "yes" : "-"),4} {(p.HasAdvance<Philosophy>() ? "yes" : "-"),4}");
			}

			// The four questions the design turns on, answered against this world.
			_out.WriteLine("");
			Player econLeader = live.OrderByDescending(g.GrossOutputOf).First();
			Player cultLeader = live.OrderByDescending(p => p.Culture).First();
			_out.WriteLine($"ECONOMIC : leader {econLeader.TribeNamePlural} holds "
			             + $"{g.GrossOutputOf(econLeader) * 100.0 / Math.Max(1, worldOut):F1}% of world output (needs >50%)");
			_out.WriteLine($"CULTURAL : leader {cultLeader.TribeNamePlural} has shadow {g.CulturalShadow(cultLeader)} "
			             + $"(needs {g.CulturalShadowTarget}) at {(live.Where(q => q != cultLeader).Max(q => q.Culture) is int r && r > 0 ? (double)cultLeader.Culture / r : 99):F2}x runner-up (needs 2x)");
			int launched = live.Count(p => g.SpaceshipLaunchTurn[g.PlayerNumber(p)] != 0);
			int withParts = live.Count(p => g.SpaceshipStructural[g.PlayerNumber(p)]
			                              + g.SpaceshipComponent[g.PlayerNumber(p)]
			                              + g.SpaceshipModule[g.PlayerNumber(p)] > 0);
			_out.WriteLine($"SPACE    : {withParts} civs hold parts, {launched} launched");
			foreach (Player p in live.Where(p => g.SpaceshipLaunchTurn[g.PlayerNumber(p)] != 0)
			                         .OrderBy(p => g.SpaceshipLaunchTurn[g.PlayerNumber(p)]))
			{
				byte n = g.PlayerNumber(p);
				bool mc = p.Cities.Any(c => c.Size > 0 && c.HasBuilding<Buildings.MissionControl>());
				_out.WriteLine($"           {p.TribeNamePlural,-14} launched turn {g.SpaceshipLaunchTurn[n],4} "
				             + $"({Common.YearString((ushort)g.SpaceshipLaunchTurn[n])})  "
				             + $"missionControl={(mc ? "YES" : "no")}");
			}
			int mcHolders = live.Count(p => p.Cities.Any(c => c.Size > 0 && c.HasBuilding<Buildings.MissionControl>()));
			_out.WriteLine($"           {mcHolders} civs hold Mission Control world-wide");
			_out.WriteLine($"MILITARY : largest empire {live.Max(p => p.Cities.Length)} cities of "
			             + $"{live.Sum(p => p.Cities.Length)} world-wide ({live.Length} civs alive — conquest needs all but one dead)");

			// ── candidate thresholds ─────────────────────────────────────────────
			// The point of the exercise: which proposed bar does this world clear? A bar
			// nobody approaches is decoration; a bar the leader strolls past is not a race.
			// "Just short" is the target — a civ that pushed would take it.
			// Why is the AI economy flat? A human reaches ~59% of world output on this same
			// map; the best AI manages 25% at a transient peak. The candidates are structural
			// and all visible here: government (Republic/Democracy add trade per tile, so a
			// civ stuck in Monarchy is capped no matter how well it plays), trade routes,
			// and the multiplier buildings.
			_out.WriteLine("");
			_out.WriteLine($"{"civ",-14} {"government",-12} {"cities",6} {"routes",6} {"mkt%",5} {"bank%",5} "
			             + $"{"harb%",5} {"avgSize",7} {"out",6}");
			foreach (Player p in live.OrderByDescending(g.GrossOutputOf))
			{
				City[] cs = p.Cities.Where(c => c.Size > 0).ToArray();
				if (cs.Length == 0) continue;
				int routes = cs.Sum(c => c.TradeRoutes.Count());
				int mkt = cs.Count(c => c.HasBuilding<Buildings.MarketPlace>());
				int bank = cs.Count(c => c.HasBuilding<Buildings.Bank>());
				int harb = cs.Count(c => c.HasBuilding<Buildings.Harbour>());
				_out.WriteLine($"{p.TribeNamePlural,-14} {p.Government?.Name ?? "?",-12} {cs.Length,6} {routes,6} "
				             + $"{mkt * 100 / cs.Length,5} {bank * 100 / cs.Length,5} {harb * 100 / cs.Length,5} "
				             + $"{cs.Average(c => (double)c.Size),7:F1} {g.GrossOutputOf(p),6}");
			}

			// Growth caps: a city is stuck at 7 without an Aqueduct and at 12 without a Sewer
			// System (City.cs:311). If the world's cities pile up ON those two numbers, the flat
			// AI economy is a growth-infrastructure failure rather than a trade one — output is
			// worked tiles, and a city frozen at 7 works seven of them forever.
			_out.WriteLine("");
			City[] all = live.SelectMany(p => p.Cities).Where(c => c.Size > 0).ToArray();
			int atAq = all.Count(c => c.Size == 7 && !c.HasBuilding<Buildings.Aqueduct>());
			int atSw = all.Count(c => c.Size == 12 && !c.HasBuilding<Buildings.SewerSystem>());
			_out.WriteLine($"world cities {all.Length}: "
			             + $"aqueduct {all.Count(c => c.HasBuilding<Buildings.Aqueduct>()) * 100 / all.Length}%, "
			             + $"sewer {all.Count(c => c.HasBuilding<Buildings.SewerSystem>()) * 100 / all.Length}%, "
			             + $"bank {all.Count(c => c.HasBuilding<Buildings.Bank>()) * 100 / all.Length}%");
			_out.WriteLine($"  frozen AT a cap: {atAq} stuck at 7 (no aqueduct), {atSw} stuck at 12 (no sewer) "
			             + $"= {(atAq + atSw) * 100 / all.Length}% of all cities");
			var hist = new System.Text.StringBuilder("  size histogram: ");
			for (int lo = 1; lo <= 19; lo += 2)
				hist.Append($"{lo}-{lo + 1}:{all.Count(c => c.Size == lo || c.Size == lo + 1)} ");
			hist.Append($"20+:{all.Count(c => c.Size >= 20)}");
			_out.WriteLine(hist.ToString());

			// What the per-turn standings log actually costs on a full late-game world. This
			// game has been bitten twice by per-turn-times-per-civ work, so the number gets
			// measured rather than assumed.
			var sw = System.Diagnostics.Stopwatch.StartNew();
			const int reps = 20;
			for (int i = 0; i < reps; i++)
				foreach (Player p in live) { (int rr, int ss) = g.CulturalReachAndShadow(p); _ = rr + ss; }
			sw.Stop();
			_out.WriteLine("");
			_out.WriteLine($"standings cost: {sw.Elapsed.TotalMilliseconds / reps:F2} ms for all {live.Length} civs "
			             + $"(sampled every {Game.VictoryStandingsInterval} turns => "
			             + $"{sw.Elapsed.TotalMilliseconds / reps / Game.VictoryStandingsInterval:F2} ms/turn amortised)");

			_out.WriteLine("");
			_out.WriteLine("── candidate thresholds against this world ──");

			int worldCities = live.Sum(p => p.Cities.Length);
			long worldPop = live.Sum(p => (long)p.Population);

			void Verdict(string name, string test, bool met, string actual)
				=> _out.WriteLine($"  {(met ? "MET    " : "not met"),-7} {name,-26} {test,-34} {actual}");

			Player econ1 = live.OrderByDescending(g.GrossOutputOf).First();
			Player econ2 = live.Where(p => p != econ1).OrderByDescending(g.GrossOutputOf).First();
			double econLead = (double)g.GrossOutputOf(econ1) / Math.Max(1, g.GrossOutputOf(econ2));
			double econShare = g.GrossOutputOf(econ1) * 100.0 / Math.Max(1, worldOut);
			Verdict("ECON current", ">50% world output", econShare > 50, $"{econ1.TribeNamePlural} {econShare:F1}%");
			Verdict("ECON 2.0x + 25% floor", "2x runner-up AND >=25% world", econLead >= 2.0 && econShare >= 25,
				$"{econ1.TribeNamePlural} {econLead:F2}x, {econShare:F1}%");
			Verdict("ECON 1.75x + 20% floor", "1.75x runner-up AND >=20% world", econLead >= 1.75 && econShare >= 20,
				$"{econ1.TribeNamePlural} {econLead:F2}x, {econShare:F1}%");

			Player cult1 = live.OrderByDescending(p => p.Culture).First();
			Player cult2 = live.Where(p => p != cult1).OrderByDescending(p => p.Culture).First();
			double cultLead = (double)cult1.Culture / Math.Max(1, cult2.Culture);
			int cultShadow = g.CulturalShadow(cult1);
			Verdict("CULT current", "2x runner-up AND shadow >= target", cultLead >= 2.0 && cultShadow >= g.CulturalShadowTarget,
				$"{cult1.TribeNamePlural} {cultLead:F2}x, shadow {cultShadow}/{g.CulturalShadowTarget}");
			Verdict("CULT 1.5x + shadow", "1.5x runner-up AND shadow >= target", cultLead >= 1.5 && cultShadow >= g.CulturalShadowTarget,
				$"{cult1.TribeNamePlural} {cultLead:F2}x, shadow {cultShadow}/{g.CulturalShadowTarget}");

			// Decompose the shadow into its two ingredients, because they respond to civ count
			// in OPPOSITE directions and the headline number hides that.
			//
			//   reach     — foreign cities near enough to be shadowed at all
			//   dominance — of those, how many have an owner under 1/3 your culture
			//
			// Fewer, larger civilizations means MORE reach (neighbours are big and close) but
			// far LESS dominance (a big rival rarely holds under a third of your culture). So
			// a count target tuned on a 16-civ world does not simply scale down; the binding
			// clause changes identity.
			//
			// Mirrors Game.CulturalShadow's own unwrapped coordinates on purpose, so the two
			// numbers are comparable. That means it inherits the same seam blindness — see the
			// note printed below.
			int ReachField(Player p)
			{
				byte pn = g.PlayerNumber(p);
				var covered = new HashSet<(int, int)>();
				foreach (City c in g.GetCities().Where(c => c.Owner == pn && c.Size > 0))
					for (int dy = -Game.CulturalShadowRange; dy <= Game.CulturalShadowRange; dy++)
					for (int dx = -Game.CulturalShadowRange; dx <= Game.CulturalShadowRange; dx++)
						covered.Add((c.X + dx, c.Y + dy));
				return g.GetCities().Count(c => c.Size > 0 && c.Owner != pn && c.Owner != 0
				                             && covered.Contains((c.X, c.Y)));
			}

			_out.WriteLine("");
			_out.WriteLine($"{"civ",-14} {"cult/city",9} {"reach",6} {"shadow",6} {"dominated",9}");
			foreach (Player p in live.OrderByDescending(p => p.Cities.Length > 0 ? p.Culture / p.Cities.Length : 0))
			{
				int reach = ReachField(p);
				int shad = g.CulturalShadow(p);
				_out.WriteLine($"{p.TribeNamePlural,-14} {(p.Cities.Length > 0 ? p.Culture / p.Cities.Length : 0),9} "
				             + $"{reach,6} {shad,6} {(reach > 0 ? $"{shad * 100.0 / reach:F0}%" : "-"),9}");
			}
			_out.WriteLine("");

			// Does the cultural rule measure dominance twice? A city only enters the shadow if
			// its owner holds under a third of your culture, so the shadow count IS a dominance
			// test. The separate "2x the best rival" clause is a second, stricter one stacked on
			// top — and it is the one that binds.
			Verdict("CULT shadow only", "shadow >= target, no 2x clause", cultShadow >= g.CulturalShadowTarget,
				$"{cult1.TribeNamePlural} shadow {cultShadow}/{g.CulturalShadowTarget}");

			// Culture as an INTENSITY rather than a total, which is the obvious answer to
			// "a cultural victory scored on totals is a biggest-empire victory". The question
			// is whether it actually changes who wins, or whether the reach clause still
			// decides — an intense little civ with no neighbours in range shadows nobody.
			int PerCity(Player p) => p.Cities.Length > 0 ? p.Culture / p.Cities.Length : 0;
			Player dense1 = live.OrderByDescending(PerCity).First();
			Player dense2 = live.Where(p => p != dense1).OrderByDescending(PerCity).First();
			double denseLead = PerCity(dense2) > 0 ? (double)PerCity(dense1) / PerCity(dense2) : 99;
			Verdict("CULT per-city + shadow", "densest culture AND shadow >= target",
				g.CulturalShadow(dense1) >= g.CulturalShadowTarget,
				$"{dense1.TribeNamePlural} {PerCity(dense1)}/city ({denseLead:F2}x), shadow {g.CulturalShadow(dense1)}/{g.CulturalShadowTarget}");

			// And the hybrid: totals still decide the shadow (so defection and the victory keep
			// measuring the same thing), but the winner must ALSO be culturally dense — which
			// filters out "won by being enormous" without decoupling the two mechanics.
			int medianDense = live.Select(PerCity).OrderBy(v => v).ElementAt(live.Length / 2);
			Verdict("CULT shadow + density floor", "shadow >= target AND >=1.25x median density",
				cultShadow >= g.CulturalShadowTarget && PerCity(cult1) >= medianDense * 1.25,
				$"{cult1.TribeNamePlural} shadow {cultShadow}/{g.CulturalShadowTarget}, "
				+ $"{PerCity(cult1)}/city vs median {medianDense}");

			// The civ-count-robust form. Express the win as a SHARE of the reachable field
			// rather than an absolute count: dominate most of the foreign cities you can
			// actually touch, and touch enough of them to matter. Both numerator and
			// denominator move together when the number of civilizations changes, which is
			// exactly what a fixed count target cannot do.
			int r1 = ReachField(cult1);
			double reachShare = r1 > 0 ? cultShadow * 100.0 / r1 : 0;
			Player bestByShare = live.Where(p => ReachField(p) >= 20)
			                         .OrderByDescending(p => g.CulturalShadow(p) * 1.0 / Math.Max(1, ReachField(p)))
			                         .FirstOrDefault() ?? cult1;
			int rb = ReachField(bestByShare);
			Verdict("CULT 75% of reach, floor 20", ">=75% of reachable foreign cities",
				rb >= 20 && g.CulturalShadow(bestByShare) * 100.0 / Math.Max(1, rb) >= 75,
				$"{bestByShare.TribeNamePlural} {g.CulturalShadow(bestByShare)}/{rb} = "
				+ $"{g.CulturalShadow(bestByShare) * 100.0 / Math.Max(1, rb):F0}%");

			// The same shape applied to money: how many rivals are economically negligible
			// beside the leader (under a third of its output), rather than what slice of a
			// 16-way world it holds.
			int econShadow = live.Count(p => p != econ1 && g.GrossOutputOf(p) * 3 < g.GrossOutputOf(econ1));
			int econShadowTarget = (int)Math.Ceiling((live.Length - 1) * 2.0 / 3.0);
			Verdict("ECON shadow (2/3 rivals)", "2/3 of rivals under 1/3 your output", econShadow >= econShadowTarget,
				$"{econ1.TribeNamePlural} dominates {econShadow}/{live.Length - 1} rivals (needs {econShadowTarget})");

			Player mil1 = live.OrderByDescending(p => p.Cities.Length).First();
			double cityShare = mil1.Cities.Length * 100.0 / Math.Max(1, worldCities);
			double popShare  = mil1.Population * 100.0 / Math.Max(1, worldPop);
			Verdict("MIL current", "every rival destroyed", live.Length == 1, $"{live.Length} civs alive");
			Verdict("MIL 40% cities + 40% pop", ">=40% cities AND >=40% pop", cityShare >= 40 && popShare >= 40,
				$"{mil1.TribeNamePlural} {cityShare:F1}% cities, {popShare:F1}% pop");
			Verdict("MIL 33% cities", ">=33% of world cities", cityShare >= 33,
				$"{mil1.TribeNamePlural} {cityShare:F1}% cities");

			// Hegemony without genocide: rivals who pay you tribute have conceded, and the
			// tribute machinery already exists. A military win that does not require killing
			// fifteen civilizations is the only kind this world can actually produce.
			Player hegemon = live.OrderByDescending(p => p.TributePayers.Count(q => !q.IsDestroyed())
			                                           + p.Cities.Length / 1000.0).First();
			int payers = hegemon.TributePayers.Count(q => !q.IsDestroyed());
			int subjugationTarget = (int)Math.Ceiling((live.Length - 1) / 2.0);
			Verdict("MIL tribute hegemony", "half of rivals pay you tribute", payers >= subjugationTarget,
				$"{hegemon.TribeNamePlural} has {payers}/{live.Length - 1} tributaries (needs {subjugationTarget})");

			int warPairs = 0;
			for (int i = 0; i < live.Length; i++)
			for (int j = i + 1; j < live.Length; j++)
				if (live[i].IsAtWar(live[j])) warPairs++;
			int totalTributaries = live.Sum(p => p.TributePayers.Count(q => !q.IsDestroyed()));
			_out.WriteLine($"  world state: {warPairs} war pairs, {totalTributaries} tribute relationships world-wide");

			// Earliest launcher, not any launcher: the race is decided by whoever gets there
			// first, and reporting a straggler makes the science path look far later than it is.
			Player? sci = live.Where(p => g.SpaceshipLaunchTurn[g.PlayerNumber(p)] != 0
			                           && p.Cities.Any(c => c.Size > 0 && c.HasBuilding<Buildings.MissionControl>()))
			                  .OrderBy(p => g.SpaceshipLaunchTurn[g.PlayerNumber(p)])
			                  .FirstOrDefault();
			Verdict("SCI current (AI-wired)", "launch + Mission Control 20t", sci is not null,
				sci is null ? "nobody" : $"{sci.TribeNamePlural} launched turn {g.SpaceshipLaunchTurn[g.PlayerNumber(sci)]}"
				            + $" -> would win ~turn {g.SpaceshipLaunchTurn[g.PlayerNumber(sci)] + 6 + 20}");
		}
	}
}
