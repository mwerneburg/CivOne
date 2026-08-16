// CivOne diagnostic (not an assertion)
//
// Which city loops, and what state is it in? The turn-328 hang is inside
// AI.ConsiderCitizens -> City.AutoAssignCitizens for one of the Khmer's cities. Rather than
// guess at the invariant, load the save and try each city under a watchdog.
//
//   CIVONE_HANG_SAVE=/path/to.cos dotnet test --filter CitizenGovernorProbe -l "console;verbosity=detailed"

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CivOne.Tiles;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class CitizenGovernorProbe
	{
		private readonly ITestOutputHelper _out;
		public CitizenGovernorProbe(ITestOutputHelper output) => _out = output;

		private static T Priv<T>(City c, string name) => (T)typeof(City)
			.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(c)!;

		[Fact]
		public void FindTheLoopingCity()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_HANG_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_HANG_SAVE — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			foreach (City c in g.GetCities().Where(c => c.Size > 0).ToArray())
			{
				var raw = Priv<System.Collections.Generic.IList<ITile>>(c, "_resourceTiles");
				var spec = (System.Collections.IList)Priv<object>(c, "_specialists");
				int filtered = c.ResourceTiles.Count();

				// Only the suspicious ones: a specialist present while the raw list already
				// reads full is the shape that cannot make progress.
				bool suspicious = spec.Count > 0 && raw.Count >= c.Size;
				if (!suspicious) continue;

				_out.WriteLine($"{c.Player.TribeNamePlural,-12} {c.Name,-14} size={c.Size} "
				             + $"raw={raw.Count} filtered={filtered} specialists={spec.Count} "
				             + $"disorder={c.IsInDisorder} growthBlocked={c.GrowthBlocked} food={c.FoodIncome}");

				bool done = Task.Run(() => c.AutoAssignCitizens()).Wait(4000);
				_out.WriteLine($"    AutoAssignCitizens -> {(done ? "returned" : "*** LOOPS ***")}");
				if (!done) { _out.WriteLine("    (leaving it here; the process must be killed)"); return; }
			}
			_out.WriteLine("no suspicious city looped — the shape is something else");

			// Second pass: try every city, not just the suspicious ones.
			foreach (City c in g.GetCities().Where(c => c.Size > 0).ToArray())
			{
				bool done = Task.Run(() => c.AutoAssignCitizens()).Wait(4000);
				if (done) continue;
				var raw = Priv<System.Collections.Generic.IList<ITile>>(c, "_resourceTiles");
				var spec = (System.Collections.IList)Priv<object>(c, "_specialists");
				_out.WriteLine($"*** LOOPS: {c.Player.TribeNamePlural} {c.Name} size={c.Size} "
				             + $"raw={raw.Count} filtered={c.ResourceTiles.Count()} specialists={spec.Count} "
				             + $"disorder={c.IsInDisorder} growthBlocked={c.GrowthBlocked} food={c.FoodIncome}");

				// Step the loop body by hand and print what does NOT change.
				var best = typeof(City).GetMethod("BestIdleTile",
					BindingFlags.NonPublic | BindingFlags.Instance)!;
				_out.WriteLine("    iter  specialists  raw  filtered  idleTile        disorder growthBlk food");
				for (int i = 0; i < 12; i++)
				{
					ITile? idle = (ITile?)best.Invoke(c, null);
					_out.WriteLine($"    {i,4}  {spec.Count,11}  {raw.Count,3}  {c.ResourceTiles.Count(),8}  "
					             + $"{(idle is null ? "null" : $"{idle.X},{idle.Y}"),-14}  "
					             + $"{c.IsInDisorder,8} {c.GrowthBlocked,9} {c.FoodIncome,4}");
					if (idle is null) break;
					c.SetResourceTile(idle);
				}
				return;
			}
			_out.WriteLine("no city looped at all on a fresh load");

			// Confirm the MECHANISM rather than inferring it: is any civ blind to its own
			// city centre? That is what drops the centre from CityTiles and splits the two
			// counts. A civ that cannot see its own city is an anomaly in its own right.
			foreach (City c in g.GetCities().Where(c => c.Size > 0).ToArray())
			{
				if (c.Player.Visible(c.X, c.Y)) continue;
				var raw = Priv<System.Collections.Generic.IList<ITile>>(c, "_resourceTiles");
				_out.WriteLine($"BLIND TO OWN CENTRE: {c.Player.TribeNamePlural} {c.Name} "
				             + $"@{c.X},{c.Y} size={c.Size} raw={raw.Count} filtered={c.ResourceTiles.Count()}");
			}
		}
	}
}
