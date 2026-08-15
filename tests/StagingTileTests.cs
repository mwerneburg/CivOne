// CivOne tests
//
// Choosing a staging square, without walking the whole army eight times.
//
// ITile.Units is Game.GetUnits(x, y): it scans EVERY unit in the game, sorts the matches and
// allocates an array. StagingTile called it twice for each of eight neighbours, so picking
// somewhere to mass cost 16 full scans, 16 sorts and 16 allocations — and every attacking unit
// does this on every move.
//
// Measured at turn 655 of a live 2,045-unit game: Armor alone burned 55.4 seconds over 3,628
// moves in ONE turn, 84% of all unit-movement time, out of a 66-second turn. The cost is
// (attackers x all units), so it worsened as the world filled — 59s at turn 650, 66s five
// turns later.
//
// These tests exist because the rewrite must not change the ANSWER, only the cost: the
// candidate order, the strictly-greater comparison and therefore the tie-break are all load
// bearing.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class StagingTileTests
	{
		private static (Game g, Player p, City target) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 10);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			City target = g.AddCity(foe, 0, 40, 25)!;
			target.Size = 4;
			Sim.ClearTasks();
			return (g, p, target);
		}

		private static ITile? Staging(Player p, City target)
			=> (ITile?)typeof(AI).GetMethod("StagingTile",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { target });

		private static void Put(Game g, Player owner, UnitType type, int x, int y)
			=> g.CreateUnit(type, x, y, g.PlayerNumber(owner));

		// With nothing anywhere, it still picks a neighbour — the first eligible one, since
		// every count is zero and the comparison is strictly-greater.
		[Fact]
		public void AnEmptyApproachStillYieldsATile()
		{
			(Game g, Player p, City target) = AWorld();

			ITile? tile = Staging(p, target);

			Assert.NotNull(tile);
			Assert.True(System.Math.Abs(tile!.X - target.X) <= 1 && System.Math.Abs(tile.Y - target.Y) <= 1);
			Assert.NotEqual((target.X, target.Y), (tile.X, tile.Y));
		}

		// The point of the function: gather where our attackers already are.
		[Fact]
		public void ItMassesWhereOurAttackersAlreadyStand()
		{
			(Game g, Player p, City target) = AWorld();
			Put(g, p, UnitType.Armor, 41, 26);
			Put(g, p, UnitType.Armor, 41, 26);
			Put(g, p, UnitType.Armor, 39, 24);   // fewer here

			ITile? tile = Staging(p, target);

			Assert.Equal((41, 26), (tile!.X, tile.Y));
		}

		// A tile holding ANY foreign unit is not a staging square, however many of ours are on
		// it — you do not mass on top of the enemy.
		[Fact]
		public void ATileHoldingEnemiesIsRefused()
		{
			(Game g, Player p, City target) = AWorld();
			Player foe = Game.Instance.GetPlayer(target.Owner);
			Put(g, p, UnitType.Armor, 41, 26);
			Put(g, p, UnitType.Armor, 41, 26);
			Put(g, foe, UnitType.Militia, 41, 26);   // now poisoned
			Put(g, p, UnitType.Armor, 39, 24);

			ITile? tile = Staging(p, target);

			Assert.Equal((39, 24), (tile!.X, tile.Y));
		}

		// Only ATTACKERS count toward massing — a defender parked next door is not a staging
		// force. This distinguishes the rewrite from "count our units".
		[Fact]
		public void OnlyAttackersCountTowardTheChoice()
		{
			(Game g, Player p, City target) = AWorld();
			for (int i = 0; i < 3; i++) Put(g, p, UnitType.Musketeers, 41, 26);   // defenders
			Put(g, p, UnitType.Armor, 39, 24);                                    // one attacker

			ITile? tile = Staging(p, target);

			Assert.Equal((39, 24), (tile!.X, tile.Y));
		}

		// The tie-break is load bearing: equal counts keep the tile seen FIRST in the dy/dx
		// scan order, which is (-1,-1) — north-west. A rewrite that reordered the candidates
		// would silently move every army in the game.
		[Fact]
		public void EqualCountsKeepTheFirstTileInScanOrder()
		{
			(Game g, Player p, City target) = AWorld();
			Put(g, p, UnitType.Armor, 39, 24);   // north-west, first in scan order
			Put(g, p, UnitType.Armor, 41, 26);   // south-east, same count

			ITile? tile = Staging(p, target);

			Assert.Equal((39, 24), (tile!.X, tile.Y));
		}

		// The cost, pinned on the source. A timing test would flake — WaterBodyCostTests does,
		// twice in one day — and the claim here is structural anyway: StagingTile must not
		// touch ITile.Units, which is a full scan, sort and allocation per call.
		[Fact]
		public void ItDoesNotWalkTheArmyPerTile()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "AI.Strategy.cs"));

			int at = src.IndexOf("private ITile? StagingTile(City target)");
			Assert.True(at > 0, "StagingTile has moved or been renamed");
			string body = src.Substring(at, src.IndexOf("\n\t\t}", at) - at);

			Assert.DoesNotContain("tile.Units", body);
			Assert.Contains("foreach (IUnit u in Game.GetUnits())", body);
		}

		// Water is not a staging square.
		[Fact]
		public void OceanNeighboursAreSkipped()
		{
			(Game g, Player p, City target) = AWorld();
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
				if (dx != 0 || dy != 0)
					Map.Instance.ChangeTileType(target.X + dx, target.Y + dy, Terrain.Ocean);
			Map.Instance.ChangeTileType(39, 24, Terrain.Grassland1);   // one dry approach
			Map.Instance.RecalculateContinentsIfDirty();

			ITile? tile = Staging(p, target);

			Assert.Equal((39, 24), (tile!.X, tile.Y));
		}

		// ...and a city with no dry approach at all has nowhere to stage.
		[Fact]
		public void ACityRingedByWaterHasNoStagingTile()
		{
			(Game g, Player p, City target) = AWorld();
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
				if (dx != 0 || dy != 0)
					Map.Instance.ChangeTileType(target.X + dx, target.Y + dy, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			Assert.Null(Staging(p, target));
		}
	}
}
