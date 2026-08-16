// CivOne tests
//
// How big a ship the AI tries to build.
//
// SpaceshipStructuresNeeded scales the hull with engines and module sets: 15 structurals for
// the minimum ship, 51 for a maxed one — roughly 2,500 shields against 10,500. Nothing chose
// that. Consider() keeps the first entry per type and the Diaspora block listed structural,
// component, module in that order, so cities built structural to its cap, then components,
// and only reached modules at the end — by which point the requirement had climbed to 51.
//
// Measured across two finished 750-turn runs: the Chinese completed a maxed ship on turn 744
// of 750, while the Japanese (40 of 51) and the Maori (42 of 51) ran out of game holding
// maxed components and modules. A ship one structural short scores exactly what no ship does.
//
// So the civ now picks the largest hull it can plausibly finish at its current output and
// stops there. Modules stay LAST in the build order, so the launch condition is completed by
// the final module and the ship launches at its target rather than at the bare minimum.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class SpaceshipTargetTests
	{
		// An empire of `cities` cities, each producing roughly `shields` per turn.
		//
		// Laid out on a grid: the interesting empires here run to a few dozen cities, which
		// does not fit on one row, and the budget is a share of output — so demonstrating that
		// the target scales at all takes an empire big enough to clear the next rung. A dozen
		// grassland cities make about 66 shields a turn between them; the hull above the
		// minimum costs 3,760.
		private static (Game g, Player p) AnEmpire(int cities, int size)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 15);
			for (int y = 16; y <= 34; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			int made = 0;
			for (int cy = 17; cy <= 33 && made < cities; cy += 2)
			for (int cx = 21; cx <= 59 && made < cities; cx += 2)
			{
				City c = g.AddCity(p, (byte)made, cx, cy);
				if (c is null) continue;
				c.Size = (byte)size;
				made++;
			}
			Assert.Equal(cities, made);
			Sim.ClearTasks();
			return (g, p);
		}

		private static (int component, int module) TargetFor(Player p)
			=> AI.Instance(p).SpaceshipTarget();

		// A civ with almost nothing does not embark on a 10,500-shield hull. The floor is the
		// launchable minimum — the smallest ship that flies beats the biggest that does not.
		[Fact]
		public void APoorCivAimsForTheMinimumShip()
		{
			(Game g, Player p) = AnEmpire(cities: 1, size: 1);

			(int comp, int module) = TargetFor(p);

			Assert.Equal(2, comp);
			Assert.Equal(3, module);
		}

		// ...and a richer empire reaches materially higher.
		//
		// Asserted as a comparison rather than "the big one maxes out": no fixture here is
		// industrial in the sense the late game is — twelve size-20 cities on bare grassland
		// make about 66 shields a turn between them, against the 10,500 a maxed hull costs,
		// because a test city works few tiles and has no Factory. The claim that matters is
		// that the target SCALES with output, which is the whole mechanism.
		[Fact]
		public void ARicherEmpireAimsHigher()
		{
			(Game _, Player poor) = AnEmpire(cities: 1, size: 4);
			int smallHull = HullCost(TargetFor(poor));

			// 40 cities, not 12: the budget is a third of output, and a dozen test cities on
			// bare grassland cannot reach even the second-smallest hull, so the comparison was
			// minimum-against-minimum and failed on correct code.
			(Game __, Player rich) = AnEmpire(cities: 40, size: 20);
			int bigHull = HullCost(TargetFor(rich));

			Assert.True(bigHull > smallHull,
				$"output made no difference to the target: {smallHull} vs {bigHull} shields");
		}

		private static int HullCost((int comp, int module) hull)
			=> Game.SpaceshipStructuresNeeded(hull.comp, hull.module) * new SSStructural().Price * 10
			 + hull.comp   * new SSComponent().Price * 10
			 + hull.module * new SSModule().Price * 10;

		// The target must never be unlaunchable. Every hull it returns has to satisfy the
		// launch condition in Game.cs: at least one engine and one module set.
		[Theory]
		[InlineData(1, 1)]
		[InlineData(3, 6)]
		[InlineData(8, 12)]
		[InlineData(12, 20)]
		public void EveryTargetIsAHullThatCanActuallyLaunch(int cities, int size)
		{
			(Game g, Player p) = AnEmpire(cities, size);

			(int comp, int module) = TargetFor(p);

			Assert.True(comp >= 2, $"{comp} components cannot make an engine");
			Assert.True(module >= 3, $"{module} modules cannot make a module set");
			Assert.Equal(0, comp % 2);
			Assert.Equal(0, module % 3);
		}

		// The point of the whole change: a middling civ aims at something SMALLER than maxed.
		// Without this the test above would pass on a function that always returned the
		// maximum, which is precisely the behaviour being replaced.
		[Fact]
		public void AMiddlingCivAimsBelowTheMaximum()
		{
			(Game g, Player p) = AnEmpire(cities: 3, size: 6);

			(int comp, int module) = TargetFor(p);

			Assert.True(comp < Game.MAX_SS_COMPONENT || module < Game.MAX_SS_MODULE,
				$"a 3-city empire set out to build the full 51/16/12 hull ({comp} comp, {module} mod)");
		}

		// Progress counts against the target: parts already built are not paid for twice, so
		// a civ that has been building all game can still afford to finish a bigger hull than
		// it could have started.
		[Fact]
		public void PartsAlreadyBuiltCountTowardTheBudget()
		{
			(Game g, Player p) = AnEmpire(cities: 3, size: 6);
			byte me = g.PlayerNumber(p);
			(int before, int _) = TargetFor(p);

			g.Progress(me).SpaceshipStructural = Game.MaxSpaceshipStructural;
			g.Progress(me).SpaceshipComponent  = Game.MAX_SS_COMPONENT;
			g.Progress(me).SpaceshipModule     = Game.MAX_SS_MODULE - 3;

			(int after, int afterModule) = TargetFor(p);

			Assert.True(after > before || afterModule == Game.MAX_SS_MODULE,
				"a nearly-finished ship should still be worth finishing");
		}

		// ── the target actually binds ────────────────────────────────────────────

		// The last-resort production fallback is how a civ NOT on the Diaspora path builds a
		// ship at all: a built-out city with nothing left to make picks parts until the hull
		// is maxed, which is how the Chinese — a Commerce civ — finished 51/16/12. The target
		// has to bind there too, or it constrains only the civs already trying.
		[Fact]
		public void TheFallbackWillNotBuildPastTheTarget()
		{
			(Game g, Player p) = AnEmpire(cities: 1, size: 1);
			byte me = g.PlayerNumber(p);
			(int targetComp, int targetModule) = TargetFor(p);
			// Already at target on every part: nothing more should be wanted.
			g.Progress(me).SpaceshipStructural = Game.SpaceshipStructuresNeeded(targetComp, targetModule);
			g.Progress(me).SpaceshipComponent  = targetComp;
			g.Progress(me).SpaceshipModule     = targetModule;

			var wants = typeof(AI).GetMethod("WantsSpaceshipPart",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			AI ai = AI.Instance(p);

			Assert.False((bool)wants.Invoke(ai, new object[] { new SSStructural() })!);
			Assert.False((bool)wants.Invoke(ai, new object[] { new SSComponent() })!);
			Assert.False((bool)wants.Invoke(ai, new object[] { new SSModule() })!);
			// ...and it never filters anything that is not a ship part.
			Assert.True((bool)wants.Invoke(ai, new object[] { new Barracks() })!);
		}

		// Below the target, the parts are still wanted — the guard must not be a blanket
		// refusal, which would stop every AI ship in the game.
		[Fact]
		public void BelowTheTargetThePartsAreStillWanted()
		{
			(Game g, Player p) = AnEmpire(cities: 6, size: 12);
			byte me = g.PlayerNumber(p);
			g.Progress(me).SpaceshipStructural = 0;
			g.Progress(me).SpaceshipComponent  = 0;
			g.Progress(me).SpaceshipModule     = 0;

			var wants = typeof(AI).GetMethod("WantsSpaceshipPart",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			AI ai = AI.Instance(p);

			Assert.True((bool)wants.Invoke(ai, new object[] { new SSStructural() })!);
			Assert.True((bool)wants.Invoke(ai, new object[] { new SSComponent() })!);
			Assert.True((bool)wants.Invoke(ai, new object[] { new SSModule() })!);
		}

		// The failure this change exists to prevent, stated as an invariant: the structure
		// requirement of the chosen hull must be reachable. 51 structurals behind maxed
		// components and modules is exactly the position the Japanese and Maori died in.
		[Theory]
		[InlineData(1, 1)]
		[InlineData(3, 6)]
		[InlineData(6, 12)]
		public void TheChosenHullIsAffordableAtCurrentOutput(int cities, int size)
		{
			(Game g, Player p) = AnEmpire(cities, size);

			(int comp, int module) = TargetFor(p);
			int cost = Game.SpaceshipStructuresNeeded(comp, module) * new SSStructural().Price * 10
			         + comp   * new SSComponent().Price * 10
			         + module * new SSModule().Price * 10;
			int perTurn = p.Cities.Sum(c => System.Math.Max(0, c.ShieldIncome));

			// The minimum hull is the floor and is allowed to be unaffordable — a civ that
			// cannot afford even that simply never finishes, and there is nothing smaller to
			// aim at. Anything BIGGER than the floor must be within the horizon.
			if (comp > 2 || module > 3)
				Assert.True(cost <= perTurn * 100,
					$"chose a {comp}/{module} hull costing {cost} on {perTurn} shields/turn");
		}
	}
}
