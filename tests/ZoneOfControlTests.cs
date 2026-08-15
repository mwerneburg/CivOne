// CivOne tests
//
// Zone of control, and the cost of asking about it.
//
// The rule: a land unit may not step from a tile bordered by an enemy FIELD unit to another
// tile also bordered by one. Exemptions — the destination already holds one of ours, either
// tile is ocean or a city, the mover is a Diplomat, Caravan or Explorer, and a garrison
// projects no ZOC (only units in the open do).
//
// The test asked that question with `GetBorderTiles()...SelectMany(t => t.Units)`, three times.
// ITile.Units is Game.GetUnits(x, y): a scan of EVERY unit in the game, plus a sort and an
// allocation. Eight border tiles is eight scans; three chains is up to twenty-four — paid by
// every land unit on every step.
//
// Measured over turns 663-668 of a live 2,121-unit game, with an AI move split into phases:
// move:MoveTo cost 15.84 ms a call and 63.4 seconds of a 95-second turn, against 0.68 ms for
// pathfinding and 0.41 ms for choosing a mission.
//
// These tests pin the RULE, not the implementation, and they pass against both the old chains
// and the single pass that replaced them. That equivalence is the point: the rewrite is
// allowed to change the cost and nothing else.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ZoneOfControlTests
	{
		// Open ground, two players at war, mover at (40,25) stepping east to (41,25).
		private static (Game g, Player p, Player foe, IUnit mover) AFrontier()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 10);
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			p.DeclareWar(foe);
			IUnit mover = g.CreateUnit(UnitType.Musketeers, 40, 25, g.PlayerNumber(p))!;
			mover.MovesLeft = mover.Move;
			Sim.ClearTasks();
			return (g, p, foe, mover);
		}

		private static bool StepsEast(IUnit mover)
		{
			bool moved = mover.MoveTo(1, 0);
			Sim.Settle();
			return moved;
		}

		// Nothing hostile anywhere: the step is allowed.
		[Fact]
		public void AnUnwatchedStepIsAllowed()
		{
			(Game g, Player p, Player foe, IUnit mover) = AFrontier();

			Assert.True(StepsEast(mover));
		}

		// The rule itself: both tiles watched by field units, so the step is refused.
		[Fact]
		public void SteppingBetweenTwoWatchedTilesIsRefused()
		{
			(Game g, Player p, Player foe, IUnit mover) = AFrontier();
			g.CreateUnit(UnitType.Militia, 40, 24, g.PlayerNumber(foe));   // borders the origin
			g.CreateUnit(UnitType.Militia, 41, 26, g.PlayerNumber(foe));   // borders the target

			Assert.False(StepsEast(mover));
		}

		// One watcher is not a zone of control — only the origin is covered.
		[Fact]
		public void OneWatchedTileIsNotEnough()
		{
			(Game g, Player p, Player foe, IUnit mover) = AFrontier();
			g.CreateUnit(UnitType.Militia, 39, 24, g.PlayerNumber(foe));   // borders origin only

			Assert.True(StepsEast(mover));
		}

		// Our own unit on the destination breaks the lock — you may always reinforce.
		[Fact]
		public void OurOwnUnitOnTheDestinationBreaksIt()
		{
			(Game g, Player p, Player foe, IUnit mover) = AFrontier();
			g.CreateUnit(UnitType.Militia, 40, 24, g.PlayerNumber(foe));
			g.CreateUnit(UnitType.Militia, 41, 26, g.PlayerNumber(foe));
			g.CreateUnit(UnitType.Musketeers, 41, 25, g.PlayerNumber(p));   // ours, on the target

			Assert.True(StepsEast(mover));
		}

		// A garrison projects nothing. This is the clause that keeps an army able to approach
		// a defended city, and it is the one most easily lost in a rewrite.
		[Fact]
		public void AGarrisonProjectsNoZoneOfControl()
		{
			(Game g, Player p, Player foe, IUnit mover) = AFrontier();
			City theirs = g.AddCity(foe, 0, 41, 26)!;   // borders the TARGET only
			theirs.Size = 4;
			g.CreateUnit(UnitType.Militia, 41, 26, g.PlayerNumber(foe));   // IN the city
			// (39,24) borders the ORIGIN only. The first version of this used (40,24), which
			// borders origin AND target — so that one unit blocked the step by itself and the
			// garrison was never the deciding factor. Both implementations failed it
			// identically, which is what showed the fixture was at fault rather than the code.
			g.CreateUnit(UnitType.Militia, 39, 24, g.PlayerNumber(foe));   // in the open

			Assert.True(StepsEast(mover));
		}

		// Diplomats, Caravans and Explorers ignore ZOC entirely.
		[Theory]
		[InlineData(UnitType.Diplomat)]
		[InlineData(UnitType.Caravan)]
		[InlineData(UnitType.Explorer)]
		public void CiviliansIgnoreIt(UnitType type)
		{
			(Game g, Player p, Player foe, IUnit soldier) = AFrontier();
			g.CreateUnit(UnitType.Militia, 40, 24, g.PlayerNumber(foe));
			g.CreateUnit(UnitType.Militia, 41, 26, g.PlayerNumber(foe));
			IUnit civilian = g.CreateUnit(type, 40, 25, g.PlayerNumber(p))!;
			civilian.MovesLeft = civilian.Move;
			Sim.ClearTasks();

			Assert.True(StepsEast(civilian));
		}

		// Map wrapping: the adjacency test has to agree with GetBorderTiles across the seam,
		// or units near x=0 obey a different rule from everyone else.
		[Fact]
		public void ItWorksAcrossTheMapSeam()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			p.Explore(0, 25, range: 10);
			for (int y = 20; y <= 30; y++)
			for (int x = -4; x <= 4; x++)
				Map.Instance.ChangeTileType((x + 80) % 80, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			p.DeclareWar(foe);
			IUnit mover = g.CreateUnit(UnitType.Musketeers, 79, 25, g.PlayerNumber(p))!;
			mover.MovesLeft = mover.Move;
			// Watchers on the far side of the seam, adjacent to origin (79) and target (0).
			g.CreateUnit(UnitType.Militia, 78, 24, g.PlayerNumber(foe));
			g.CreateUnit(UnitType.Militia, 1, 26, g.PlayerNumber(foe));
			Sim.ClearTasks();

			Assert.False(StepsEast(mover));
		}
	}
}
