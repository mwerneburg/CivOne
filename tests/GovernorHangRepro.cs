// CivOne diagnostic (not an assertion)
//
// A run hung at turn 487 (1937 AD) with the main thread at 100% and allocating — a spin, not
// a deadlock. This walks a finished save and runs the citizen governor over every city under
// a watchdog, naming the one that does not come back.
//
// Go to the save first. Three earlier hangs in this project were misdiagnosed from logs and
// only the save gave the truth.
//
//   CIVONE_ENDGAME_SAVE=~/Library/.../autosave.cos dotnet test --filter GovernorHang -l "console;verbosity=detailed"

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class GovernorHangRepro
	{
		private const int WatchdogMs = 4000;
		private readonly ITestOutputHelper _out;
		public GovernorHangRepro(ITestOutputHelper output) => _out = output;

		[Fact]
		public void FindTheCityThatNeverReturns()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;
			_out.WriteLine($"turn {g.GameTurn} ({Common.YearString(g.GameTurn)})");

			int checked_ = 0, stuck = 0;
			foreach (City c in g.GetCities().Where(c => c.Size > 0).ToArray())
			{
				Player p = g.GetPlayer(c.Owner);
				if (p is null || g.PlayerNumber(p) == 0) continue;
				checked_++;

				City city = c;
				var t = Task.Run(() => city.AutoAssignCitizens());
				if (t.Wait(WatchdogMs)) continue;

				stuck++;
				string path2 = p.IsHuman ? "HUMAN" : AI.Instance(p).Path.ToString();
				_out.WriteLine($"STUCK: {c.Name} ({p.TribeNamePlural}, {path2}) size {c.Size} "
					+ $"at ({c.X},{c.Y}) — worked {c.ResourceTiles.Count() - 1}, "
					+ $"food {c.FoodIncome}, disorder {c.IsInDisorder}, capped {c.GrowthBlocked}");
				if (stuck >= 5) break;   // five is a pattern; the rest is the same story
			}
			_out.WriteLine($"{checked_} cities checked, {stuck} stuck");
		}
	}
}
