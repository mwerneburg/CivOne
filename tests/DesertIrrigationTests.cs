// CivOne tests
//
// Desert does not daisy-chain.
//
// Irrigation normally spreads from any adjacent irrigated tile, which is how a single oasis
// seeds a line of green marching a hundred tiles across the Gobi. Desert now needs a REAL
// source in the cross — river, lake, or freshwater coast — so riverbank and oasis agriculture
// still work (Egypt and Mesopotamia happened exactly that way) while the chain stops at the
// second tile. The deep interior gets the Moisture Farm instead, and cutting a river across
// dry land late in the game is worth far more than it was.
//
// The rule had been stated FIVE times across four files. It is stated once now, in
// TileExtensions.HasIrrigationSource, and these tests hold every caller to it — that
// duplication is the single most repeated bug in this codebase.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class DesertIrrigationTests
	{
		// A dry plain with nothing wet anywhere near it.
		private static (Game g, Player p) ADryWorld(Terrain terrain)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 8);
			for (int y = 18; y <= 32; y++)
			for (int x = 32; x <= 48; x++)
			{
				Map.Instance.ChangeTileType(x, y, terrain);
				((BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Sim.ClearTasks();
			return (g, p);
		}

		[Fact]
		public void DesertBesideARiverStillIrrigates()
		{
			ADryWorld(Terrain.Desert);
			Map.Instance.ChangeTileType(39, 25, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.True(Map.Instance[40, 25].HasIrrigationSource(), "riverbank desert must still farm");
		}

		// The chain, and the whole point of the change: an irrigated desert tile is not a
		// water source for the next desert tile along.
		[Fact]
		public void DesertDoesNotChainFromAnIrrigatedNeighbour()
		{
			ADryWorld(Terrain.Desert);
			Map.Instance[39, 25].Irrigation = true;

			Assert.False(Map.Instance[40, 25].HasIrrigationSource(),
				"the daisy-chain marched on across the desert");
		}

		// ...but grassland still chains, which is the behaviour everywhere that is not desert.
		[Fact]
		public void GrasslandStillChains()
		{
			ADryWorld(Terrain.Grassland1);
			Map.Instance[39, 25].Irrigation = true;

			Assert.True(Map.Instance[40, 25].HasIrrigationSource(), "chaining should be unchanged off desert");
		}

		// The late-game payoff the user pointed out: cutting a river across dry ground now
		// opens land that nothing else could reach.
		[Fact]
		public void ANewRiverOpensDesertThatWasClosed()
		{
			ADryWorld(Terrain.Desert);
			Assert.False(Map.Instance[40, 25].HasIrrigationSource(), "precondition: nothing wet nearby");

			Map.Instance.ChangeTileType(40, 24, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.True(Map.Instance[40, 25].HasIrrigationSource());
		}

		// ── every caller agrees ───────────────────────────────────────────────────

		// The builder. A settler standing on chain-fed desert must refuse the order, or the AI
		// is routed to work it cannot perform — the disagreement this project has four bug
		// comments about.
		[Fact]
		public void TheSettlerRefusesToIrrigateChainFedDesert()
		{
			(Game g, Player p) = ADryWorld(Terrain.Desert);
			Map.Instance[39, 25].Irrigation = true;
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(g.CurrentPlayer))!;

			// BuildIrrigation returns TRUE here: the return value means "order handled", not
			// "work started" — the no-water-source branch shows the player a note and returns
			// true. What must not happen is the job actually starting.
			s.BuildIrrigation();

			Assert.Equal(0, s.BuildingIrrigation);
		}

		[Fact]
		public void TheSettlerStillIrrigatesBesideARiver()
		{
			(Game g, Player p) = ADryWorld(Terrain.Desert);
			Map.Instance.ChangeTileType(39, 25, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(g.CurrentPlayer))!;

			Assert.True(s.BuildIrrigation());
		}

		// The AI's work scan, which is what routes a settler there in the first place.
		[Theory]
		[InlineData(false, false)]   // chain-fed only  -> no work
		[InlineData(true,  true)]    // real river      -> work
		public void TheWorkScanAgreesWithTheBuilder(bool river, bool expected)
		{
			(Game g, Player p) = ADryWorld(Terrain.Desert);
			if (river) Map.Instance.ChangeTileType(39, 25, Terrain.River);
			else       Map.Instance[39, 25].Irrigation = true;
			Map.Instance.RecalculateContinentsIfDirty();

			object work = typeof(AI).GetMethod("WorkAvailable",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { Map.Instance[40, 25] })!;

			Assert.Equal(expected, (bool)work.GetType().GetField("Irrigation")!.GetValue(work)!);
		}

		// The menu gate, so the human is not offered an order the builder refuses.
		[Fact]
		public void TheMenuGateAgreesToo()
		{
			ADryWorld(Terrain.Desert);
			Map.Instance[39, 25].Irrigation = true;

			Assert.False(Map.Instance[40, 25].AllowIrrigation());
		}

		// One statement of the rule, not five.
		//
		// Pinned narrowly: an earlier version asserted no caller mentions IsFreshwaterAt at
		// all, which failed on three legitimate and unrelated uses — river adjacency for the
		// Hydro Engineer, and the salt-water test that gates the Harbour. What must not come
		// back is the irrigation SOURCE TRIPLE (irrigated-or-river-or-swamp) outside the
		// shared predicate.
		[Theory]
		[InlineData("src/Units/Settlers.cs")]
		[InlineData("src/AI.Strategy.cs")]
		public void NoCallerRestatesTheWaterRule(string file)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, file.Replace('/', System.IO.Path.DirectorySeparatorChar)));

			Assert.DoesNotContain("is River || x is Swamp", src);
			Assert.DoesNotContain("is River) || (t is Swamp)", src);
		}

		// ...and the chain rule lives in exactly one place.
		[Fact]
		public void TheSharedPredicateCarriesTheDesertRule()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Tiles", "TileExtensions.cs"));

			Assert.Contains("bool mayChain = tile is not Desert;", src);
		}
	}
}
