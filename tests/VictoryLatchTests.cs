// CivOne tests
//
// Every ending in EndTurn is reached from a condition that STAYS TRUE once met — a streak
// sitting at 20, a year past 2100, the last rival destroyed. The ending enqueues a screen
// chain finishing in Runtime.Quit(), and the queue takes several rounds to drain. Until it
// does, an unguarded block fires again on every round: the milestone awarded again, the fame
// roster written again, the newspaper stacked.
//
// Found on Diaspora, where a test expecting +200 read +600. Dome, Conquest and Coexistence
// already had their latches — the comment on _coexistenceFired records a score of 5,881
// becoming 11,496 — but Pax Mercatoria, Cultural Ascendancy and the 2100 AD score ending
// did not.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class VictoryLatchTests
	{
		private static void PlayRounds(Game g, int rounds)
		{
			uint target = g.GameTurn + (uint)rounds;
			while (g.GameTurn < target)
			{
				Sim.ClearTasks();
				g.EndTurn();
			}
		}

		// A world where the human is admired: six foreign cities inside the 5-tile shadow,
		// owned by civs holding less than a third of the human's culture, three rivals alive,
		// Philosophy known. That is the Cultural Ascendancy condition, and once met it stays
		// met — which is the whole point of this test.
		[Fact]
		public void CulturalAscendancyAwardsItsMilestoneExactlyOnce()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player human = g.HumanPlayer;
			Player[] rivals = g.Players
				.Where(p => p is not null && p != human && g.PlayerNumber(p) != 0).Take(3).ToArray();
			foreach (Player p in rivals.Append(human))
			{
				p.Government = new CivOne.Governments.Monarchy();
				p.Explore(40, 25, range: 20);
			}
			human.AddAdvance(new Philosophy(), false);
			g.AddCity(human, 0, 40, 25)!.Size = 6;

			// Six neighbours within the shadow. The target is three fifths of reach with a
			// floor of 6, so a reach of exactly 6 demands all six — the tightest fixture that
			// still clears the bar.
			int id = 1;
			foreach ((int x, int y) in new[] { (38, 23), (42, 23), (38, 27), (42, 27), (37, 25), (43, 25) })
			{
				City c = g.AddCity(rivals[id % rivals.Length], id, x, y)!;
				c.Size = 3;
				id++;
			}
			human.SetCulture(900);
			foreach (Player p in rivals) p.SetCulture(100);

			(int inRange, int shadow, long bestNear) = g.CulturalReachAndShadow(human);
			Assert.True(shadow >= Game.CulturalShadowTarget(inRange),
				$"fixture is not admired: shadow {shadow} of {Game.CulturalShadowTarget(inRange)} (reach {inRange})");
			int before = human.MilestoneScore;

			// Past the target on purpose: the rounds after the win are where an unlatched
			// ending awards itself again.
			PlayRounds(g, 24);

			Assert.True(g.Progress(g.PlayerNumber(g.HumanPlayer)).CultureStreak >= 20, $"streak reached only {g.Progress(g.PlayerNumber(g.HumanPlayer)).CultureStreak}");
			Assert.Equal(before + 150, human.MilestoneScore);
		}

		// Pax Mercatoria's condition needs half the world's output plus economic bindings on
		// half the surviving rivals — a fixture heavy enough that it would be testing itself
		// more than the latch. The 2100 ending awards no milestone at all, so there is no
		// score to watch. Both are pinned at the source instead, the same way
		// EconomicHegemonyTests pins the exclusions it cannot cheaply stage.
		[Theory]
		[InlineData("Progress(cnum).EconStreak >= 20", "_econVictoryFired")]
		[InlineData("Progress(cnum).CultureStreak >= 20", "_cultVictoryFired")]
		[InlineData("Common.TurnToYear(_gameTurn) >= 2100", "_scoreVictoryFired")]
		public void EveryStandingConditionEndingIsLatched(string condition, string latch)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "Game.cs"));

			int at = src.IndexOf(condition);
			Assert.True(at > 0, $"the {condition} ending has moved or been rewritten");
			// The latch must be on the condition itself, not merely somewhere nearby.
			string line = src.Substring(at, src.IndexOf('\n', at) - at);

			Assert.Contains("!" + latch, line);
		}
	}
}
