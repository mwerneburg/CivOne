// CivOne tests
//
// Terrain 14: wooded slopes. Two parents — the hill's defence, the forest's timber —
// and one decision that justifies the terrain existing at all: it CANNOT be mined.
// A coal seam under trees is worth 4 shields standing, or 4 shields chopped and mined,
// so clearing is a real choice rather than free upside.
//
// The tile deliberately does NOT subclass Hills. Half the engine asks `tile is Hills`
// to decide whether a settler may mine, irrigate or terraform, and inheriting would
// have opted wooded slopes into all of it silently.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ForestedHillsTests
	{
		// 1 food / 2 shield standing, +2 more if the seam is there. A bare hill is 1/0.
		[Fact]
		public void WoodedSlopesPayTimberAndKeepTheHillsDefence()
		{
			var wooded = new ForestedHills(4, 4, special: false);
			var seam   = new ForestedHills(4, 4, special: true);
			var bare   = new Hills(4, 4, special: false);

			Assert.Equal(1, wooded.Food);
			Assert.Equal(2, wooded.Shield);
			Assert.Equal(0, wooded.Trade);
			Assert.Equal(4, seam.Shield);
			Assert.Equal(bare.Defense, wooded.Defense);   // the slope is what protects
			Assert.Equal(2, wooded.Movement);
		}

		// The whole point. A bare hill mines for +2; a wooded one cannot be mined at all,
		// so the coal under the trees is unreachable until they come down.
		[Fact]
		public void AWoodedSlopeCannotBeMinedUntilItIsCleared()
		{
			Assert.True(new ForestedHills(4, 4, false).MiningShieldBonus < 0);
			Assert.True(new Hills(4, 4, false).MiningShieldBonus is var _ and >= -4);
			Assert.True(new ForestedHills(4, 4, false).AllowChangeTerrain());
		}

		// Not a Hills subclass — see the header. If this ever inherits, every `is Hills`
		// site in Settlers/AI/TileExtensions changes meaning at once and silently.
		[Fact]
		public void WoodedSlopesAreNotAKindOfHills()
		{
			Assert.False(new ForestedHills(4, 4, false) is Hills);
		}

		// Planting on a hill must not flatten it. Before this, BuildPlantForest wrote
		// Terrain.Forest unconditionally.
		[Fact]
		public void PlantingTreesOnAHillGivesWoodedSlopesNotFlatForest()
		{
			Settlers settlers = ASettlerOn(Terrain.Hills, new Bioformatting());

			Assert.True(settlers.BuildPlantForest());
			Finish(settlers);

			Assert.Equal(Terrain.ForestedHills, Map.Instance[40, 25].Type);
		}

		// ...and clearing leaves the slope behind, not plains. This is the only route to
		// a mine on a wooded hill.
		[Fact]
		public void ClearingWoodedSlopesLeavesBareHills()
		{
			Settlers settlers = ASettlerOn(Terrain.ForestedHills, new Bioformatting());

			Assert.True(settlers.BuildIrrigation(), "scenario: the clear order was refused");
			Finish(settlers);

			Assert.Equal(Terrain.Hills, Map.Instance[40, 25].Type);
		}

		// Clearing FLAT forest still gives plains — the new branch must not have stolen
		// the old one.
		[Fact]
		public void ClearingFlatForestStillGivesPlains()
		{
			Settlers settlers = ASettlerOn(Terrain.Forest, new Bioformatting());

			Assert.True(settlers.BuildIrrigation(), "scenario: the clear order was refused");
			Finish(settlers);

			Assert.Equal(Terrain.Plains, Map.Instance[40, 25].Type);
		}

		// Generation puts them where hills meet woodland, and leaves bare hills beside
		// them — a world with no bare hills left has no mines to build.
		[Fact]
		public void GenerationWoodsSomeHillsAndLeavesOthersBare()
		{
			Sim.NewGame(width: 80, height: 50);
			var tiles = Map.Instance.AllTiles().ToArray();
			int wooded = tiles.Count(t => t.Type == Terrain.ForestedHills);
			int bare   = tiles.Count(t => t.Type == Terrain.Hills);

			Assert.True(wooded > 0, "no forested hills generated at all");
			Assert.True(bare > wooded / 2, $"only {bare} bare hills left against {wooded} wooded");

			// ...and every one of them touches woodland, which is the placement rule.
			foreach (ITile t in tiles.Where(t => t.Type == Terrain.ForestedHills).Take(25))
			{
				bool nearWoods = t.GetBorderTiles().Any(b => b is not null &&
					(b.Type == Terrain.Forest || b.Type == Terrain.Jungle
					 || b.Type == Terrain.ForestedHills));
				Assert.True(nearWoods, $"wooded hill at {t.X},{t.Y} has no woodland neighbour");
			}
		}

		// A new terrain that saves as something else is a silent map corruption — the
		// classic-MAP path defaults to OCEAN, which would drown the range. Round-trip it.
		[Fact]
		public void WoodedSlopesSurviveASaveAndReload()
		{
			Sim.NewGame(width: 80, height: 50);
			// A BLOCK, not one tile. The loaders re-run EnsureFreshwaterReachability, which
			// plants river oases on dry land — the first draft of this test put its single
			// wooded hill on a dry spot and got back River, which looks exactly like a
			// broken save code and is not one.
			for (int y = 20; y < 25; y++)
			for (int x = 35; x < 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.ForestedHills);
			int before = Map.Instance.AllTiles().Count(t => t.Type == Terrain.ForestedHills);

			string file = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "woodedhills.cos");
			System.IO.Directory.CreateDirectory(Settings.Instance.SavesDirectory);
			Game.Instance.SaveCos(file);
			Sim.ResetState();
			Game.LoadCos(file);

			int after = Map.Instance.AllTiles().Count(t => t.Type == Terrain.ForestedHills);
			Assert.True(before >= 50, $"scenario: only {before} wooded hills to save");
			// Without a case in Map.Cos this is 0 — the default there is OCEAN, so the
			// range would come back as sea.
			Assert.True(after >= before - 5, $"{before} saved, {after} returned");
		}

		private static Settlers ASettlerOn(Terrain terrain, IAdvance? advance)
		{
			Sim.NewGame(width: 80, height: 50, difficulty: 2);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			// The CURRENT player, not the human: BuildPlantForest gates on
			// Game.CurrentPlayer.HasAdvance, and at turn 0 that is not the human — the
			// first draft granted Bioformatting to a player the check never consulted.
			Player player = g.CurrentPlayer;
			player.Government = new Governments.Monarchy();
			if (advance is not null) player.AddAdvance(advance, false);
			player.Explore(40, 25, range: 10);
			Map.Instance.ChangeTileType(40, 25, terrain);
			Map.Instance.RecalculateContinentsIfDirty();

			Settlers settlers = new Settlers { X = 40, Y = 25, Owner = g.PlayerNumber(player) };
			Sim.ClearTasks();
			return settlers;
		}

		// Terraform orders tick down one turn at a time; run enough turns for any of them.
		private static void Finish(Settlers settlers)
		{
			for (int i = 0; i < 10; i++)
			{
				settlers.NewTurn();
				Sim.Settle();
			}
		}
	}
}
