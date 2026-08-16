// CivOne tests
//
// A city at its growth ceiling builds the thing that unblocks it, ahead of whatever the
// empire is trying to achieve.
//
// Aqueduct and Sewer System were both in the standard infrastructure chain at the bottom of
// PlanProductionInto, and both were starved there. Position is priority — Consider() keeps
// the first entry per type and CityProduction builds plan[0] — and the victory-path switch
// runs first with two entries that never terminate: Conquest re-offers BestAttacker up to
// three units per city, and Commerce re-offers a Caravan with NO ceiling, because a
// delivered caravan is consumed so the count never accumulates.
//
// Measured in a finished world: 44% aqueduct coverage, with 200 cities sitting at exactly
// size 7 — the largest bucket on the map, with a cliff to 25 immediately above it.

using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class GrowthUnblockTests
	{
		// A capped city with plenty to work, so nothing else is obviously urgent.
		private static (Game g, Player p, City c) ACappedCity(int size)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Enums.Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(45, 25, range: 30);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = (byte)size;
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (System.Math.Abs(dx) == 2 && System.Math.Abs(dy) == 2) continue;
				ITile t = Map.Instance[40 + dx, 25 + dy];
				if (t is not null && !t.IsOcean) t.Road = true;
			}
			c.ResetResourceTiles();
			// A garrison already present, so the universal defender entry — which correctly
			// outranks everything — does not sit at the head of the plan and mask the result.
			g.CreateUnit(Enums.UnitType.Militia, 40, 25, g.PlayerNumber(p), false);
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static List<IProduction> PlanFor(Player p, City c, string stance = "Develop")
		{
			var plan = new List<IProduction>();
			var method = typeof(AI).GetMethod("PlanProductionInto",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var stanceType = typeof(AI).GetNestedType("StrategyStance",
				System.Reflection.BindingFlags.NonPublic);
			object stanceValue = System.Enum.Parse(stanceType!, stance);
			return (List<IProduction>)method!.Invoke(AI.Instance(p), new object[] { plan, c, stanceValue })!;
		}

		// Setting _path alone does NOT work: the Path property re-derives via ChoosePath()
		// whenever GameTurn - _pathChosenTurn >= PathReviewInterval, and the field starts at
		// -interval-1 so the very first read re-chooses and discards whatever was set. The
		// first version of these tests did exactly that, so no civ was ever on Commerce, no
		// Caravan was ever planned, and every assertion passed against the unfixed code.
		// Caught by the negative check killing nothing at all.
		private static void SetPath(Player p, string path)
		{
			AI ai = AI.Instance(p);
			var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			var pathType = typeof(AI).GetNestedType("VictoryPath", System.Reflection.BindingFlags.NonPublic);
			typeof(AI).GetField("_path", flags)!.SetValue(ai, System.Enum.Parse(pathType!, path));
			typeof(AI).GetField("_pathChosenTurn", flags)!.SetValue(ai, (int)Game.Instance.GameTurn);
			typeof(AI).GetField("_pathSignalSeen", flags)!.SetValue(ai, Game.Instance.SETISignalReceived);

			var actual = typeof(AI).GetProperty("Path", flags)!.GetValue(ai)!.ToString();
			Assert.True(actual == path, $"fixture: path did not stick — wanted {path}, got {actual}");
		}

		private static void Give(Player p, params IAdvance[] advances)
		{
			foreach (IAdvance a in advances) p.AddAdvance(a, false);
		}

		// The bug, stated directly. A Commerce civ re-offers a Caravan every turn with no
		// ceiling, and it sat ahead of the aqueduct forever.
		[Fact]
		public void ACommerceCivStillUnblocksACappedCity()
		{
			(Game g, Player p, City c) = ACappedCity(size: 7);
			Give(p, new Construction(), new CivOne.Advances.Trade(), new Currency());
			SetPath(p, "Commerce");

			List<IProduction> plan = PlanFor(p, c);
			int aqueduct = plan.FindIndex(x => x is Aqueduct);
			int caravan = plan.FindIndex(x => x is Units.Caravan);

			// Both must be present. "caravan < 0 counts as a pass" is precisely the escape
			// that made the first version of this test vacuous.
			Assert.True(aqueduct >= 0, "no aqueduct planned for a city stuck at its cap");
			Assert.True(caravan >= 0, "fixture: the Commerce path did not offer a caravan at all");
			Assert.True(aqueduct < caravan,
				$"caravan (index {caravan}) was planned ahead of the aqueduct (index {aqueduct})");
		}

		// Same for the conqueror, whose own code comment says "a conqueror builds the army
		// before the aqueduct" — true, and it is why its cities never grew.
		[Fact]
		public void AConquerorStillUnblocksACappedCity()
		{
			(Game g, Player p, City c) = ACappedCity(size: 7);
			// IronWorking so BestAttacker() is a Legion, which BestDefender() never returns.
			// With only BronzeWorking both come back as cheap units and "the first thing with
			// an attack value" matched the garrison Phalanx at index 0 — a unit that SHOULD
			// precede everything, so the test failed against correct code.
			Give(p, new Construction(), new BronzeWorking(), new IronWorking());
			SetPath(p, "Conquest");

			List<IProduction> plan = PlanFor(p, c);
			int aqueduct = plan.FindIndex(x => x is Aqueduct);
			int attacker = plan.FindIndex(x => x is Legion);

			// Ordering, not mere presence: the aqueduct is reached in the standard chain
			// anyway in a one-city fixture, so "Contains" passed against the unfixed code.
			Assert.True(aqueduct >= 0, "no aqueduct planned for a city stuck at its cap");
			Assert.True(attacker >= 0, "fixture: the Conquest path did not offer an attacker at all");
			Assert.True(aqueduct < attacker,
				$"attacker (index {attacker}) was planned ahead of the aqueduct (index {aqueduct})");
		}

		// The sewer tier, at the second cap.
		[Fact]
		public void ACityAtTheSewerCapPlansASewer()
		{
			(Game g, Player p, City c) = ACappedCity(size: 12);
			Give(p, new Construction(), new Engineering());
			c.AddBuilding(new Aqueduct());

			Assert.Contains(PlanFor(p, c), x => x is SewerSystem);
		}

		// And it must NOT fire early: a city with room left has better things to build than
		// an aqueduct it will not need for several turns. GrowthBlocked is the whole gate.
		[Fact]
		public void ACityWithRoomToGrowIsNotForcedToBuildAnAqueduct()
		{
			(Game g, Player p, City c) = ACappedCity(size: 4);
			Give(p, new Construction(), new CivOne.Advances.Trade(), new Currency());
			SetPath(p, "Commerce");

			List<IProduction> plan = PlanFor(p, c);

			Assert.False(c.GrowthBlocked, "fixture: a size-4 city should not be growth-blocked");
			Assert.True(plan.FindIndex(x => x is Aqueduct) != 0,
				"an aqueduct led the plan in a city that can still grow");
		}
	}
}
