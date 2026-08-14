// CivOne tests
//
// Terrace (Hills) and Moisture Farm (Desert): two ways to get food out of ground irrigation
// cannot reach. Irrigation needs fresh water in the cross, and the interior of a continent
// has none — which is why the Sahara filled with size-3 towns that had already built every
// food building available to them and were still starving.
//
// Salt Flat is deliberately excluded from both. It stays good for nothing, and a food
// improvement that reached it would also undermine the founding gate that keeps cities off
// it (AI.CentreCanFeed).
//
// The AI trio has to agree — WorkAvailable offers the work, AI.MoveInner orders it, Settlers
// performs it. Four separate bugs in this project have come from those disagreeing, so the
// agreement is asserted directly.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class DryGroundImprovementTests
	{
		private static (Game g, Player p, Settlers s) ASettlerOn(Terrain terrain, bool masonry = true, bool refining = true)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			// A real civ, NOT Game.CurrentPlayer. On a fresh game CurrentPlayer is the
			// BARBARIANS (slot 0), which is both the wrong owner for a settler test and the
			// one slot where the AI instance cache used to hand back a stale object — the
			// first version of this fixture used it and failed only in the full suite.
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 6);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
			{
				Map.Instance.ChangeTileType(x, y, terrain);
				((BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			// Both the owner AND CurrentPlayer: WorkAvailable asks the owning Player, while
			// Settlers.BuildTerrace/BuildMoistureFarm ask Game.CurrentPlayer — the same
			// idiom BuildRoad uses. The test asserts those two agree, so it has to satisfy both.
			foreach (Player who in new[] { p, g.CurrentPlayer })
			{
				if (masonry)  who.AddAdvance(new Masonry(), false);
				if (refining) who.AddAdvance(new Refining(), false);
			}
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (g, p, s);
		}

		private static void FinishTheJob(Settlers s)
		{
			// Both jobs take several turns; NewTurn ticks the counter and lays the improvement
			// on the turn it reaches zero.
			for (int i = 0; i < 12; i++) s.NewTurn();
		}

		// ── terrace ───────────────────────────────────────────────────────────────

		[Fact]
		public void ATerraceOnHillsAddsFoodWithoutWater()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Hills);
			ITile tile = Map.Instance[40, 25];
			int before = tile.Food;
			Assert.False(tile.AllowIrrigation(), "fixture: these hills should have no water to draw on");

			Assert.True(s.BuildTerrace());
			FinishTheJob(s);

			Assert.True(tile.Terrace, "the terrace was never finished");
			Assert.Equal(before + 1, tile.Food);
		}

		[Fact]
		public void TerracingNeedsMasonry()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Hills, masonry: false);

			Assert.False(s.BuildTerrace());
		}

		[Theory]
		[InlineData(Terrain.Desert)]
		[InlineData(Terrain.SaltFlat)]
		[InlineData(Terrain.Grassland1)]
		public void TerracingIsHillsOnly(Terrain terrain)
		{
			(Game g, Player p, Settlers s) = ASettlerOn(terrain);

			Assert.False(s.BuildTerrace());
		}

		// ── moisture farm ─────────────────────────────────────────────────────────

		[Fact]
		public void AMoistureFarmOnDesertAddsFood()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Desert);
			ITile tile = Map.Instance[40, 25];
			int before = tile.Food;

			Assert.True(s.BuildMoistureFarm());
			FinishTheJob(s);

			Assert.True(tile.MoistureFarm, "the moisture farm was never finished");
			Assert.Equal(before + 1, tile.Food);
		}

		[Fact]
		public void MoistureFarmingNeedsRefining()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Desert, refining: false);

			Assert.False(s.BuildMoistureFarm());
		}

		// The point the user was explicit about: salt flats stay good for nothing.
		[Fact]
		public void ASaltFlatGetsNeitherImprovement()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.SaltFlat);

			Assert.False(s.BuildMoistureFarm());
			Assert.False(s.BuildTerrace());
			Assert.Equal(0, Map.Instance[40, 25].Food);
		}

		// ── the AI trio agrees ────────────────────────────────────────────────────

		[Theory]
		[InlineData(Terrain.Hills,    true,  false)]
		[InlineData(Terrain.Desert,   false, true)]
		[InlineData(Terrain.SaltFlat, false, false)]
		[InlineData(Terrain.Grassland1, false, false)]
		public void TheWorkScanOffersExactlyWhatTheBuilderAccepts(Terrain terrain, bool terrace, bool moisture)
		{
			(Game g, Player p, Settlers s) = ASettlerOn(terrain);
			ITile tile = Map.Instance[40, 25];

			object work = typeof(AI).GetMethod("WorkAvailable",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { tile })!;
			bool offersTerrace  = (bool)work.GetType().GetField("Terrace")!.GetValue(work)!;
			bool offersMoisture = (bool)work.GetType().GetField("MoistureFarm")!.GetValue(work)!;

			Assert.Equal(terrace,  offersTerrace);
			Assert.Equal(moisture, offersMoisture);
			// ...and the builder agrees with the scan, which is the property that matters.
			Assert.Equal(offersTerrace,  s.BuildTerrace());
			Assert.Equal(offersMoisture, s.BuildMoistureFarm());
		}

		// Once built, the scan must stop offering it, or the settler stands there repeating
		// a job that is already done.
		[Fact]
		public void FinishedWorkIsNoLongerOffered()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Hills);
			Assert.True(s.BuildTerrace());
			FinishTheJob(s);

			object work = typeof(AI).GetMethod("WorkAvailable",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { Map.Instance[40, 25] })!;

			Assert.False((bool)work.GetType().GetField("Terrace")!.GetValue(work)!);
		}

		// ── persistence and art ───────────────────────────────────────────────────

		[Fact]
		public void BothSurviveASave()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Hills);
			Map.Instance[40, 25].Terrace = true;
			Map.Instance.ChangeTileType(41, 25, Terrain.Desert);
			Map.Instance[41, 25].MoistureFarm = true;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "dryground.cos");

			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.True(Map.Instance[40, 25].Terrace, "the terrace did not survive the save");
			Assert.True(Map.Instance[41, 25].MoistureFarm, "the moisture farm did not survive the save");
		}

		// Both sprites draw something on their own, so the improvement is visible from the
		// moment it is built rather than waiting on art.
		[Fact]
		public void BothImprovementsHaveAProceduralSprite()
		{
			Sim.EnsureRuntime();

			Assert.NotEmpty(CivOne.Graphics.Sprites.MapTile.Terrace.ToByteArray().Where(b => b != 0));
			Assert.NotEmpty(CivOne.Graphics.Sprites.MapTile.MoistureFarm.ToByteArray().Where(b => b != 0));
		}

		// The art slots ship BLANK for the user to draw into, and a blank grid has to read as
		// an empty slot rather than as an override of nothing: taken literally it would replace
		// the procedural sprite with full transparency, the improvement would vanish from the
		// map, and the only symptom would be a tile that looks unimproved.
		//
		// Pinned at the source rather than exercised. Free.Improvement reads
		// improvement_tiles.txt from Environment.CurrentDirectory, which under the test host is
		// not the project root — so the override is absent either way and a behavioural test
		// here passes whatever the guard does. It did, before this comment replaced it.
		[Fact]
		public void ABlankArtSlotIsTreatedAsAbsent()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "Graphics", "Free.cs"));

			int at = src.IndexOf("public byte[]? Improvement(string name)");
			Assert.True(at > 0, "Free.Improvement has moved or been rewritten");
			string body = src.Substring(at, src.IndexOf("\n\t\t}", at) - at);

			Assert.Contains("if (b != 0) return data;", body);
		}

		// ...and the slots themselves are in the file, or there is nothing to draw into.
		[Theory]
		[InlineData("[terrace]")]
		[InlineData("[moisture_farm]")]
		public void TheArtSlotIsShipped(string section)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);

			string file = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "improvement_tiles.txt"));

			Assert.Contains(section, file);
		}
	}
}
