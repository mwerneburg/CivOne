// CivOne diagnostic (not an assertion)
//
// Who was in line for Cultural Ascendancy when the game ended, and what stopped them?
//
// The decision log carries culture and populace, so a post-hoc script can work out who LED
// the measure — but not why the coronation never came. Four other clauses gate it (Philosophy,
// three surviving rivals, the populace floor, a war of your own making) and none of them is
// logged. Measured need: two games in the 18 Aug batch showed a civ holding the per-head lead
// with the margin for 125 and 195 turns against a 100-turn requirement, and neither won.
//
// This evaluates every clause against a finished save and prints the streak the game itself
// was carrying, so the answer comes from the same state the rule read.
//
//   CIVONE_ENDGAME_SAVE=/path/to.cos dotnet test --filter CulturalClaim -l "console;verbosity=detailed"

using System;
using System.Linq;
using CivOne.Advances;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class VictoryClaimDiagnostic
	{
		private readonly ITestOutputHelper _out;
		public VictoryClaimDiagnostic(ITestOutputHelper output) => _out = output;

		[Fact]
		public void WhyNoCoronation()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			Player[] live = g.Players.Where(p => p is not null && !p.IsDestroyed() && g.PlayerNumber(p) != 0).ToArray();
			long Pop(Player p) => Math.Max(1, p.Cities.Sum(c => (int)c.Size));
			long floor = Game.CulturalPopulaceFloor(live.Select(Pop));

			_out.WriteLine($"{path}  turn {g.GameTurn} ({Common.YearString(g.GameTurn)})");
			_out.WriteLine("civ                per-head   pop  phil  pplous  foremost  war(started)  streak");

			foreach (Player p in live.OrderByDescending(p => (double)p.Culture / Pop(p)))
			{
				long pop = Pop(p);
				double per = (double)p.Culture / pop;
				bool populous = pop >= floor;
				bool foremost = p.Culture > 0 && live.Where(q => q != p && q.Cities.Any(c => c.Size > 0)).All(q =>
				{
					long rp = Pop(q);
					if (rp < floor) return true;
					return per >= (double)q.Culture / rp * Game.CultureLeadMargin;
				});

				// The same aggression test the rule uses, and the name of whoever it caught —
				// "at war" and "started it" are different findings and the fix differs.
				Player[] wars = live.Where(q => q != p && p.IsAtWar(q)).ToArray();
				string warNote = wars.Length == 0 ? "-"
					: string.Join(",", wars.Select(q => q.TribeName
						+ (StartedIt(g, p, q) ? "*" : "")));

				uint streak = Streak(g, p);
				_out.WriteLine($"{p.TribeNamePlural,-18} {per,8:F1} {pop,5}  "
					+ $"{(p.HasAdvance<Philosophy>() ? "yes" : "NO "),-4}  {(populous ? "yes" : "NO "),-6}  "
					+ $"{(foremost ? "yes" : "NO "),-8}  {warNote,-12}  {streak,6}");
			}
			_out.WriteLine("* = war of their own making (breaks the streak).  "
				+ $"floor {floor} pop (1/{Game.CultureFloorShare} of the median), margin {Game.CultureLeadMargin}x, "
				+ $"hold {Game.CultureHoldTurns}t from {Game.CultureGateYear} AD");
		}

		// The same question for Pax Mercatoria. Worth asking of the same saves: two games in
		// the 18 Aug batch put a civ over the 50% output bar (54.6% and 58.0%) and neither won
		// economically, so the share is no longer the only thing standing in the way.
		[Fact]
		public void WhyNoPaxMercatoria()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			Player[] live = g.Players.Where(p => p is not null && !p.IsDestroyed() && g.PlayerNumber(p) != 0).ToArray();
			int worldOut = live.Sum(g.GrossOutputOf);

			_out.WriteLine($"{path}  turn {g.GameTurn} ({Common.YearString(g.GameTurn)})  world output {worldOut}");
			_out.WriteLine("civ                 output  share  bank  war(started)   bound/needed  streak");

			foreach (Player p in live.OrderByDescending(g.GrossOutputOf))
			{
				int outp = g.GrossOutputOf(p);
				Player[] rivals = live.Where(q => q != p).ToArray();
				int bound = rivals.Count(r => r.PaysTributeTo(p) || r.HasDefensePact(p) || Trading(g, p, r));
				Player[] wars = live.Where(q => q != p && p.IsAtWar(q)).ToArray();
				string warNote = wars.Length == 0 ? "-"
					: string.Join(",", wars.Select(q => q.TribeName + (StartedIt(g, p, q) ? "*" : "")));

				_out.WriteLine($"{p.TribeNamePlural,-18} {outp,7}  {(worldOut > 0 ? 100.0 * outp / worldOut : 0),5:F1}%  "
					+ $"{(p.HasAdvance<Banking>() ? "yes" : "NO "),-4}  {warNote,-13}  "
					+ $"{bound,5}/{(rivals.Length + 1) / 2,-6}  {Streak(g, p, "EconStreak"),6}");
			}
			_out.WriteLine("* = war of their own making.  bound = rivals paying tribute, in a defense "
				+ "pact, or running a trade route with this civ; half of them are required.");
		}

		// The economic bond the rule counts: a live trade route either way.
		private static bool Trading(Game g, Player a, Player b)
		{
			byte an = g.PlayerNumber(a), bn = g.PlayerNumber(b);
			return g.GetCities().Any(c => c.Size > 0 &&
				((c.Owner == an && c.TradeRoutes.Any(t => t.Partner.Owner == bn))
				|| (c.Owner == bn && c.TradeRoutes.Any(t => t.Partner.Owner == an))));
		}

		private static bool StartedIt(Game g, Player a, Player b)
		{
			var m = typeof(Game).GetMethod("StartedWarWith",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (m is null) return false;
			return (bool)m.Invoke(g, new object[] { g.PlayerNumber(a), g.PlayerNumber(b) })!;
		}

		private static uint Streak(Game g, Player p, string field = "CultureStreak")
		{
			var m = typeof(Game).GetMethod("Progress",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			object prog = m!.Invoke(g, new object[] { g.PlayerNumber(p) })!;
			return (uint)prog.GetType().GetField(field)!.GetValue(prog)!;
		}
	}
}
