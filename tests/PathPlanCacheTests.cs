// CivOne tests
//
// Common.GotoStep ran a complete A* per step and returned only the first tile. The
// 2026-08-03 run measured the cost: 63,632 Diplomat moves at 28 ms each, with target
// selection just 1.7% of it — the journey was being re-planned every turn.
//
// GotoStep now keeps the route and walks it. The cache is only worth having if it is
// invisible: the same steps the uncached search would have given, and a fresh search
// whenever the world has moved under it.

using System.Collections.Generic;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class PathPlanCacheTests
	{
		// Open grassland, no cities: nothing to complicate the route.
		private static Player OnOpenGround()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 18; y <= 32; y++)
			for (int x = 30; x <= 55; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = Game.Instance.Players
				.First(x => x is not null && Game.Instance.PlayerNumber(x) != 0
				         && x != Game.Instance.HumanPlayer);
			p.Explore(42, 25, range: 20);
			Sim.ClearTasks();
			return p;
		}

		private static IUnit Spawn(Player p, UnitType type, int x, int y)
			=> Game.Instance.CreateUnit(type, x, y, Game.Instance.PlayerNumber(p))!;

		// Walk a unit to its goal one step at a time, returning the tiles it passed through.
		private static List<(int X, int Y)> Walk(IUnit unit, int gx, int gy, int maxSteps = 60)
		{
			var route = new List<(int, int)>();
			for (int i = 0; i < maxSteps; i++)
			{
				ITile? step = Common.GotoStep(unit, gx, gy);
				if (step is null) break;
				route.Add((step.X, step.Y));
				unit.MovesLeft = unit.Move;
				unit.X = step.X; unit.Y = step.Y;
				if (unit.X == gx && unit.Y == gy) break;
			}
			return route;
		}

		// The property that matters: caching must not change the route.
		[Fact]
		public void ACachedWalk_TakesTheSameRouteAsAnUncachedOne()
		{
			Player p = OnOpenGround();

			// Uncached: a brand-new unit each step, so no plan can be reused.
			var uncached = new List<(int X, int Y)>();
			int cx = 34, cy = 25;
			for (int i = 0; i < 60 && !(cx == 50 && cy == 28); i++)
			{
				IUnit scratch = Spawn(p, UnitType.Diplomat, cx, cy);
				ITile? step = Common.GotoStep(scratch, 50, 28);
				Game.Instance.DisbandUnit(scratch);
				if (step is null) break;
				uncached.Add((step.X, step.Y));
				cx = step.X; cy = step.Y;
			}

			IUnit walker = Spawn(p, UnitType.Diplomat, 34, 25);
			var cached = Walk(walker, 50, 28);

			Assert.NotEmpty(uncached);
			Assert.Equal(uncached, cached);
			Assert.Equal((50, 28), (walker.X, walker.Y));
		}

		// The saving itself: one search for the journey, not one per step.
		[Fact]
		public void WalkingARoute_SearchesOnceNotOncePerStep()
		{
			Player p = OnOpenGround();
			IUnit dip = Spawn(p, UnitType.Diplomat, 34, 25);
			TurnMetrics.Reset();

			var route = Walk(dip, 50, 28);

			Assert.True(route.Count >= 8, $"expected a journey of several steps, got {route.Count}");
			long misses = TurnMetrics.Buckets().Where(b => b.Key == "path:Miss").Sum(b => b.Calls);
			long hits = TurnMetrics.Buckets().Where(b => b.Key == "path:Hit").Sum(b => b.Calls);
			Assert.Equal(1, misses);
			Assert.Equal(route.Count - 1, hits);
		}

		// A new goal is a new journey — the old plan must not be handed out for it.
		[Fact]
		public void ChangingTheGoal_ForcesAFreshSearch()
		{
			Player p = OnOpenGround();
			IUnit dip = Spawn(p, UnitType.Diplomat, 34, 25);
			Common.GotoStep(dip, 50, 28);
			TurnMetrics.Reset();

			ITile? step = Common.GotoStep(dip, 34, 20);   // due north instead of east

			Assert.NotNull(step);
			Assert.True(step!.Y < 25, "a northward goal must not be served the eastward plan");
			Assert.Equal(1, TurnMetrics.Buckets().Where(b => b.Key == "path:Miss").Sum(b => b.Calls));
		}

		// The staleness that matters: a peaceful neighbour parks a unit on our next tile.
		// A Diplomat refuses to enter it, so the cached step is one it will not take.
		[Fact]
		public void AUnitParkedOnTheNextTile_InvalidatesThePlan()
		{
			Player p = OnOpenGround();
			Player other = Game.Instance.Players
				.First(x => x is not null && x != p && Game.Instance.PlayerNumber(x) != 0
				         && x != Game.Instance.HumanPlayer);
			IUnit dip = Spawn(p, UnitType.Diplomat, 34, 25);

			ITile? first = Common.GotoStep(dip, 50, 28);
			Assert.NotNull(first);
			// Block the very tile the plan is about to hand out again.
			Spawn(other, UnitType.Musketeers, first!.X, first.Y);
			TurnMetrics.Reset();

			ITile? second = Common.GotoStep(dip, 50, 28);

			Assert.Equal(1, TurnMetrics.Buckets().Where(b => b.Key == "path:Miss").Sum(b => b.Calls));
			Assert.True(second is null || second.X != first.X || second.Y != first.Y,
				"the plan handed back a step onto a tile the diplomat refuses to enter");
		}

		// A unit moved off its route by something other than the plan — a transport, a hut,
		// a bounced attack — must not be handed the next step of a route it has left.
		[Fact]
		public void AUnitMovedOffPlan_ReplansFromWhereItActuallyIs()
		{
			Player p = OnOpenGround();
			IUnit dip = Spawn(p, UnitType.Diplomat, 34, 25);
			Common.GotoStep(dip, 50, 28);
			dip.X = 36; dip.Y = 20;   // teleported clear of the route
			TurnMetrics.Reset();

			ITile? step = Common.GotoStep(dip, 50, 28);

			Assert.NotNull(step);
			Assert.Equal(1, TurnMetrics.Buckets().Where(b => b.Key == "path:Miss").Sum(b => b.Calls));
			Assert.True(Common.DistanceToTile(36, 20, step!.X, step.Y) == 1,
				"the next step must adjoin where the unit actually stands");
		}

		// A unit with more than one move point calls GotoStep repeatedly within a turn.
		[Fact]
		public void AMultiMoveUnit_KeepsItsPlanAcrossStepsInOneTurn()
		{
			Player p = OnOpenGround();
			IUnit knight = Spawn(p, UnitType.Knights, 34, 25);
			TurnMetrics.Reset();

			var route = Walk(knight, 48, 25);

			Assert.True(route.Count >= 8);
			Assert.Equal(1, TurnMetrics.Buckets().Where(b => b.Key == "path:Miss").Sum(b => b.Calls));
		}
	}
}
