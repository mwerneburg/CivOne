// CivOne tests
//
// What a civ is TRYING to do, as opposed to what it is reacting to.
//
// Nothing in the AI decided anything before this. StrategyStance answers "are we rioting, are
// we at war, is there room" — every long-range build fell out of accident. The spaceship is
// the clearest case: no code anywhere called Consider on an SS part, so parts were reachable
// only through the last-resort fallback that rolls over whatever a city has left. Measured
// over the 2200 AD run, three AI civs launched full ships purely because their big cities had
// run out of buildings, while the autoplayed human's ninety-five smaller ones always had
// another Cathedral available and finished with four parts.
//
// Three consumers, tested here: production intent, research weights, stance modulation.

using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Leaders;

namespace CivOne.Tests
{
	public class VictoryPathTests
	{
		private static (Game g, Player p) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 33; x <= 47; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				((CivOne.Tiles.BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Sim.ClearTasks();
			return (g, p);
		}

		private static City AddCity(Game g, Player p, int id, int x, int size = 6)
		{
			City c = g.AddCity(p, (byte)id, x, 25)!;
			c.Size = (byte)size;
			return c;
		}

		// Leader objects — and therefore their Doctrine — are shared and cached
		// (BaseLeader._doctrine), so a knob set by one test is still set in the next one.
		// Every test states the WHOLE temperament it depends on rather than nudging one
		// value: the conqueror test was failing because a previous test had left
		// ScienceBias at 100 on the same leader, and with the signal up that scores
		// Diaspora above Conquest.
		private static void Temperament(Player p, double war, int science, double expansion = 1.0)
		{
			Doctrine d = p.Civilization.Leader.Doctrine;
			d.WarAppetite      = war;
			d.ScienceBias      = science;
			d.ExpansionAppetite = expansion;
			d.UnrestTolerance  = 0.5;
		}

		private static object PathOf(Player p)
			=> typeof(AI).GetProperty("Path",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.GetValue(AI.Instance(p))!;

		private static string PathName(Player p) => PathOf(p).ToString()!;

		// ── choosing ──────────────────────────────────────────────────────────────

		// Space is only a real ambition once the road to it exists. Before the signal there is
		// no spaceship to build, so a would-be Diaspora civ would be acting on a plan it cannot
		// execute — it reads as something else until the sky says otherwise.
		[Fact]
		public void ThereIsNoSpaceAmbitionBeforeTheSignal()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.5, science: 100);
			Assert.False(g.SETISignalReceived);

			Assert.NotEqual("Diaspora", PathName(p));
		}

		[Fact]
		public void AScientistReachesForTheStarsOnceTheSignalArrives()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.5, science: 100);
			g.SETISignalReceived = true;

