// TEMPORARY probe (2026-08-03) — not a regression test. Splits the city:Income bucket
// (ShieldIncome / FoodIncome / Citizens) at a realistic late-game city count so the
// 6.8ms-per-city figure can be attributed instead of guessed at. Delete when done.

using System;
using System.Diagnostics;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class IncomeCostProbe
	{
		private readonly ITestOutputHelper _out;
		public IncomeCostProbe(ITestOutputHelper output) => _out = output;

		[Fact(Skip = "probe; remove -Skip to run")]
		public void SplitTheIncomeBucket()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player[] ps = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer)
				.ToArray();

			// ~440 cities spread over the players, as in the 2196 AD run.
			int made = 0;
			for (int y = 2; y < 48 && made < 440; y += 2)
			for (int x = 2; x < 78 && made < 440; x += 2)
			{
				Player p = ps[made % ps.Length];
				p.Explore(x, y, range: 3);
				City? c = g.AddCity(p, (byte)(made % 250), x, y);
				if (c is null) continue;
				c.Size = 6;
				made++;
			}
			_out.WriteLine($"cities: {g.GetCities().Length}");

			City[] cities = g.GetCities();
			foreach (City c in cities) c.InvalidateCache();

			var sw = new Stopwatch();
			double shield = 0, food = 0, citizens = 0, playerCities = 0, hasWonder = 0;

			foreach (City c in cities)
			{
				c.InvalidateCache();
				sw.Restart(); _ = c.ShieldIncome;      shield   += sw.Elapsed.TotalMilliseconds;
				sw.Restart(); _ = c.FoodIncome;        food     += sw.Elapsed.TotalMilliseconds;
				sw.Restart(); _ = c.Citizens.ToArray(); citizens += sw.Elapsed.TotalMilliseconds;
			}

			double smoke = 0, tileCity = 0, radius = 0;
			foreach (City c in cities)
			{
				sw.Restart(); _ = c.Player.Cities.Length;                    playerCities += sw.Elapsed.TotalMilliseconds;
				sw.Restart(); _ = c.Player.HasWonder<Wonders.HangingGardens>(); hasWonder += sw.Elapsed.TotalMilliseconds;
				c.InvalidateCache();
				sw.Restart(); _ = c.SmokeStacks;                             smoke    += sw.Elapsed.TotalMilliseconds;
				sw.Restart(); _ = Map.Instance[c.X, c.Y].City;               tileCity += sw.Elapsed.TotalMilliseconds;
				sw.Restart(); _ = c.CityRadius;                              radius   += sw.Elapsed.TotalMilliseconds;
			}
			_out.WriteLine($"  CityRadius    {radius / cities.Length:F3} ms/call");
			_out.WriteLine($"  SmokeStacks   {smoke / cities.Length:F3} ms/call");
			_out.WriteLine($"  Tile.City     {tileCity / cities.Length:F3} ms/call");

			int n = cities.Length;
			_out.WriteLine($"ShieldIncome    {shield / n:F3} ms/city");
			_out.WriteLine($"FoodIncome      {food / n:F3} ms/city");
			_out.WriteLine($"Citizens        {citizens / n:F3} ms/city");
			_out.WriteLine($"  Player.Cities {playerCities / n:F3} ms/call");
			_out.WriteLine($"  HasWonder     {hasWonder / n:F3} ms/call");
			_out.WriteLine($"TOTAL           {(shield + food + citizens) / n:F3} ms/city");
		}
	}
}
