// CivOne tests
//
// A city that has stopped growing builds the thing that feeds it.
//
// Measured over the last 202 turns of the 2200 AD run: 1,011 AI production decisions were
// taken by cities of size <= 6 with food income <= 0 — 285 of them size 2 — and the ranking
// was Caravan 14%, Diplomat 12%, Colosseum 10%, Observatory 7%, SAM Battery 7%. Granary 2%,
// Aqueduct 1%, Harbour 0%. Empire-wide the Harbour was chosen 26 times in 7,475 decisions.
//
// The growth entries further down PlanProductionInto were never reached: a starving city is
// usually also unhappy or under-garrisoned, and both of those are considered first. Consider()
// keeps the first entry per type, so position in that method IS the priority.
//
// Also here: the Autopilot launch gate. The auto-launch loop skipped HumanPlayer with no
// Autopilot exception, so an autoplayed human accumulated ship parts forever — which is why
// the 2200 AD run finished with a Mission Control city and no ship while two AI civs launched.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class FoodFirstProductionTests
	{
		// An island city: every workable tile is ocean, so its food income cannot rise without
		// a Harbour. This is the shape the Harbour was built for and never reached.
		private static (Game g, Player p, City c) AStarvingIslandCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 10);

			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			// Strip the specials. Map generation seeded two Fish in this radius (food 3 each),
			// which fed the city to +3 and made a "starving" fixture that was not starving —
			// exactly the kind of incidental agreement that makes a green test meaningless.
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				((CivOne.Tiles.BaseTile)Map.Instance[x, y]).Special = false;
			Map.Instance.RecalculateContinentsIfDirty();

			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 4;
			p.AddAdvance(new Pottery(), false);
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static IProduction[] Plan(Player p, City c)
		{
			// StrategyStance is a private nested enum, so the value has to be built by
			// reflection too; Develop is the ordinary peacetime stance these cities are in.
			var plan = new System.Collections.Generic.List<IProduction>();
			System.Type stance = typeof(AI).GetNestedType("StrategyStance",
				System.Reflection.BindingFlags.NonPublic)!;
			typeof(AI).GetMethod("PlanProductionInto",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), new object[] { plan, c, System.Enum.Parse(stance, "Develop") });
			return plan.ToArray();
		}

		[Fact]
		public void AStarvingCoastalCityReachesForTheHarbour()
		{
			(Game g, Player p, City c) = AStarvingIslandCity();
			Assert.True(c.FoodIncome <= 0, $"fixture is not starving: food income {c.FoodIncome}");

			IProduction[] plan = Plan(p, c);

			Assert.Contains(plan, x => x is Harbour);
		}

		// Position is the whole point, and this is the part the rule actually changes.
		//
		// The first version of this test compared the Harbour against Temple and Colosseum and
		// passed with the rule deleted — EarnsItsKeep refuses happiness buildings to a city
		// with no unhappiness, so neither was ever in the plan and `temple < 0 ||` waved it
		// through. What the rule demonstrably does is put the Harbour ahead of the GRANARY:
		// without it the plan reads [garrison, Granary, Harbour], with it [garrison, Harbour,
		// Granary]. That ordering is the whole argument — a Granary halves the food already
		// arriving, which is nothing on a city ringed by water, while the Harbour is what makes
		// any arrive at all.
		[Fact]
		public void TheHarbourOutranksTheGranaryWhenTheFoodIsComingFromTheSea()
		{
			(Game g, Player p, City c) = AStarvingIslandCity();

			IProduction[] plan = Plan(p, c);

			int harbour = System.Array.FindIndex(plan, x => x is Harbour);
			int granary = System.Array.FindIndex(plan, x => x is Granary);

			Assert.True(harbour >= 0, "no Harbour in the plan at all");
			Assert.True(granary >= 0, "no Granary in the plan at all");
			Assert.True(harbour < granary,
				$"Granary at {granary} beat the Harbour at {harbour} on a city that works only ocean");
		}

		// An inland city gets the Granary instead — the rule is "feed it", not "build a port".
		[Fact]
		public void AStarvingInlandCityReachesForTheGranary()
		{
			(Game g, Player p, City c) = AStarvingIslandCity();
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Desert);
			Map.Instance.RecalculateContinentsIfDirty();
			c.InvalidateCache();
			Assert.True(c.FoodIncome <= 0, $"fixture is not starving: food income {c.FoodIncome}");

			IProduction[] plan = Plan(p, c);

			Assert.Contains(plan, x => x is Granary);
			Assert.DoesNotContain(plan, x => x is Harbour);
		}

		// ── the Autopilot launch gate ─────────────────────────────────────────────
		//
		// A ship that meets the launch minimums sits on the pad forever when the AI is driving
		// the human, because the auto-launch loop skipped HumanPlayer unconditionally. Nobody
		// is there to open the SpaceShips screen and press the button.
		private static Game AHumanShipReadyToLaunch()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			byte h = g.PlayerNumber(g.HumanPlayer);
			// The launch minimums: 1 engine (2 components), 1 module set (3 modules), and
			// enough structure to carry them.
			g.Progress(h).SpaceshipComponent  = 2;
			g.Progress(h).SpaceshipModule     = 3;
			g.Progress(h).SpaceshipStructural = Game.SpaceshipStructuresNeeded(2, 3);
			Sim.ClearTasks();
			return g;
		}

		private static void PlayARound(Game g)
		{
			uint target = g.GameTurn + 1u;
			while (g.GameTurn < target)
			{
				Sim.ClearTasks();
				g.EndTurn();
			}
		}

		[Fact]
		public void UnderAutopilotTheHumansShipLaunchesItself()
		{
			Game g = AHumanShipReadyToLaunch();
			byte h = g.PlayerNumber(g.HumanPlayer);
			bool was = Settings.Instance.Autopilot;
			try
			{
				Settings.Instance.Autopilot = true;
				PlayARound(g);
				Assert.True(g.Progress(h).SpaceshipLaunchTurn > 0,
					"the autoplayed human's ship never left the ground");
			}
			finally { Settings.Instance.Autopilot = was; }
		}

		// ...and with a human actually playing, the launch stays theirs to order.
		[Fact]
		public void WithAHumanAtTheControlsTheLaunchIsStillManual()
		{
			Game g = AHumanShipReadyToLaunch();
			byte h = g.PlayerNumber(g.HumanPlayer);
			bool was = Settings.Instance.Autopilot;
			try
			{
				Settings.Instance.Autopilot = false;
				PlayARound(g);
				Assert.Equal(0, g.Progress(h).SpaceshipLaunchTurn);
			}
			finally { Settings.Instance.Autopilot = was; }
		}

		// A city that is feeding itself is left alone: this rule is for cities that have
		// actually stopped, not a blanket reordering of every coastal build queue.
		[Fact]
		public void AFedCityIsNotReordered()
		{
			// Built fed from the start rather than by re-terraforming a starving fixture:
			// changing the tiles under an existing city does not reassign its citizens, so
			// the workers stayed out on the ocean and the "well fed" city still read as
			// starving.
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 10);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			// Still coastal, so the Harbour remains eligible — the test is about the food
			// rule not firing, not about the city being landlocked.
			Map.Instance.ChangeTileType(43, 25, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 42, 25)!;
			c.Size = 4;
			p.AddAdvance(new Pottery(), false);
			Sim.ClearTasks();
			Assert.True(c.FoodIncome > 0, "fixture should be well fed");

			IProduction[] plan = Plan(p, c);
			int harbour = System.Array.FindIndex(plan, x => x is Harbour);

			// It may still appear further down the ordinary growth chain; what it must not do
			// is lead the plan on the strength of a food rule that does not apply.
			Assert.True(harbour != 0, "the food-first rule fired for a city that is not starving");
		}
	}
}
