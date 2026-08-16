// CivOne tests
//
// Caravans must find NEW partners, and there must not be an unbounded number of them.
//
// Trade routes are unique per partner: City.AddTradeRoute removes any existing route to the
// same city before adding, so a second delivery to the same place replaces the route rather
// than stacking it. It buys only the one-time gold, which itself falls by a third once both
// civs hold Railroad and again with Flight — to about a ninth of a pre-industrial delivery.
//
// The targeting picked the NEAREST foreign city and checked nothing else, and "nearest" is a
// stable choice, so every caravan a city ever built walked to the same neighbour and re-sold
// it the same route. Meanwhile the Commerce path re-offered a Caravan every turn with no
// ceiling — the one path entry with no bound — which is what starved the aqueducts.

using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CaravanTargetingTests
	{
		// Us at x=40, and two foreign cities to the east at 44 and 48 — both reachable, both
		// on the same continent, the nearer one first in the ranking.
		private static (Game g, Player us, Player them, City home, City near, City far) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player us = ps[0], them = ps[1];
			foreach (Player p in new[] { us, them })
			{
				p.Government = new Governments.Monarchy();
				p.Explore(45, 25, range: 30);
			}

			City home = g.AddCity(us, 0, 40, 25)!;   home.Size = 8;
			City near = g.AddCity(them, 1, 44, 25)!; near.Size = 8;
			City far  = g.AddCity(them, 2, 48, 25)!; far.Size = 8;
			Sim.ClearTasks();
			return (g, us, them, home, near, far);
		}

		private static IUnit ACaravanAt(Game g, Player us, City home, int x, int y)
		{
			g.CreateUnit(Enums.UnitType.Caravan, x, y, g.PlayerNumber(us), false);
			IUnit unit = g.GetUnits().First(u => u is Caravan && u.Owner == g.PlayerNumber(us));
			unit.SetHome(home);
			return unit;
		}

		// Re-targeting is STAGGERED: IdleRetryTurn defers unless
		// GetHashCode(unit) & 7 == GameTurn & 7, so a unit only re-probes one turn in eight
		// (a deliberate fix for thirty idle caravans in one city re-probing together). Calling
		// it once left Goto empty and failed every test here, including the one asserting
		// behaviour that had not changed. So walk the turn on until the unit reaches its slot.
		private static void AssignMission(Player us, IUnit unit)
		{
			var method = typeof(AI).GetMethod("AssignMission",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.NotNull(method);
			for (int i = 0; i < 8 && unit.Goto.IsEmpty; i++)
			{
				method!.Invoke(AI.Instance(us), new object[] { unit });
				if (unit.Goto.IsEmpty) Game.Instance.GameTurn++;
			}
		}

		// With no routes yet, the nearest foreign city is the right answer — this is the
		// existing behaviour and it must not change.
		[Fact]
		public void AFreshCaravanHeadsForTheNearestForeignCity()
		{
			(Game g, Player us, Player them, City home, City near, City far) = AWorld();
			IUnit unit = ACaravanAt(g, us, home, 41, 25);

			AssignMission(us, unit);

			Assert.Equal(near.X, unit.Goto.X);
			Assert.Equal(near.Y, unit.Goto.Y);
		}

		// The fix. Once the home city is routed to the near neighbour, a second caravan must
		// go somewhere new rather than re-selling the same route for a shrinking fee.
		[Fact]
		public void ASecondCaravanSkipsAPartnerAlreadyRouted()
		{
			(Game g, Player us, Player them, City home, City near, City far) = AWorld();
			home.AddTradeRoute(near, "Silk");
			IUnit unit = ACaravanAt(g, us, home, 41, 25);

			AssignMission(us, unit);

			Assert.True(unit.Goto.X == far.X && unit.Goto.Y == far.Y,
				$"expected the unrouted city at ({far.X},{far.Y}), got ({unit.Goto.X},{unit.Goto.Y})");
		}

		// ...but when every reachable partner is already routed, delivering again beats
		// standing still at upkeep forever. The filter must not strand the unit.
		[Fact]
		public void ACaravanWithNoNewPartnersStillDelivers()
		{
			(Game g, Player us, Player them, City home, City near, City far) = AWorld();
			home.AddTradeRoute(near, "Silk");
			home.AddTradeRoute(far, "Spice");
			IUnit unit = ACaravanAt(g, us, home, 41, 25);

			AssignMission(us, unit);

			Assert.True((unit.Goto.X == near.X && unit.Goto.Y == near.Y)
			         || (unit.Goto.X == far.X && unit.Goto.Y == far.Y),
				$"caravan was stranded with no destination: ({unit.Goto.X},{unit.Goto.Y})");
		}

		// The ceiling. A Commerce civ used to re-offer a Caravan every turn, unbounded,
		// because a delivered caravan is consumed and the count never accumulates.
		[Fact]
		public void TheCommercePathStopsOfferingCaravansAtTheCeiling()
		{
			(Game g, Player us, Player them, City home, City near, City far) = AWorld();
			us.AddAdvance(new CivOne.Advances.Trade(), false);
			us.AddAdvance(new Currency(), false);
			var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			var pathType = typeof(AI).GetNestedType("VictoryPath", System.Reflection.BindingFlags.NonPublic);
			AI ai = AI.Instance(us);
			typeof(AI).GetField("_path", flags)!.SetValue(ai, System.Enum.Parse(pathType!, "Commerce"));
			typeof(AI).GetField("_pathChosenTurn", flags)!.SetValue(ai, (int)g.GameTurn);
			typeof(AI).GetField("_pathSignalSeen", flags)!.SetValue(ai, g.SETISignalReceived);

			// The ceiling is max(2, cities/6), which is 2 for a one-city civ — the same
			// expression the standard chain uses, so two afoot fills it for both entries.
			ACaravanAt(g, us, home, 41, 25);
			ACaravanAt(g, us, home, 39, 25);

			var plan = new List<IProduction>();
			var method = typeof(AI).GetMethod("PlanProductionInto", flags);
			var stanceType = typeof(AI).GetNestedType("StrategyStance", System.Reflection.BindingFlags.NonPublic);
			plan = (List<IProduction>)method!.Invoke(ai,
				new object[] { plan, home, System.Enum.Parse(stanceType!, "Develop") })!;

			Assert.DoesNotContain(plan, x => x is Caravan);
		}
	}
}
