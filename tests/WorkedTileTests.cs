// CivOne tests
//
// "Is this tile already farmed by somebody else?" — asked eight times per land move.
//
// Game.IsWorkedByOther and GetWorkerOfTile used to walk every city and materialise its
// ResourceTiles, which builds CityRadius: a fresh 5x5 ITile array plus a per-tile visibility
// pass, per city, per question. BaseUnitLand.ValidMoveTarget asks it for all eight border
// tiles of every land move, so the whole city list was walked eight times per step.
//
// MoveCostBenchmark measures the result at 250 cities and 2,000 units: 3.66 ms per move for
// the eight calls, against 0.011 ms for a full scan of every unit in the game. After the fix,
// 0.035 ms — 105x. Both figures come from the same in-process world, which is the point of
// having it: the two previous attempts at this cost were aimed by live turn_timing records
// and each took a game restart to disprove.
//
// These tests pin the ANSWER, not the implementation. They pass identically against the old
// LINQ form and the pre-filtered one — that equivalence is what licenses the rewrite.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class WorkedTileTests
	{
		// Two civs, one city each, on open ground both can see.
		private static (Game g, Player mine, Player theirs, City theirCity) AFrontier()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player mine = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			Player theirs = g.Players.First(x => x is not null && x != mine && x != g.HumanPlayer
			                                  && g.PlayerNumber(x) != 0);
			for (int y = 18; y <= 32; y++)
			for (int x = 32; x <= 48; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			mine.Explore(40, 25, range: 10);
			theirs.Explore(40, 25, range: 10);
			City theirCity = g.AddCity(theirs, 0, 40, 25)!;
			theirCity.Size = 4;
			Sim.ClearTasks();
			return (g, mine, theirs, theirCity);
		}

		// Assign a tile to the city's citizens.
		//
		// NOT via SetResourceTile: that refuses once _resourceTiles.Count >= Size, and a city
		// has already filled its slots by the time a test sees it. The first version of these
		// tests used it, assigned nothing, and asked "is this worked?" about a tile nobody
		// worked. Writing the field directly is what the engine's own picker does.
		private static void Assign(City c, int x, int y)
		{
			var field = typeof(City).GetField("_resourceTiles",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			var tiles = (System.Collections.Generic.List<ITile>)field.GetValue(c)!;
			if (!tiles.Any(t => t.X == x && t.Y == y)) tiles.Add(Map.Instance[x, y]);
		}

		private static void Unassign(City c, int x, int y)
		{
			var field = typeof(City).GetField("_resourceTiles",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			var tiles = (System.Collections.Generic.List<ITile>)field.GetValue(c)!;
			tiles.RemoveAll(t => t.X == x && t.Y == y);
		}

		// The rule: a tile another civ's city actually works is closed to us.
		[Fact]
		public void ATileWorkedByAnotherCivIsClosed()
		{
			(Game g, Player mine, Player theirs, City theirCity) = AFrontier();
			Assign(theirCity, 41, 25);

			Assert.True(g.IsWorkedByOther(41, 25, g.PlayerNumber(mine)));
			Assert.Equal(theirs, g.GetWorkerOfTile(41, 25, g.PlayerNumber(mine)));
		}

		// Inside the radius but not assigned to a citizen: open ground.
		[Fact]
		public void AnUnassignedTileInTheRadiusIsOpen()
		{
			(Game g, Player mine, Player theirs, City theirCity) = AFrontier();
			Unassign(theirCity, 41, 25);

			Assert.False(g.IsWorkedByOther(41, 25, g.PlayerNumber(mine)));
			Assert.Null(g.GetWorkerOfTile(41, 25, g.PlayerNumber(mine)));
		}

		// The city centre is never reported as "worked by another" — you are walking into the
		// city itself, which is a different question with a different answer elsewhere.
		//
		// The centre is assigned here deliberately. ResourceTiles always contains it (through
		// its own clause, not _resourceTiles), so only the explicit centre guard excludes it;
		// without this line the test passed with the guard deleted, which is to say it was
		// testing nothing.
		[Fact]
		public void TheCityCentreIsNotReported()
		{
			(Game g, Player mine, Player theirs, City theirCity) = AFrontier();
			Assign(theirCity, 40, 25);

			Assert.False(g.IsWorkedByOther(40, 25, g.PlayerNumber(mine)));
			Assert.Null(g.GetWorkerOfTile(40, 25, g.PlayerNumber(mine)));
		}

		// Our OWN city's fields are ours to cross.
		[Fact]
		public void OurOwnFieldsAreNotForeign()
		{
			(Game g, Player mine, Player theirs, City theirCity) = AFrontier();
			Assign(theirCity, 41, 25);

			Assert.False(g.IsWorkedByOther(41, 25, g.PlayerNumber(theirs)));
		}

		// Beyond the 5x5 radius nothing is claimed, however many cities exist.
		[Fact]
		public void OutsideTheRadiusNothingIsClaimed()
		{
			(Game g, Player mine, Player theirs, City theirCity) = AFrontier();

			Assert.False(g.IsWorkedByOther(43, 25, g.PlayerNumber(mine)));
			Assert.False(g.IsWorkedByOther(40, 28, g.PlayerNumber(mine)));
		}

		// The four corners of the 5x5 are cut by CityRadius, so a corner is open even though it
		// is within Chebyshev distance 2 — the distance pre-filter alone would get this wrong.
		[Fact]
		public void TheCutCornersAreOpen()
		{
			(Game g, Player mine, Player theirs, City theirCity) = AFrontier();
			// Assign() bypasses the assignment path entirely, so what excludes this tile can
			// only be the corner rule itself.
			Assign(theirCity, 42, 27);

			Assert.False(g.IsWorkedByOther(42, 27, g.PlayerNumber(mine)));
		}

		// A city's fields wrap around the map seam like everything else.
		[Fact]
		public void ItWorksAcrossTheMapSeam()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player mine = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			Player theirs = g.Players.First(x => x is not null && x != mine && x != g.HumanPlayer
			                                  && g.PlayerNumber(x) != 0);
			for (int y = 20; y <= 30; y++)
			for (int x = -4; x <= 4; x++)
				Map.Instance.ChangeTileType((x + 80) % 80, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			mine.Explore(0, 25, range: 10);
			theirs.Explore(0, 25, range: 10);
			City theirCity = g.AddCity(theirs, 0, 1, 25)!;
			theirCity.Size = 4;
			Sim.ClearTasks();
			Assign(theirCity, 79, 25);   // two tiles west of x=1, across the seam

			Assert.True(g.IsWorkedByOther(79, 25, g.PlayerNumber(mine)));
		}

		// The cost, pinned on the source. A timing assertion would flake — WaterBodyCostTests
		// does — and the claim is structural: the per-tile question must not build ResourceTiles.
		[Fact]
		public void ItDoesNotMaterialiseEveryCitysRadius()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "Game.cs"));

			int at = src.IndexOf("private City? WorkingCity(int x, int y, byte owner)");
			Assert.True(at > 0, "WorkingCity has moved or been renamed");
			string body = src.Substring(at, src.IndexOf("\n\t\t}", at) - at);

			Assert.DoesNotContain("ResourceTiles", body);
			Assert.Contains("if (dx > 2 || Math.Abs(c.Y - y) > 2) continue;", body);
		}
	}
}
