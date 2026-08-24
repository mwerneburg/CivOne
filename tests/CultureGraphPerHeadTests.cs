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

		// A SAVE WRITTEN BEFORE PopulaceHistory EXISTED — which is every save on disk when
		// this shipped, and the case the graph is actually used in.
		//
		// Reported from a loaded game at 1851 AD: the culture page drew only the first few
		// turns of the game, labelled 3980 BCE to 3660 BCE, with today's numbers in them.
		//
		// The derivation paired culture[i] with populace[i] from the START of both lists.
		// After a load the culture history runs from 4000 BCE and the populace history begins
		// at the turn the save was loaded, so index 0 of one is four thousand years from
		// index 0 of the other. Every pair was mismatched, and taking min(count) meant the
		// graph showed the OLDEST culture samples rather than the newest.
		//
		// TheTwoSeriesStayInStep passed throughout: in a fresh game both lists start together
		// and never diverge. The only way to see this is to make them diverge the way a load
		// does.
		[Fact]
		public void AShortPopulaceHistoryStillPlotsTheRECENTTurns()
		{
			(Game g, Player _, Player __) = AWorldWhereTotalAndPerHeadDisagree();
			uint target = g.GameTurn + 8u;
			for (int i = 0; i < 1600 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }
			Assert.True(Game.Instance.CultureHistory.Count >= 6, "not enough history to test with");

			// What loading a pre-PopulaceHistory save leaves behind: a long culture series and
			// an empty populace one, which then starts filling from the current turn.
			var populace = (System.Collections.Generic.List<int[]>)typeof(Game)
				.GetField("_populaceHistory", System.Reflection.BindingFlags.NonPublic
				                            | System.Reflection.BindingFlags.Instance)!
				.GetValue(Game.Instance)!;
			populace.Clear();

			ushort resumed = Game.Instance.GameTurn;
			uint target2 = Game.Instance.GameTurn + 3u;
			for (int i = 0; i < 600 && Game.Instance.GameTurn < target2; i++)
				{ Sim.ClearTasks(); Game.Instance.EndTurn(); }

			var plotted = Game.CulturePerHeadHistory();
			Assert.NotEmpty(plotted);

			// Every sample must be from AFTER the load, not from the opening turns.
			foreach (int[] row in plotted)
				Assert.True(row[0] >= resumed,
					$"the graph is plotting turn {row[0]}, from before the populace history began "
					+ $"(resumed at {resumed}) — the two series are being paired by index");
		}

		// ...and the pairing is right, not merely late: each row divides the culture and the
		// populace RECORDED ON THAT TURN.
		[Fact]
		public void EachPlottedSampleDividesTheSameTurnsFigures()
		{
			(Game g, Player _, Player __) = AWorldWhereTotalAndPerHeadDisagree();
			uint target = g.GameTurn + 6u;
			for (int i = 0; i < 1200 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }

			var culture  = Game.Instance.CultureHistory.ToDictionary(r => r[0]);
			var populace = Game.Instance.PopulaceHistory.ToDictionary(r => r[0]);

			foreach (int[] row in Game.CulturePerHeadHistory())
			{
				Assert.True(culture.ContainsKey(row[0]) && populace.ContainsKey(row[0]),
					$"plotted a turn ({row[0]}) that is not in both source series");
				int[] c = culture[row[0]], q = populace[row[0]];
				for (int pi = 1; pi < row.Length; pi++)
					Assert.Equal(c[pi] / System.Math.Max(1, q[pi]), row[pi]);
			}
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

		// Reported from a game at 1870 AD: the axis ran to 800 while every civilization on
		// screen sat between 10 and 64, squashing all seven curves into the bottom eighth.
		//
		// The cause was a civ with culture and NO PEOPLE. Decoded from the save: Skynet held
		// 726 culture and zero city population, and dividing by Math.Max(1, 0) published that
		// 726 as a per-head figure. NiceInterval then rounded the axis up to exactly 800.
		//
		// Skynet was not even in the legend. It set the scale for a graph it does not appear
		// on.
		[Fact]
		public void ACivilizationWithNoPeopleHasNoCulturePerHead()
		{
			(Game g, Player big, Player small) = AWorldWhereTotalAndPerHeadDisagree();
			Player ghost = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                                 && p != big && p != small && p.Cities.Length == 0);
			ghost.SetCulture(726);                 // culture, no cities — Skynet's exact shape

			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }

			int[] row = Game.CulturePerHeadHistory()[^1];
			int col = Game.Instance.PlayerNumber(ghost) + 1;

			Assert.Equal(0, row[col]);
		}

		// ...and the effect that mattered: nothing on the plotted series exceeds what the
		// civilizations with people actually hold, so the axis fits them.
		[Fact]
		public void TheSeriesNeverExceedsTheLivingCivilizations()
		{
			(Game g, Player big, Player small) = AWorldWhereTotalAndPerHeadDisagree();
			Player ghost = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                                 && p != big && p != small && p.Cities.Length == 0);
			ghost.SetCulture(100000);

			uint target = g.GameTurn + 2u;
			for (int i = 0; i < 400 && g.GameTurn < target; i++) { Sim.ClearTasks(); g.EndTurn(); }

			long Pop(Player p) => System.Math.Max(1, p.Cities.Sum(c => (int)c.Size));
			int living = (int)System.Math.Max(big.Culture / Pop(big), small.Culture / Pop(small));

			foreach (int[] row in Game.CulturePerHeadHistory())
				for (int pi = 1; pi < row.Length; pi++)
					Assert.True(row[pi] <= living,
						$"column {pi} plots {row[pi]}, above every civilization that has people ({living})");
		}

		// The axis must be scaled by what it draws. The scan used to walk every column ever
		// recorded — barbarians, destroyed civs, story factions — none of which are drawn or
		// listed.
		[Fact]
		public void TheAxisIsScaledOnlyByTheCivilizationsItDraws()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "Reports", "CivilizationScore.cs"));
			int at = src.IndexOf("int maxScore = 1;");
			Assert.True(at > 0, "the axis range calculation has moved");
			string block = src.Substring(System.Math.Max(0, at - 400), 800);

			Assert.Contains("foreach (int pi in columns)", block);
			Assert.DoesNotContain("for (int pi = 1; pi < snap.Length; pi++)", block);
		}

		// One quantity, one number. The readout used :F0 (rounds) while the legend and the
		// curve use an int cast (truncates), so a civ on 41.5 per head was shown as 42 beside
		// a legend entry reading 41 — one line apart on the same screen.
		[Fact]
		public void TheReadoutAndTheLegendShowTheSameNumber()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "Reports", "CivilizationScore.cs"));
			int at = src.IndexOf("string standing =");
			Assert.True(at > 0, "the standing readout has moved");
			string block = src.Substring(System.Math.Max(0, at - 300), 700);

			Assert.DoesNotContain("PerHead(Human):F0", block);
			Assert.Contains("(int)PerHead(Human)", block);
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