			Assert.Equal("Diaspora", PathName(p));
		}

		[Fact]
		public void AWarlikeLeaderChoosesConquest()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 2.0, science: 0);

			Assert.Equal("Conquest", PathName(p));
		}

		// Stickiness is the whole design. StrategyStance is re-derived every call and this file
		// is largely a history of what happens when a long-range signal thrashes; a path that
		// changed with the weather would be worse than no path at all.
		[Fact]
		public void ThePathDoesNotChangeWithTheWeather()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 2.0, science: 0);
			string first = PathName(p);

			// Flip the temperament underneath it. Within the review interval the civ holds
			// the plan it committed to.
			Temperament(p, war: 0.1, science: 0);

			Assert.Equal(first, PathName(p));
		}

		// ...but the signal is a genuine shock, and re-opens the question immediately.
		[Fact]
		public void TheSignalReopensTheQuestionAtOnce()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.5, science: 100);
			Assert.NotEqual("Diaspora", PathName(p));

			g.SETISignalReceived = true;

			Assert.Equal("Diaspora", PathName(p));
		}

		// ── production intent ─────────────────────────────────────────────────────

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

		// The reason this whole mechanism exists.
		[Fact]
		public void ADiasporaCivBuildsShipPartsDeliberately()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.5, science: 100);
			City c = AddCity(g, p, 0, 40);
			g.CreateUnit(UnitType.Musketeers, 40, 25, g.PlayerNumber(p));
			g.SETISignalReceived = true;
			g.DomeAssignments[1] = new System.Collections.Generic.List<Wonder> { Wonder.DomeSensorNet };
			p.AddAdvance(new SpaceFlight(), false);
			foreach (City x in p.Cities) x.AddWonder(new Wonders.ApolloProgram());
			Assert.Equal("Diaspora", PathName(p));
			Assert.True(p.ProductionAvailable(new SSStructural()), "fixture: ship parts are not available");

			IProduction[] plan = Plan(p, c);

			Assert.Contains(plan, x => x is ISpaceShip);
		}

		// ...and a conqueror in the same position does not, which is what makes it a choice
		// rather than a new step in the standard chain.
		[Fact]
		public void AConquerorInTheSamePositionDoesNot()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 2.0, science: 0);
			City c = AddCity(g, p, 0, 40);
			g.CreateUnit(UnitType.Musketeers, 40, 25, g.PlayerNumber(p));
			g.SETISignalReceived = true;
			g.DomeAssignments[1] = new System.Collections.Generic.List<Wonder> { Wonder.DomeSensorNet };
			p.AddAdvance(new SpaceFlight(), false);
			foreach (City x in p.Cities) x.AddWonder(new Wonders.ApolloProgram());
			Assert.Equal("Conquest", PathName(p));

			IProduction[] plan = Plan(p, c);
			int ship = System.Array.FindIndex(plan, x => x is ISpaceShip);

			Assert.True(ship != 0, "a conqueror led its plan with a spaceship part");
		}

		// ── research weights ──────────────────────────────────────────────────────

		[Fact]
		public void ADiasporaCivValuesSpaceFlightOverWeapons()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.5, science: 100);
			g.SETISignalReceived = true;
			Assert.Equal("Diaspora", PathName(p));

			int space = AI.Instance(p).AdvanceDemandValue(new SpaceFlight());
			int guns  = AI.Instance(p).AdvanceDemandValue(new Conscription());

			Assert.True(space > guns, $"Space Flight {space} did not outrank Conscription {guns}");
		}

		[Fact]
		public void ACommerceCivValuesBanking()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.1, science: 0);
			for (int i = 0; i < 8; i++) AddCity(g, p, i, 34 + i);
			Assert.Equal("Commerce", PathName(p));

			Assert.True(AI.Instance(p).AdvanceDemandValue(new Banking())
			          > AI.Instance(p).AdvanceDemandValue(new Conscription()));
		}

		// ── stance modulation ─────────────────────────────────────────────────────

		// A plan speaks only where nothing is actually wrong. A rioting civ consolidates
		// whatever it was hoping to become — this is the guard that keeps an ambition from
		// overriding a situation.
		[Fact]
		public void ARiotingCivConsolidatesWhateverItsPlanIs()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.5, science: 100);
			g.SETISignalReceived = true;
			for (int i = 0; i < 6; i++) AddCity(g, p, i, 34 + i);
			p.LuxuriesRate = 5;   // the ConsiderSliders crisis signal
			Assert.Equal("Diaspora", PathName(p));

			object stance = typeof(AI).GetMethod("GetStance",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), null)!;

			Assert.Equal("Consolidate", stance.ToString());
		}

		// A builder path with a real base deepens instead of spreading.
		[Fact]
		public void ABuilderWithABaseDevelopsRatherThanExpands()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.1, science: 0);
			for (int i = 0; i < 8; i++) AddCity(g, p, i, 34 + i);
			Assert.Equal("Commerce", PathName(p));

			object stance = typeof(AI).GetMethod("GetStance",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), null)!;

			Assert.Equal("Develop", stance.ToString());
		}

		// ...but a civ with two cities still expands. A plan you cannot fund is not a plan.
		[Fact]
		public void ABuilderWithoutABaseStillExpands()
		{
			(Game g, Player p) = AWorld();
			Temperament(p, war: 0.1, science: 0);
			AddCity(g, p, 0, 40);

			object stance = typeof(AI).GetMethod("GetStance",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(AI.Instance(p), null)!;

			Assert.Equal("Expand", stance.ToString());
		}
	}
}
