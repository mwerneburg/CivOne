// CivOne diagnostic (not an assertion)
//
// How far is a culture supposed to carry?
//
// CulturalShadowRange is 5 tiles, inherited from Civ 1's 80x50 world where that is a
// sixteenth of the map's width. On the 320x200 epic map it is a sixty-fourth — the same
// class of unscaled absolute the warming rules had. Measured consequence: in a 13-civ epic
// game NO civilization ever had reach above 10, and in a 3-civ game reach was ZERO for all
// three civs across 560 turns. They never came within five tiles of each other, so the
// cultural path was shut before anyone made a decision.
//
// This sweeps the range over a finished save and prints what reach and shadow WOULD have
// been, so the constant can be set from evidence rather than from the original's map.
//
//   CIVONE_ENDGAME_SAVE=/path/to.cos dotnet test --filter CulturalRange -l "console;verbosity=detailed"

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class CulturalRangeDiagnostic
	{
		private readonly ITestOutputHelper _out;
		public CulturalRangeDiagnostic(ITestOutputHelper output) => _out = output;

		[Fact]
		public void SweepTheCulturalRange()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			Player[] live = g.Players
				.Where(p => p is not null && !p.IsDestroyed() && g.PlayerNumber(p) != 0)
				.ToArray();
			City[] cities = g.GetCities().Where(c => c.Size > 0).ToArray();

			// Same unwrapped coordinates as Game.CulturalReachAndShadow, so the range-5 column
			// reproduces what the live rule sees.
			double _ratio = Game.CultureShadowRatio;   // the shipping rule, not a hard-coded 3
			(int reach, int shadow) At(Player p, int range)
			{
				byte pn = g.PlayerNumber(p);
				var covered = new HashSet<(int, int)>();
				foreach (City c in cities.Where(c => c.Owner == pn))
					for (int dy = -range; dy <= range; dy++)
					for (int dx = -range; dx <= range; dx++)
						covered.Add((c.X + dx, c.Y + dy));

				int reach = 0, shadow = 0;
				foreach (City c in cities)
				{
					if (c.Owner == pn || c.Owner == 0) continue;
					Player owner = g.GetPlayer(c.Owner);
					if (owner.Civilization is Civilizations.Olvir or Civilizations.TheOthers
					                       or Civilizations.TheThing or Civilizations.Skynet) continue;
					if (!covered.Contains((c.X, c.Y))) continue;
					reach++;
					if (p.Culture > 0 && owner.Culture * _ratio < p.Culture) shadow++;
				}
				return (reach, shadow);
			}

			int[] ranges = { 5, 8, 10, 15, 20, 25 };

			_out.WriteLine($"turn {g.GameTurn}  map {Map.WIDTH}x{Map.HEIGHT}  "
			             + $"{live.Length} civs  {cities.Length} cities");
			_out.WriteLine("");
			_out.WriteLine("REACH (foreign cities in range)");
			_out.WriteLine($"{"civ",-14}" + string.Concat(ranges.Select(r => $"{"r" + r,7}")));
			foreach (Player p in live.OrderByDescending(p => p.Culture))
				_out.WriteLine($"{p.TribeNamePlural,-14}"
					+ string.Concat(ranges.Select(r => $"{At(p, r).reach,7}")));

			_out.WriteLine("");
			_out.WriteLine($"SHADOW (of those, owner under 1/{Game.CultureShadowRatio} your culture) — target is 3/5 of reach, floor 6");
			_out.WriteLine($"{"civ",-14}" + string.Concat(ranges.Select(r => $"{"r" + r,10}")));
			foreach (Player p in live.OrderByDescending(p => p.Culture))
			{
				string row = $"{p.TribeNamePlural,-14}";
				foreach (int r in ranges)
				{
					(int reach, int shadow) = At(p, r);
					int tgt = Game.CulturalShadowTarget(reach);
					row += $"{$"{shadow}/{tgt}" + (shadow >= tgt ? "*" : ""),10}";
				}
				_out.WriteLine(row);
			}

			_out.WriteLine("");
			_out.WriteLine("* = shadow clause met. The 2x-runner-up clause is separate and not shown.");

			// The headline: at what range does ANY civ clear the shadow clause?
			foreach (int r in ranges)
			{
				var met = live.Where(p => !(p.Civilization is Civilizations.Olvir or Civilizations.TheOthers
				                                          or Civilizations.TheThing or Civilizations.Skynet))
					.Where(p => { (int reach, int shadow) = At(p, r); return shadow >= Game.CulturalShadowTarget(reach); })
					.ToArray();
				_out.WriteLine($"  range {r,2}: {met.Length} civ(s) clear the shadow clause"
				             + (met.Length > 0 ? $" — {string.Join(", ", met.Select(p => p.TribeNamePlural))}" : ""));
			}

			// The other knob. Shadow needs the neighbour under 1/RATIO of your culture; the
			// rule ships 3. If the field is flat, no range makes that reachable — so sweep the
			// ratio too and print the best DOMINANCE FRACTION anyone achieves, which is the
			// number the target's 3/5 has to sit under.
			_out.WriteLine("");
			_out.WriteLine("best dominance fraction (shadow/reach) over all civs");
			_out.WriteLine($"{"ratio",-8}" + string.Concat(new[]{5,10,15,20}.Select(r => $"{"r"+r,12}")));
			foreach (double ratio in new[] { 3.0, 2.5, 2.0, 1.5, 1.25 })
			{
				_ratio = ratio;
				string row = $"1/{ratio,-6:0.##}";
				foreach (int r in new[]{5,10,15,20})
				{
					double best = 0; string who = "-";
					foreach (Player p in live)
					{
						if (p.Civilization is Civilizations.Olvir or Civilizations.TheOthers
						                   or Civilizations.TheThing or Civilizations.Skynet) continue;
						(int reach, int shadow) = At(p, r);
						if (reach >= 6 && (double)shadow / reach > best) { best = (double)shadow / reach; who = p.TribeNamePlural.Substring(0, 3); }
					}
					row += $"{$"{best*100:F0}% {who}",12}";
				}
				_out.WriteLine(row);
			}
			_ratio = Game.CultureShadowRatio;
		}
	}
}
