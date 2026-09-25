// CivOne tests
//
// Every line on the culture-per-head graph dipped at its live end, and the dip "healed" a
// turn later (reported Sep 2026). The history and the live end averaged different worlds:
// the history counted barbarian towns and the lingering peaks of dead civs — both small,
// so a smaller divisor and higher past points. Game.CountsInCulturalAverage is now the one
// set for the rule, the history and the live end.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CultureGraphLiveEndTests
	{
		[Fact]
		public void TheNewestHistoryPointIsTheLiveValue()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 15; x <= 65; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			g.GameTurn = Sim.TurnPastCultureGate();   // after 1 AD: no respawn to muddy the slots

			Player[] civs = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).Take(4).ToArray();
			int id = 0;
			foreach ((Player p, int i) in civs.Select((p, i) => (p, i)))
			{
				City c = g.AddCity(p, id++, 20 + i * 10, 25)!;
				c.Size = (byte)(4 + 3 * i);
				p.SetCulture(5000 + 4000 * i);   // large, so a one-point shift in the average shows after rounding
			}
			// The civ that will fall is a one-town civ, as the fallen usually are: a small peak
			// that, left in the average, pulls it down.
			civs[3].Cities.Single().Size = 1;
			// A barbarian town: small, and not a nation.
			g.AddCity(g.GetPlayer(0), id++, 60, 32)!.Size = 2;

			// One sample while the last civ is alive...
			g.RecordScoreSnapshot();

			// ...then it loses its only city. Its old peak must stop counting.
			Player fallen = civs[3];
			fallen.Cities.Single().Owner = g.PlayerNumber(civs[0]);
			g.GameTurn++;
			g.RecordScoreSnapshot();
			Assert.False(g.CountsInCulturalAverage(fallen), "fixture: the fallen civ still counts");

			int[] newest = Game.CulturePerHeadHistory().Last();
			long avg = g.CulturalWorldAverageNow();
			foreach (Player p in civs.Take(3))
				Assert.Equal((int)Game.CulturalDensity(p, avg), newest[g.PlayerNumber(p) + 1]);
		}
	}
}
