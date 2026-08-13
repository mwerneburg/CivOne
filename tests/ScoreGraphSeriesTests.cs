// CivOne tests
//
// The score graph grew two more pages: cultural weight, and gross economic output.
//
// Output is the interesting one. Pax Mercatoria asks the human to hold more than half the
// world's gross output for twenty consecutive turns, and nothing on any screen said what your
// share was — the streak ran invisibly and a player could only infer progress from the two
// advisor messages at turns 1 and 10. The page graphs GrossOutputOf, which is literally the
// function the victory check calls, so the picture cannot drift from the rule.
//
// These tests are about the DATA behind the pages. The three series must stay index-aligned:
// the report pages one scroll position across all of them, so a ragged history would plot one
// civ's culture against another turn's score.

using System.Linq;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class ScoreGraphSeriesTests
	{
		private static Game AGameWithHistory(int snapshots)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.CurrentPlayer;
			p.Government = new Monarchy();
			p.Explore(40, 25, range: 10);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;

			// The HUMAN needs output of their own. At turn 0 the current player is not the
			// human, and a human with no cities has GrossOutput 0 — which silently made the
			// threshold test unable to tell "includes the human" from "excludes the human".
			// The negative check caught that: zeroing the human's entry changed nothing.
			if (g.HumanPlayer != p)
			{
				g.HumanPlayer.Government = new Monarchy();
				g.HumanPlayer.Explore(60, 25, range: 10);
				City hc = g.AddCity(g.HumanPlayer, 1, 60, 25)!;
				hc.Size = 6;
			}

			for (int i = 0; i < snapshots; i++) g.RecordScoreSnapshot();
			Sim.ClearTasks();
			return g;
		}

		// One snapshot per turn per series, all the same length, all starting with the turn.
		[Fact]
		public void TheThreeSeriesStayAligned()
		{
			Game g = AGameWithHistory(5);

			Assert.Equal(5, g.ScoreHistory.Count);
			Assert.Equal(g.ScoreHistory.Count, g.CultureHistory.Count);
			Assert.Equal(g.ScoreHistory.Count, g.OutputHistory.Count);

			for (int t = 0; t < g.ScoreHistory.Count; t++)
			{
				Assert.Equal(g.ScoreHistory[t].Length, g.CultureHistory[t].Length);
				Assert.Equal(g.ScoreHistory[t].Length, g.OutputHistory[t].Length);
				Assert.Equal(g.ScoreHistory[t][0], g.CultureHistory[t][0]);   // same turn stamp
				Assert.Equal(g.ScoreHistory[t][0], g.OutputHistory[t][0]);
			}
		}

		// The culture series records the ledger the legend used to show in brackets.
		[Fact]
		public void TheCultureSeriesRecordsPlayerCulture()
		{
			Game g = AGameWithHistory(0);
			Player p = g.CurrentPlayer;
			int idx = g.PlayerNumber(p) + 1;

			g.RecordScoreSnapshot();
			int before = g.CultureHistory[^1][idx];

			p.SetCulture(before + 250);
			g.RecordScoreSnapshot();

			Assert.Equal(before + 250, g.CultureHistory[^1][idx]);
		}

		// ...and the output series records exactly what the victory check reads, which is the
		// whole point of the page: a graph that disagreed with the rule would be worse than no
		// graph at all.
		[Fact]
		public void TheOutputSeriesRecordsTheVictoryMetric()
		{
			Game g = AGameWithHistory(0);
			g.RecordScoreSnapshot();

			foreach (Player p in g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0))
			{
				int idx = g.PlayerNumber(p) + 1;
				Assert.Equal(g.GrossOutputOf(p), g.OutputHistory[^1][idx]);
			}
		}

		// The threshold the output page draws is half the world's gross output — a number the
		// player is asked to beat, so it must be computed from the same GrossOutputOf the
		// victory uses and must include the human's own share in the denominator. Reported
		// from a 1922 AD game where the line looked far too high; it was not the arithmetic
		// (half was 5,303 against a human 7,202) but the line failing to draw at all with a
		// single sample, while its legend was printed regardless.
		[Fact]
		public void TheHalfWorldThresholdIsHalfOfEveryoneIncludingYou()
		{
			Game g = AGameWithHistory(1);

			int world = g.Players.Where(p => p is not null).Sum(g.GrossOutputOf);
			var snap = g.OutputHistory[^1];
			int fromSnapshot = 0;
			for (int pi = 1; pi < snap.Length; pi++) fromSnapshot += snap[pi];

			Assert.True(g.GrossOutputOf(g.HumanPlayer) > 0, "fixture gave the human no output to count");
			Assert.Equal(world, fromSnapshot);
			Assert.Equal(g.GrossOutputOf(g.HumanPlayer), snap[g.PlayerNumber(g.HumanPlayer) + 1]);
		}

		// Destroyed civilizations are not standings. They kept their place in the legend with a
		// frozen score forever, which is how a finished game showed four rivals on 0 output as
		// though they were merely poor rather than gone.
		[Fact]
		public void TheGraphLeavesOutDestroyedCivilizations()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(RepoRoot(), "src", "Screens", "Reports", "CivilizationScore.cs"));
			int at = src.IndexOf("var players = Game.Players");
			Assert.True(at > 0, "the player filter has moved or been rewritten");
			string block = src.Substring(at, src.IndexOf(';', at) - at);

			Assert.Contains("IsDestroyed", block);
		}

		private static string RepoRoot()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return dir!.FullName;
		}

		// The new series round-trip through a save. (A save written before they existed simply
		// has no such keys and loads with an empty history — the report tolerates a short
		// series rather than back-filling zeros, which would draw a cliff at the join.)
		[Fact]
		public void TheNewSeriesRoundTripThroughASave()
		{
			Game g = AGameWithHistory(3);
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "graphseries.cos");

			g.SaveCos(path);
			string text = System.IO.File.ReadAllText(path);
			Assert.Contains("CultureHistory:", text);
			Assert.Contains("OutputHistory:", text);

			Sim.ResetState();   // LoadCos builds a fresh Game; one must not already exist
			Assert.True(Game.LoadCos(path), "load failed");
			Assert.Equal(3, Game.Instance.CultureHistory.Count);
			Assert.Equal(3, Game.Instance.OutputHistory.Count);
		}
	}
}
