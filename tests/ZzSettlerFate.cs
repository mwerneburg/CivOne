using System.Collections.Generic;
using System.Linq;
using CivOne.Tiles;
using CivOne.Units;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class ZzSettlerFate
	{
		private readonly ITestOutputHelper _o;
		public ZzSettlerFate(ITestOutputHelper o) { _o = o; }

		[Fact]
		public void Measure()
		{
			Sim.EnsureRuntime(); Sim.ResetState();
			Assert.True(Game.LoadCos("/private/tmp/claude-501/-Users-michael-scratch-CivOne/f6dd179d-1eff-48a4-85eb-b56ea60d85c2/scratchpad/live2.cos"));
			Settings.Instance.Autopilot = true;
			Common.SetRandomSeed(short.Parse(System.Environment.GetEnvironmentVariable("SEED") ?? "1"));
			Game g = Game.Instance;
			var last = new Dictionary<Settlers, (int x, int y, byte o, bool ai, bool ac)>();
			var sizes = new Dictionary<City, int>();
			var cityAt = new HashSet<(int, int)>();
			var fate = new Dictionary<(byte, string), int>();
			var born = new Dictionary<byte, int>();
			void Snap()
			{
				last.Clear(); sizes.Clear(); cityAt.Clear();
				foreach (Settlers s in g.GetUnits().OfType<Settlers>()) last[s] = (s.X, s.Y, s.Owner, s.AutoImprove, s.AutoClean);
				foreach (City c in g.GetCities()) { sizes[c] = c.Size; cityAt.Add((c.X, c.Y)); }
			}
			Snap();
			int turns = int.TryParse(System.Environment.GetEnvironmentVariable("TURNS"), out int t) ? t : 15;
			Sim.RunTurns(turns, _ =>
			{
				var now = g.GetUnits().OfType<Settlers>().ToHashSet();
				foreach (var s in now.Where(s => !last.ContainsKey(s))) born[s.Owner] = born.GetValueOrDefault(s.Owner) + 1;
				foreach (var kv in last.Where(kv => !now.Contains(kv.Key)))
				{
					var (x, y, o, _, _) = kv.Value;
					City? c = g.GetCities().FirstOrDefault(c => System.Math.Abs(c.X - x) <= 1 && System.Math.Abs(c.Y - y) <= 1);
					string f = c is null ? "died" : !cityAt.Contains((c.X, c.Y)) ? "founded" : c.Size > sizes.GetValueOrDefault(c) ? "joined" : "gone@city";
					fate[(o, f)] = fate.GetValueOrDefault((o, f)) + 1;
				}
				Snap();
			});
			foreach (Player p in g.Players.Where(p => p is not null && !p.IsDestroyed() && p.Cities.Any()).OrderByDescending(p => p.Cities.Length))
			{
				byte n = g.PlayerNumber(p);
				string fs = string.Join(" ", fate.Where(kv => kv.Key.Item1 == n).Select(kv => $"{kv.Key.Item2}:{kv.Value}"));
				int live = g.GetUnits().Count(u => u.Owner == n && u is Settlers);
				var land = p.Cities.SelectMany(c => c.ResourceTiles.Where(t => t.City is null && !t.IsOcean)).ToArray();
				var rad = p.Cities.SelectMany(c => c.CityRadius.Cast<ITile>()).Where(t => t is not null && t.City is null).Distinct().ToArray();
				int forest = rad.Count(t => t is CivOne.Tiles.Forest), wForest = land.Count(t => t is CivOne.Tiles.Forest);
				int irr = land.Count(t => t.Irrigation), rd = land.Count(t => t.Road || t.RailRoad);
				_o.WriteLine($"FATE {p.TribeNamePlural,-12} cities={p.Cities.Length,3} pop={p.Cities.Sum(c => c.Size),4} food={p.Cities.Sum(c => c.FoodIncome),4} shields={p.Cities.Sum(c => c.ShieldIncome),5} irr={100.0*irr/System.Math.Max(1,land.Length),3:F0}% road={100.0*rd/System.Math.Max(1,land.Length),3:F0}% forest={forest} workedForest={wForest} built={born.GetValueOrDefault(n),3} liveNow={live,3} | {fs}");
			}
			_o.WriteLine($"FATE turn={g.GameTurn}");
		}
	}
}
