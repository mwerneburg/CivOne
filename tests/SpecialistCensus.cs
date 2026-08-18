// CivOne diagnostic (not an assertion)
//
// Does the field actually USE its specialists?
//
// The Artist and the Taxman were added on the argument that a civilization on the Culture
// path should be able to buy culture with population, and one on Commerce should be able to
// buy output. Both are optional — the governor only reaches for a specialist when a citizen
// has nothing better to do — so the rule can be correct and the mechanic still be dead.
//
// The decision log records culture and output but never citizen ALLOCATION, so a finished
// save is the only place the answer exists. This counts every specialist in the world by
// type and by owner, alongside each civ's chosen victory path.
//
//   CIVONE_ENDGAME_SAVE=/path/to.cos dotnet test --filter SpecialistCensus -l "console;verbosity=detailed"

using System;
using System.Linq;
using CivOne.Enums;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class SpecialistCensus
	{
		private readonly ITestOutputHelper _out;
		public SpecialistCensus(ITestOutputHelper output) => _out = output;

		[Fact]
		public void CountTheSpecialists()
		{
			string? path = Environment.GetEnvironmentVariable("CIVONE_ENDGAME_SAVE");
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{ _out.WriteLine("set CIVONE_ENDGAME_SAVE to a .cos file — skipped"); return; }

			Sim.EnsureRuntime();
			Sim.ResetState();
			Assert.True(Game.LoadCos(path!), $"load failed: {path}");
			Game g = Game.Instance;

			_out.WriteLine($"{path}  turn {g.GameTurn} ({Common.YearString(g.GameTurn)})");
			_out.WriteLine("civ                path        cities  pop   ent  art  tax  sci   culture");

			int wArt = 0, wTax = 0, wSci = 0, wEnt = 0;
			foreach (Player p in g.Players.Where(q => q is not null && g.PlayerNumber(q) != 0
			                                       && !q.IsDestroyed()))
			{
				City[] cities = p.Cities.Where(c => c.Size > 0).ToArray();
				if (cities.Length == 0) continue;

				int Count(Citizen kind) => cities.Sum(c => c.Citizens.Count(z => z == kind));
				int ent = Count(Citizen.Entertainer), art = Count(Citizen.Artist);
				int tax = Count(Citizen.Taxman),      sci = Count(Citizen.Scientist);
				wEnt += ent; wArt += art; wTax += tax; wSci += sci;

				string p2 = p.IsHuman ? "HUMAN" : AI.Instance(p).Path.ToString();
				// What share of the culture INCOME the artists are actually responsible for —
				// the stock is decades of buildings, so only the rate says whether the
				// specialist is carrying anything.
				int rate = p.CultureRate;
				int fromArt = art * City.ArtistCulture;
				_out.WriteLine($"{p.TribeNamePlural,-18} {p2,-11} {cities.Length,5}  "
					+ $"{cities.Sum(c => (int)c.Size),4}  {ent,4} {art,4} {tax,4} {sci,4}  {p.Culture,8}"
					+ $"  rate {rate,5} ({(rate > 0 ? 100 * fromArt / rate : 0),3}% artists)");
			}
			_out.WriteLine($"{"WORLD",-18} {"",-11} {"",5}  {"",4}  {wEnt,4} {wArt,4} {wTax,4} {wSci,4}");

			// A city-level look at wherever the artists actually are — if the answer is "one
			// civ, three cities", the mechanic is not doing anything to the world.
			foreach (City c in g.GetCities().Where(c => c.Size > 0
				&& c.Citizens.Any(z => z == Citizen.Artist)).Take(25))
			{
				_out.WriteLine($"  artist city: {c.Name,-14} size {c.Size,2}  "
					+ $"{g.GetPlayer(c.Owner).TribeNamePlural}  "
					+ $"artists {c.Citizens.Count(z => z == Citizen.Artist)}");
			}
		}
	}
}
