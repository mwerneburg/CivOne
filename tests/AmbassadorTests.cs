// CivOne tests
//
// Ambassadors by treaty (Sep 2026): the AI had no AI-to-AI diplomacy and its Diplomats never
// crossed water, so world contact stalled (36% in an autoplay) and neither the reach clause
// nor the Evaluators' cohesion could be met. A civ at peace with another whose city it has
// seen now exchanges ambassadors at AI.AmbassadorChance a turn — mutually with another AI,
// one way (their envoy to us) with the human.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class AmbassadorTests : System.IDisposable
	{
		private readonly bool _auto;
		public AmbassadorTests() { Sim.EnsureRuntime(); _auto = Settings.Instance.Autopilot; Settings.Instance.Autopilot = false; }
		public void Dispose() => Settings.Instance.Autopilot = _auto;

		// Two AI civs and the human, each with a city; `a` has seen the others' cities only
		// if `seen`.
		private static (Game g, Player a, Player b, Player human) ThreeCivs(bool seen = true)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player human = g.HumanPlayer;
			Player[] ais = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != human).ToArray();
			Player a = ais[0], b = ais[1];
			g.AddCity(a, 0, 15, 15)!.Size = 3;
			g.AddCity(b, 1, 60, 35)!.Size = 3;
			g.AddCity(human, 2, 60, 15)!.Size = 3;
			if (seen) { a.Explore(60, 35, range: 2); a.Explore(60, 15, range: 2); }
			Sim.ClearTasks();
			return (g, a, b, human);
		}

		private static void Turns(Player p, int n)
		{
			for (int i = 0; i < n; i++) AI.Instance(p).ConsiderAmbassadors();
		}

		[Fact]
		public void CivsWhoHaveSeenEachOtherExchangeAmbassadors()
		{
			(Game _, Player a, Player b, Player _) = ThreeCivs();
			Assert.False(a.HasEmbassy(b), "fixture: already in contact");

			Turns(a, 1000);

			Assert.True(a.HasEmbassy(b) && b.HasEmbassy(a), "no exchange in 1000 turns");
		}

		[Fact]
		public void NotWithAnEnemy()
		{
			(Game _, Player a, Player b, Player _) = ThreeCivs();
			a.DeclareWar(b);

			Turns(a, 1000);

			Assert.False(a.HasEmbassy(b));
		}

		[Fact]
		public void NotWithACivTheyHaveNeverSeen()
		{
			(Game _, Player a, Player b, Player _) = ThreeCivs(seen: false);
			Assert.False(b.Cities.Any(c => a.Visible(c.X, c.Y)), "fixture: a can see b");

			Turns(a, 1000);

			Assert.False(a.HasEmbassy(b));
		}

		// Their envoy comes to us; ours we still send ourselves.
		[Fact]
		public void WithTheHumanItRunsOneWay()
		{
			(Game _, Player a, Player _, Player human) = ThreeCivs();

			Turns(a, 1000);

			Assert.True(a.HasEmbassy(human), "no envoy arrived");
			Assert.False(human.HasEmbassy(a), "the human was handed an embassy they did not send");
		}
	}
}
