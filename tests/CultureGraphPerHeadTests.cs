// CivOne tests
//
// The culture graph plotted the wrong race.
//
// Cultural Ascendancy is decided on culture PER HEAD. The graph drew raw culture, which is a
// different contest with a different leader. In game 3de868a5 the human's raw-culture curve
// towered over the field from turn 170 — and the Lakota, on a THIRD of that culture, led the
// measure that counts and were running the victory clock at turn 405. The player read the
// graph, saw a comfortable lead, and was losing.
//
// The page already printed "CULTURE PER HEAD n - RANK n/m" as text beside the curves. Text
// said one thing and the picture said another; players believe the picture.
//
// PopulaceHistory is recorded so the curve can divide with the same expression the rule
// uses, rather than storing a pre-divided figure that could drift from it.

using System.IO;
using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class CultureGraphPerHeadTests
	{
		private static (Game game, Player big, Player small) AWorldWhereTotalAndPerHeadDisagree()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player big = ps[0], small = ps[1];
			foreach (Player p in new[] { big, small }) p.Government = new Monarchy();

			// The exact shape of the reported game: the big civ leads on TOTAL culture and
			// trails on the measure that decides the victory.
			g.AddCity(big,   0, 40, 25)!.Size = 20;
			g.AddCity(small, 1, 45, 30)!.Size = 4;
			big.SetCulture(2000);     // 100 per head
			small.SetCulture(1200);   // 300 per head
			Sim.ClearTasks();
			return (g, big, small);
		}

		// The snapshot the graph is built from has to exist at all.
		[Fact]
		public void PopulaceIsRecordedAlongsideCulture()
		{
			(Game g, Player big, Player _) = AWorldWhereTotalAndPerHeadDisagree();
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }

			Assert.NotEmpty(Game.Instance.PopulaceHistory);
			int[] last = Game.Instance.PopulaceHistory[^1];
			Assert.Equal(20, last[Game.Instance.PlayerNumber(big) + 1]);
		}

		// The two series line up, sample for sample, or dividing one by the other is nonsense.
		[Fact]
		public void TheTwoSeriesStayInStep()
		{
			(Game g, Player _, Player __) = AWorldWhereTotalAndPerHeadDisagree();
			uint target = g.GameTurn + 4u;
			for (int i = 0; i < 800 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }

			var c = Game.Instance.CultureHistory;
			var q = Game.Instance.PopulaceHistory;
			Assert.Equal(c.Count, q.Count);
			for (int i = 0; i < c.Count; i++)
			{
				Assert.Equal(c[i][0], q[i][0]);       // same turn stamp
				Assert.Equal(c[i].Length, q[i].Length);
			}
		}

		// The defect, as the graph actually draws it. Goes through Game.CulturePerHeadHistory —
		// the function the report plots — because an earlier version of this test recomputed
		// the ratio itself and passed happily against the raw-culture graph it was written to
		// catch. A test that reimplements the thing under test is testing itself.
		[Fact]
		public void TheGraphRanksByPerHeadNotByTotal()
		{
			(Game g, Player big, Player small) = AWorldWhereTotalAndPerHeadDisagree();
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }

			Assert.True(big.Culture > small.Culture, "fixture: the big civ should lead on total");
			long Pop(Player p) => System.Math.Max(1, p.Cities.Sum(c => (int)c.Size));
			Assert.True((double)small.Culture / Pop(small) > (double)big.Culture / Pop(big),
				"fixture: the small civ should lead per head");

			int[] plotted = Game.CulturePerHeadHistory()[^1];
			int bigPlot   = plotted[Game.Instance.PlayerNumber(big) + 1];
			int smallPlot = plotted[Game.Instance.PlayerNumber(small) + 1];

			Assert.True(smallPlot > bigPlot,
				$"the curve still ranks by total culture (big={bigPlot}, small={smallPlot})");
		}

		// The page must not go on calling it something else. "CULTURAL WEIGHT" over a per-head
		// axis is the same mislabelling in a different place.
		[Fact]
		public void ThePageIsLabelledForWhatItPlots()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "Reports", "CivilizationScore.cs"));

			Assert.Contains("Page.Culture => \"CULTURE PER HEAD\"", src);
			Assert.DoesNotContain("\"CULTURAL WEIGHT\"", src);
		}

		// Saves carry it, or a loaded game draws a flat line for everything before the load.
		[Fact]
		public void PopulaceHistoryRoundTripsThroughASave()
		{
			(Game g, Player big, Player _) = AWorldWhereTotalAndPerHeadDisagree();
			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }
			int before = Game.Instance.PopulaceHistory.Count;
			Assert.True(before > 0, "nothing recorded to round-trip");
			int[] lastBefore = Game.Instance.PopulaceHistory[^1];

			string path = Path.Combine(Settings.Instance.SavesDirectory, "perhead.cos");
			Game.Instance.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.Equal(before, Game.Instance.PopulaceHistory.Count);
			Assert.Equal(lastBefore, Game.Instance.PopulaceHistory[^1]);
		}
	}
}
