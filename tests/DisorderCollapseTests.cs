// CivOne tests
//
// When civil disorder brings the government down.
//
// Civ 1 collapsed a Republic or Democracy the moment ANY ONE city had rioted three turns
// running, and shipped that rule for players holding about eight cities. This game's empires
// are an order of magnitude larger — the Malians finished a 750-turn run with 105 — and at
// that size the chance that no city anywhere is three turns into a riot is close to zero.
//
// Measured over turns 312-749 of one run, from the per-city disorder flag in the decision
// log: the trigger fired 34 times for the Malians and 48 for the Mongols. Each firing drops
// the whole empire into Anarchy, whose corruption multiplier is 12 against a Republic's 24
// and a Democracy's 0, so every city's trade collapses on the same turn and recovers when a
// new government is adopted. That is the sawtooth on the economic output graph, and the
// Marketplace burned on disorder turn 1 and the Bank looted on turn 2 are why each recovery
// returns to a slightly lower level.
//
// The threshold is now one city in eight, never fewer than one — the original behaviour at
// the original scale, proportional above it.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class DisorderCollapseTests
	{
		private static (Game g, Player p, City[] cities) AnEmpire(int count)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Republic();          // CollapsesInDisorder
			p.Explore(40, 25, range: 15);
			for (int y = 18; y <= 32; y++)
			for (int x = 26; x <= 54; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			var cities = new City[count];
			for (int i = 0; i < count; i++)
			{
				cities[i] = g.AddCity(p, (byte)i, 27 + i, 25)!;
				cities[i].Size = 4;
			}
			Sim.ClearTasks();
			return (g, p, cities);
		}

		// Riot a city for the full three turns, the way the disorder counter does.
		private static void RiotFor3Turns(City c) => c.DisorderTurns = 3;

		// Civ 1's scale, Civ 1's rule: one rioting city out of eight brings the government down.
		[Fact]
		public void InASmallEmpireOneRiotingCityIsStillEnough()
		{
			(Game g, Player p, City[] cities) = AnEmpire(8);
			RiotFor3Turns(cities[0]);

			Assert.True(p.DisorderIsGeneral);
		}

		// ...and smaller still. A one-city civ has nothing to average over.
		[Fact]
		public void AOneCityCivCollapsesOnItsOnlyCity()
		{
			(Game g, Player p, City[] cities) = AnEmpire(1);
			RiotFor3Turns(cities[0]);

			Assert.True(p.DisorderIsGeneral);
		}

		// The change. A hundred-city empire does not fall over one unhappy town.
		[Fact]
		public void ALargeEmpireSurvivesASingleRiot()
		{
			(Game g, Player p, City[] cities) = AnEmpire(40);
			RiotFor3Turns(cities[0]);

			Assert.False(p.DisorderIsGeneral,
				"one city in forty brought down the government");
		}

		// It is a threshold, not immunity: general disorder still ends the government.
		[Fact]
		public void ALargeEmpireStillFallsWhenTheDisorderIsGeneral()
		{
			(Game g, Player p, City[] cities) = AnEmpire(40);
			foreach (City c in cities.Take(5)) RiotFor3Turns(c);

			Assert.True(p.DisorderIsGeneral);
		}

		// The boundary, stated exactly: 40/8 = 5, so four is not enough and five is.
		[Fact]
		public void TheThresholdIsOneCityInEight()
		{
			(Game g, Player p, City[] cities) = AnEmpire(40);

			foreach (City c in cities.Take(4)) RiotFor3Turns(c);
			Assert.False(p.DisorderIsGeneral, "four cities in forty should not be general disorder");

			RiotFor3Turns(cities[4]);
			Assert.True(p.DisorderIsGeneral, "five cities in forty should be");
		}

		// A city one turn short of the full riot does not count toward the threshold — the
		// count has to agree with the case that asks it.
		[Fact]
		public void ShortRiotsDoNotCount()
		{
			(Game g, Player p, City[] cities) = AnEmpire(8);
			cities[0].DisorderTurns = 2;

			Assert.False(p.DisorderIsGeneral);
		}

		// ── the rule is actually reached ─────────────────────────────────────────

		// The gate above is only worth anything if City.NewTurn consults it. Driven through
		// the real disorder path: a size-1 city cannot riot, so the fixture forces the
		// counter and runs the turn, and the observable is whether the civ is in Anarchy.
		[Fact]
		public void AGovernmentDoesNotFallWhileTheDisorderIsLocal()
		{
			(Game g, Player p, City[] cities) = AnEmpire(40);
			Assert.False(p.Government is Anarchy, "fixture: should start as a Republic");

			// Two cities deep in disorder, well under 40/8 = 5.
			cities[0].DisorderTurns = 3;
			cities[1].DisorderTurns = 3;
			foreach (City c in cities) { c.NewTurn(); Sim.Settle(); }

			Assert.False(p.Government is Anarchy,
				"the government fell over two cities in forty");
		}
	}
}
