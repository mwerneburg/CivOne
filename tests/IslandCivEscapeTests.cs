// CivOne tests
//
// Getting an island civ off its island, and folding a stranded settler back into a city.
//
// Neither of these is a new rule. Both are rules that existed and could never be reached.
//
// The boat: PlanProductionInto already offered a Longboat to a boxed-in coastal city, and
// BoxedIn() was true on 35 of the Maori's 63 logged production decisions — so the boat went
// into the plan and stayed there. Consider() keeps the first entry per type and
// CityProduction builds plan[0], so POSITION is priority, and the boat sat below the whole
// infrastructure chain. A city with any building left to make never got that far. Measured
// across three complete 750-turn runs: not one boat of any kind, the same 8 cities from turn
// 313 to 749, and a capital that grew to size 17 building Library, Aqueduct, Harbour,
// Observatory, Hospital, Neural Lab and Mass Transit instead.
//
// The settler: Orders.CreateCity has always turned a settler standing on a city tile into a
// population point, capped at size 10 — but only the human menu ever issued it. The AI's
// idle-settler path drifted toward home and then stopped, so the Maori finished the game with
// two settlers pacing an island that had nothing left to irrigate.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class IslandCivEscapeTests
	{
		// A small island: land in the middle, ocean everywhere around it, so there is no legal
		// site to walk to and BoxedIn() is true.
		//
		// Radius 1 by default, and the size is load-bearing. HasExpansionRoom looks for a
		// foundable tile at least 4 from every city, so a radius-2 island with its city on the
		// shore still has a legal site in the far corner and is NOT boxed in — the fixture
		// quietly stopped testing the thing it was named for.
		private static (Game g, Player p, City c) AnIslandCiv(int islandRadius = 1)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 15);
			for (int y = 15; y <= 35; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 25 - islandRadius; y <= 25 + islandRadius; y++)
			for (int x = 40 - islandRadius; x <= 40 + islandRadius; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			// On the SHORE, not the middle: the boat rule requires an adjacent ocean tile,
			// and a city at the centre of the island has land on all eight sides.
			City c = g.AddCity(p, 0, 40 + islandRadius, 25)!;
			c.Size = 6;
			p.AddAdvance(new MapMaking(), false);
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static IProduction[] Plan(Player p, City c)
		{
			var plan = new System.Collections.Generic.List<IProduction>();
			System.Type stance = typeof(AI).GetNestedType("StrategyStance",
				System.Reflection.BindingFlags.NonPublic)!;
			typeof(AI).GetMethod("PlanProductionInto",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { plan, c, System.Enum.Parse(stance, "Develop") });
			return plan.ToArray();
		}

		private static bool BoxedIn(Player p)
			=> (bool)typeof(AI).GetMethod("BoxedIn",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), null)!;

		// The fixture has to be genuinely hemmed in, or none of this tests anything.
		[Fact]
		public void TheFixtureIsActuallyBoxedIn()
		{
			(Game g, Player p, City c) = AnIslandCiv();

			Assert.True(BoxedIn(p), "fixture: the island civ should have nowhere left to walk");
		}

		// The change: the boat is reachable, not merely present. Asserted on POSITION, because
		// presence was never the problem — the old code put a Longboat in the plan too, just
		// below everything a city would rather build.
		[Fact]
		public void ABoxedInCityLeadsItsPlanWithAHull()
		{
			(Game g, Player p, City c) = AnIslandCiv();

			IProduction[] plan = Plan(p, c);
			int boat = System.Array.FindIndex(plan, x => x is Longboat);

			Assert.True(boat >= 0, "no hull in the plan at all");
			Assert.True(boat <= 1, $"the hull is at position {boat}, behind {string.Join(", ", plan.Take(boat).Select(x => x.GetType().Name))}");
		}

		// The Longboat needs no passenger, and that is worth stating: it is not IBoardable,
		// it spends a population point and consumes itself ashore exactly as a land Settlers
		// does. An earlier version of this file asked the plan for a settler to put aboard,
		// which is a category error — and the hull cap made the matching mistake, counting
		// only IBoardable hulls and therefore never counting a Longboat at all.
		[Fact]
		public void TheLongboatIsItsOwnColonist()
		{
			Assert.False(new Longboat() is IBoardable,
				"if the Longboat ever gains a hold, the hull cap and the colonist logic both change");
		}

		// ...but a civ with somewhere to walk does not want a navy. This is the guard that
		// keeps the entry narrow — without it every coastal city in the world builds boats.
		[Fact]
		public void ACivWithRoomToWalkDoesNotBuildHulls()
		{
			(Game g, Player p, City c) = AnIslandCiv(islandRadius: 8);   // room to spare
			Assert.False(BoxedIn(p), "fixture: this civ should have somewhere to settle");

			Assert.DoesNotContain(Plan(p, c), x => x is Longboat);
		}

		// Two hulls is a crossing; more is a fleet. The cap already existed on the old rule
		// and has to survive the move.
		[Fact]
		public void TheHullCapStillHolds()
		{
			(Game g, Player p, City c) = AnIslandCiv();
			g.CreateUnit(UnitType.Longboat, c.X, c.Y, g.PlayerNumber(p));
			g.CreateUnit(UnitType.Longboat, c.X, c.Y, g.PlayerNumber(p));

			Assert.DoesNotContain(Plan(p, c), x => x is Longboat);
		}

		// ── the stranded settler ─────────────────────────────────────────────────

		private static void MoveAI(Player p, IUnit unit)
			=> typeof(AI).GetMethod("Move",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { unit });

		// A settler with nothing to found and nothing to improve, standing in its own city,
		// becomes a citizen instead of pacing for the rest of the game.
		[Fact]
		public void AStrandedSettlerJoinsTheCityItStandsIn()
		{
			(Game g, Player p, City c) = AnIslandCiv();   // nothing left to do anywhere
			int before = c.Size;
			IUnit settler = g.CreateUnit(UnitType.Settlers, c.X, c.Y, g.PlayerNumber(p))!;
			settler.MovesLeft = settler.Move;

			MoveAI(p, settler);
			Sim.Settle();

			Assert.Equal(before + 1, c.Size);
			Assert.DoesNotContain(g.GetUnits(), u => u == settler);
		}

		// A size-10 city refuses, and the refusal is the ENGINE's: Orders.CreateCity raises
		// ADDCITY and leaves the settler standing. Pinned because it is the behaviour the
		// AI's own cap is mirroring — but note this test passes with the AI cap removed, so
		// it is not a check on MaxJoinCitySize. That is the next test.
		[Fact]
		public void TheEngineRefusesToJoinAFullCity()
		{
			(Game g, Player p, City c) = AnIslandCiv();
			c.Size = 10;
			IUnit settler = g.CreateUnit(UnitType.Settlers, c.X, c.Y, g.PlayerNumber(p))!;
			settler.MovesLeft = settler.Move;

			MoveAI(p, settler);
			Sim.Settle();

			Assert.Equal(10, c.Size);
			Assert.Contains(g.GetUnits(), u => u == settler);
		}

		// What MaxJoinCitySize is actually for: choosing the DESTINATION. Walking a settler to
		// the nearest city is wrong when that city is full — it arrives, is refused, and paces
		// there instead of somewhere it could have been useful.
		[Fact]
		public void AStrandedSettlerWalksPastAFullCityToOneThatCanTakeIt()
		{
			(Game g, Player p, City full) = AnIslandCiv();
			full.Size = 10;
			City room = g.AddCity(p, 1, full.X - 2, 25)!;
			room.Size = 4;
			// Standing IN the full city: a settler on open ground finds road or irrigation work
			// first and never reaches the drift branch, which is what the first version of this
			// test tripped over — Goto came back empty because the settler was busy.
			IUnit settler = g.CreateUnit(UnitType.Settlers, full.X, full.Y, g.PlayerNumber(p))!;
			settler.MovesLeft = settler.Move;
			Sim.ClearTasks();

			MoveAI(p, settler);

			Assert.Equal(new System.Drawing.Point(room.X, room.Y), settler.Goto);
		}
	}
}
