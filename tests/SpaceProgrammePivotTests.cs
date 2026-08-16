// CivOne tests
//
// What a civ does after it launches, and what happens when the ship is lost.
//
// Three things had to hold together. Mission Control lived inside the Diaspora production
// branch, so a civ that pivoted away after launching would never rebuild a captured one —
// and losing it resets the twenty-turn countdown to zero, which is the win thrown away by a
// change of ambition. The Diaspora path kept scoring after launch, steering a civ by a plan
// it had already carried out. And the breach comment promised "founding the colony again
// means building another ship" while the launch turn and part counters were never cleared,
// so a civ that lost its colony was quietly barred from ever trying again.

using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class SpaceProgrammePivotTests
	{
		// `wantsSpace` picks a civilization PreferredPathFor sends to Diaspora (Russians,
		// Maori, Greeks). Without that the pivot tests were vacuous: an authored preference
		// decides the path outright, so a civ that would never choose Diaspora satisfies
		// "not Diaspora" whatever the programme is doing. The negative check caught it by
		// killing nothing at all.
		private static (Game g, Player p, City c) ACivWithACity(bool wantsSpace = false)
		{
			// The full roster: an 8-civ game may contain no civilization PreferredPathFor
			// sends to Diaspora at all, and the pivot tests need one that genuinely wants it.
			Sim.NewGame(width: 80, height: 50, competition: 17);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = wantsSpace
				? g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
					&& x.Civilization is Civilizations.Russian or Civilizations.Maori or Civilizations.Greek)
				: g.Players.First(x => x is not null && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(45, 25, range: 30);
			p.AddAdvance(new SpaceFlight(), false);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 8;
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (System.Math.Abs(dx) == 2 && System.Math.Abs(dy) == 2) continue;
				ITile t = Map.Instance[40 + dx, 25 + dy];
				if (t is not null && !t.IsOcean) t.Road = true;
			}
			c.ResetResourceTiles();
			g.CreateUnit(Enums.UnitType.Militia, 40, 25, g.PlayerNumber(p), false);
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static List<IProduction> PlanFor(Player p, City c)
		{
			var plan = new List<IProduction>();
			var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			var method = typeof(AI).GetMethod("PlanProductionInto", flags);
			var stanceType = typeof(AI).GetNestedType("StrategyStance", System.Reflection.BindingFlags.NonPublic);
			return (List<IProduction>)method!.Invoke(AI.Instance(p),
				new object[] { plan, c, System.Enum.Parse(stanceType!, "Develop") })!;
		}

		private static string PathOf(Player p)
		{
			var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			return typeof(AI).GetProperty("Path", flags)!.GetValue(AI.Instance(p))!.ToString()!;
		}

		// ── the lifeline ─────────────────────────────────────────────────────────

		// A civ with a colony rebuilds a lost Mission Control WHATEVER it is now pursuing.
		// This is the trap the pivot would otherwise spring.
		[Fact]
		public void ACivWithAColonyRebuildsMissionControlOffThePath()
		{
			(Game g, Player p, City c) = ACivWithACity();
			byte me = g.PlayerNumber(p);
			g.Progress(me).ColonyFounded = true;                      // landed
			g.SpaceshipArrivalTurn[me] = 0;                  // no longer in flight

			Assert.NotEqual("Diaspora", PathOf(p));          // fixture: not on the science path
			Assert.Contains(PlanFor(p, c), x => x is MissionControl);
		}

		// Same while the ship is still crossing — the lifeline has to be standing when it
		// lands, not started afterwards.
		[Fact]
		public void ACivWithAShipInFlightBuildsMissionControl()
		{
			(Game g, Player p, City c) = ACivWithACity();
			byte me = g.PlayerNumber(p);
			g.SpaceshipArrivalTurn[me] = g.GameTurn + 30;

			Assert.Contains(PlanFor(p, c), x => x is MissionControl);
		}

		// ...and a civ with neither does not spend on it.
		[Fact]
		public void ACivWithNoProgrammeDoesNotBuildMissionControl()
		{
			(Game g, Player p, City c) = ACivWithACity();

			Assert.DoesNotContain(PlanFor(p, c), x => x is MissionControl);
		}

		// ── the pivot ────────────────────────────────────────────────────────────

		// Once the ship is flying there is nothing left to build for it, so the ambition
		// must move on rather than steer by a plan already carried out.
		[Fact]
		public void ALaunchedCivLeavesTheDiasporaPath()
		{
			(Game g, Player p, City c) = ACivWithACity(wantsSpace: true);
			byte me = g.PlayerNumber(p);
			g.SETISignalReceived = true;

			// Precondition, and the thing that makes this test mean anything: with a ship
			// still to build, this civ reaches for Diaspora.
			Assert.Equal("Diaspora", PathOf(p));

			g.SpaceshipArrivalTurn[me] = g.GameTurn + 30;

			Assert.NotEqual("Diaspora", PathOf(p));
		}

		// And a colony on the ground is equally under way.
		[Fact]
		public void ACivWithALandedColonyLeavesTheDiasporaPath()
		{
			(Game g, Player p, City c) = ACivWithACity(wantsSpace: true);
			byte me = g.PlayerNumber(p);
			g.SETISignalReceived = true;
			Assert.Equal("Diaspora", PathOf(p));

			g.Progress(me).ColonyFounded = true;

			Assert.NotEqual("Diaspora", PathOf(p));
		}

		// ── losing it ────────────────────────────────────────────────────────────

		// The whole of "resume if something goes wrong": with the ship gone and no colony,
		// there is a ship to build again, so the path becomes available again.
		[Fact]
		public void LosingTheProgrammeMakesDiasporaScoreAgain()
		{
			(Game g, Player p, City c) = ACivWithACity();
			byte me = g.PlayerNumber(p);
			g.SETISignalReceived = true;

			g.SpaceshipArrivalTurn[me] = g.GameTurn + 30;
			var underWay = typeof(AI).GetMethod("ProgrammeUnderWay",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.True((bool)underWay!.Invoke(AI.Instance(p), null)!);

			g.ResetSpaceProgramme(me);                       // the pickets took it
			Assert.False((bool)underWay!.Invoke(AI.Instance(p), null)!,
				"a civ with no ship and no colony still reads as having a programme under way");
		}

		// The promise the breach comment makes: starting over must actually be possible.
		// The launch turn and the part counters both bar it if they survive.
		[Fact]
		public void ALostProgrammeCanBeBuiltAgain()
		{
			(Game g, Player p, City c) = ACivWithACity();
			byte me = g.PlayerNumber(p);
			g.SpaceshipLaunchTurn[me] = 100;
			g.SpaceshipArrivalTurn[me] = 140;
			g.SpaceshipStructural[me] = 51;
			g.SpaceshipComponent[me] = 16;
			g.SpaceshipModule[me] = 12;

			g.ResetSpaceProgramme(me);

			Assert.Equal(0, g.SpaceshipLaunchTurn[me]);
			Assert.Equal(0, g.SpaceshipArrivalTurn[me]);
			Assert.Equal(0, g.SpaceshipStructural[me]);
			Assert.Equal(0, g.SpaceshipComponent[me]);
			Assert.Equal(0, g.SpaceshipModule[me]);
		}
	}
}
