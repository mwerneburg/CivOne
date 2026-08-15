// CivOne tests
//
// The victory-standings log, and the reach/shadow split it needed.
//
// Why reach exists: the endgame investigation could only read FINISHED saves, and a victory
// fires on FIRST crossing. A cultural shadow of 41 at turn 750 is equally consistent with
// crossing the bar at turn 300 and at turn 700, and those imply opposite designs. The log
// samples the trajectory so the question can be answered by reading it.
//
// The risk in this change is not the logging — it is that CulturalShadow, a live victory
// condition, was rewritten to share one covered-tile pass with the new reach count. So the
// first test here is equivalence against the rule as it stood, not a property of the new one.

using System.Linq;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class VictoryStandingsTests
	{
		// Us, a poor near neighbour, a rich near neighbour, and a poor distant one. That
		// covers every branch: in reach and dominated, in reach and not dominated, and
		// dominated on culture but out of reach.
		private static (Game game, Player us, Player poorNear, Player richNear, Player poorFar) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player us = ps[0], poorNear = ps[1], richNear = ps[2], poorFar = ps[3];
			foreach (Player p in new[] { us, poorNear, richNear, poorFar })
			{
				p.Government = new Monarchy();
				p.Explore(45, 25, range: 40);
			}

			g.AddCity(us, 0, 40, 25)!.Size = 6;
			g.AddCity(poorNear, 1, 43, 25)!.Size = 3;   // 3 tiles — in reach
			g.AddCity(richNear, 2, 37, 25)!.Size = 3;   // 3 tiles — in reach
			g.AddCity(poorFar,  3, 65, 25)!.Size = 3;   // 25 tiles — out of reach

			us.SetCulture(900);
			poorNear.SetCulture(100);   // 900 > 3x100 — dominated
			// 400 rather than a comfortable 600, and the margin is the point: 3x400 = 1200
			// clears 900 so this neighbour is NOT dominated, but 2x400 = 800 does not. That
			// makes the fixture sensitive to the dominance RATIO, not just to its direction.
			// At 600 it was not: both 2x and 3x left this civ out, so the equivalence test
			// below passed against a deliberately broken rule. Caught by the negative check.
			richNear.SetCulture(400);   // in reach, NOT dominated — but only just
			poorFar.SetCulture(100);    // poor, but nowhere near us
			Sim.ClearTasks();
			return (g, us, poorNear, richNear, poorFar);
		}

		// The load-bearing one. CulturalShadow decides a victory; the rewrite must not have
		// moved it. Verified against the pre-change rule recomputed here independently.
		[Fact]
		public void TheShadowIsUnchangedByTheReachRefactor()
		{
			(Game g, Player us, _, _, _) = AWorld();

			// The rule as it stood: build the covered set, then count foreign cities whose
			// owner holds under a third of our culture.
			int Original(Player p)
			{
				byte num = g.PlayerNumber(p);
				long threshold = p.Culture;
				if (threshold <= 0) return 0;
				var covered = new System.Collections.Generic.HashSet<(int, int)>();
				foreach (City c in g.GetCities().Where(c => c.Owner == num && c.Size > 0))
					for (int dy = -Game.CulturalShadowRange; dy <= Game.CulturalShadowRange; dy++)
					for (int dx = -Game.CulturalShadowRange; dx <= Game.CulturalShadowRange; dx++)
						covered.Add((c.X + dx, c.Y + dy));
				int count = 0;
				foreach (City c in g.GetCities())
				{
					if (c.Size <= 0 || c.Owner == num || c.Owner == 0) continue;
					Player owner = g.GetPlayer(c.Owner);
					if (owner.Culture * 3 >= threshold) continue;
					if (covered.Contains((c.X, c.Y))) count++;
				}
				return count;
			}

			foreach (Player p in g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0))
				Assert.Equal(Original(p), g.CulturalShadow(p));
		}

		// Reach counts the neighbours you could shadow; shadow counts the ones you do.
		// The rich neighbour next door is the whole point: in reach, not dominated.
		[Fact]
		public void ReachCountsNeighboursThatTheShadowDoesNot()
		{
			(Game g, Player us, _, _, _) = AWorld();

			(int reach, int shadow) = g.CulturalReachAndShadow(us);

			Assert.Equal(2, reach);    // poorNear and richNear
			Assert.Equal(1, shadow);   // only poorNear is culturally dominated
		}

		// A distant weakling is dominated on culture and still counts for nothing — the
		// reach clause is geography, and that is what makes it move with civ count.
		[Fact]
		public void DistanceExcludesACityFromBothCounts()
		{
			(Game g, Player us, _, _, Player poorFar) = AWorld();

			(int reach, int shadow) = g.CulturalReachAndShadow(us);

			Assert.True(us.Culture > poorFar.Culture * 3, "fixture: the far civ should be dominated on culture");
			Assert.Equal(2, reach);    // the far city is in neither
			Assert.Equal(1, shadow);
		}

		// A civ with no culture shadows nobody — preserved from the original, which returned
		// zero before it built anything. Reach is still real, and is the point: it shows a
		// civ surrounded by neighbours it has no cultural hold over.
		[Fact]
		public void ACivWithNoCultureHasReachButNoShadow()
		{
			(Game g, Player us, _, _, _) = AWorld();
			us.SetCulture(0);

			(int reach, int shadow) = g.CulturalReachAndShadow(us);

			Assert.Equal(2, reach);
			Assert.Equal(0, shadow);
		}

		// The record has to carry both halves of the ratio, or the log cannot answer the
		// question it was added for. A missing field fails silently: the run completes, the
		// file looks fine, and the answer is simply not in it.
		[Theory]
		[InlineData("turn")]
		[InlineData("civ")]
		[InlineData("reach")]
		[InlineData("shadow")]
		[InlineData("culture")]
		[InlineData("gross_out")]
		[InlineData("world_out")]
		[InlineData("ss_module")]
		[InlineData("launch_turn")]
		[InlineData("mission_ctl")]
		public void TheStandingsRecordCarriesEveryMetric(string field)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "DecisionLogger.cs"));

			int at = src.IndexOf("\"victory_standings\"),");
			Assert.True(at > 0, "the victory_standings record has moved or been rewritten");
			string record = src.Substring(at, src.IndexOf("}));", at) - at);

			Assert.Contains($"KV(\"{field}\"", record);
		}
	}
}
