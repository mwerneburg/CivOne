// CivOne tests
//
// Loads a .cos save and prints who holds what: civ standings, the largest cities, where
// each faction's territory lies, and which wonders ended up where. Written to read the end
// state of a long autoplay game — the saves carry the whole story, but only as bytes.

using System;
using System.IO;
using System.Linq;
using CivOne;
using CivOne.Wonders;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class WorldReportProbe
	{
		private readonly ITestOutputHelper _out;
		public WorldReportProbe(ITestOutputHelper o) => _out = o;

		private void Report(string path)
		{
			_out.WriteLine("");
			_out.WriteLine("================ " + Path.GetFileName(path) + " ================");
			Sim.EnsureRuntime();
			Sim.ResetState();
			if (!Game.LoadCos(path)) { _out.WriteLine("FAILED TO LOAD"); return; }

			Game g = Game.Instance;
			_out.WriteLine($"turn {g.GameTurn}   year {g.GameYear}   human={g.HumanPlayer?.TribeName}");
			_out.WriteLine($"cities {g.GetCities().Length}   units {g.GetUnits().Length}");
			_out.WriteLine("");
			_out.WriteLine($"{"civ",-16}{"leader",-18}{"cities",7}{"units",7}{"score",7}{"gold",7}  {"gov",-14} at war with");

			foreach (Player p in g.Players.Where(x => x is not null))
			{
				if (p.Civilization is CivOne.Civilizations.Barbarian) continue;
				City[] cities = g.GetCities().Where(c => c.Owner == g.PlayerNumber(p)).ToArray();
				int units = g.GetUnits().Count(u => u.Owner == g.PlayerNumber(p));
				string wars = string.Join(",", g.Players.Where(q => q is not null && q != p
					&& !(q.Civilization is CivOne.Civilizations.Barbarian) && p.IsAtWar(q))
					.Select(q => q.TribeName));
				_out.WriteLine($"{p.Civilization.Name,-16}{p.LeaderName,-18}{cities.Length,7}{units,7}{p.Score,7}{p.Gold,7}  {p.Government.Name,-14} {wars}");
			}

			_out.WriteLine("");
			_out.WriteLine("-- largest cities --");
			foreach (City c in g.GetCities().OrderByDescending(c => c.Size).Take(12))
				_out.WriteLine($"  {c.Name,-16} size {c.Size,3}  {g.GetPlayer(c.Owner).TribeName,-14} wonders={c.Wonders.Length} buildings={c.Buildings.Length}");

			_out.WriteLine("");
			_out.WriteLine("-- English holdings --");
			foreach (City c in g.GetCities().Where(c => g.GetPlayer(c.Owner).TribeName == "English"))
				_out.WriteLine($"  {c.Name,-16} size {c.Size,3} at ({c.X},{c.Y}) buildings={c.Buildings.Length}");
			_out.WriteLine("");
			_out.WriteLine("-- The Thing / Other / Machine holdings (count by region) --");
			foreach (string t in new[]{"The Thing","Other","Machine"})
			{
				var cs = g.GetCities().Where(c => g.GetPlayer(c.Owner).TribeName == t).ToArray();
				if (cs.Length == 0) { _out.WriteLine($"  {t}: none"); continue; }
				_out.WriteLine($"  {t}: {cs.Length} cities, x {cs.Min(c=>c.X)}-{cs.Max(c=>c.X)}, y {cs.Min(c=>c.Y)}-{cs.Max(c=>c.Y)}");
			}

			_out.WriteLine("");
			_out.WriteLine("-- wonders built --");
			foreach (City c in g.GetCities().Where(c => c.Wonders.Length > 0).OrderBy(c => c.Name))
				_out.WriteLine($"  {c.Name,-16} ({g.GetPlayer(c.Owner).TribeName}): {string.Join(", ", c.Wonders.Select(w => w.Name))}");
		}

		// A reporting tool, not a test: dumps the state of the world from a save so a finished
		// game can be read without replaying it. Reads the player's own save directory, so it
		// stays off by default — remove -Skip and edit the paths below to run it.
		[Fact(Skip = "reporting tool; remove -Skip and set the paths to run")]
		public void Dump()
		{
			string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				"Library", "Application Support", "CivOne", "saves");
			Report(Path.Combine(dir, "c", "CIVIL1.cos"));
			Report(Path.Combine(dir, "c", "CIVIL2.cos"));
			Report(Path.Combine(dir, "autosave.cos"));
		}
	}
}
