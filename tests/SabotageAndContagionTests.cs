// CivOne tests
//
// A 520-turn run logged 48,643 diplomat moves, 35,434 of them ending in the sabotage of a
// human city — 68 per turn, every one a modal screen. That is not a spy game; it is a city
// being dismantled a building at a time by software the player cannot argue with.
//
// Three answers, none of which is "show fewer messages":
//   Police Station  catches the agent outright. Ordinary police work, which is what actually
//                   caught spies through the Cold War, and the counterplay the player lacked.
//   Hospital        refuses The Thing. In the film the blood test was worked out in an
//                   afternoon; the organism is only unstoppable where no medicine meets it.
//   Diplomat cap    6 -> 3 per civ. Sixteen civs at 6 apiece, with the unit CONSUMED by its
//                   mission, is a permanent standing campaign.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SabotageAndContagionTests
	{
		private static (Game, Player, Player) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player a = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			return (g, a, g.HumanPlayer);
		}

		private static City ACity(Player owner, int x, int y = 25, int size = 6)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(x, y, range: 3);
			City c = g.AddCity(owner, x + y, x, y)!;
			c.Size = (byte)size;
			foreach (IBuilding b in new IBuilding[] { new Temple(), new Library(), new Barracks() })
				c.AddBuilding(b);
			return c;
		}

		// ── the Police Station ──────────────────────────────────────────────

		// The defect, stated directly: a defended city keeps its buildings.
		[Fact]
		public void APoliceStationStopsSabotage()
		{
			(Game g, Player ai, Player human) = AWorld();
			City c = ACity(human, 40);
			c.AddBuilding(new PoliceStation());
			int before = c.Buildings.Length;

			IUnit spy = g.CreateUnit(UnitType.Diplomat, 41, 25, g.PlayerNumber(ai))!;
			string report = (spy as Diplomat)!.Sabotage(c);

			Assert.Equal(before, c.Buildings.Length);
			Assert.Contains("police", report);
		}

		// ...and without one it does not, or the test above proves nothing.
		[Fact]
		public void WithoutAPoliceStationSabotageLands()
		{
			(Game g, Player ai, Player human) = AWorld();
			City c = ACity(human, 40);
			int before = c.Buildings.Length;

			IUnit spy = g.CreateUnit(UnitType.Diplomat, 41, 25, g.PlayerNumber(ai))!;
			(spy as Diplomat)!.Sabotage(c);

			// Either a building was destroyed or production was halted — both are real damage.
			Assert.True(c.Buildings.Length < before || c.Shields == 0);
		}

		// The agent is spent either way: a campaign of sabotage against a policed city costs
		// the sender units. That is what makes the building a deterrent rather than a wall.
		[Fact]
		public void TheCaughtAgentIsStillLost()
		{
			(Game g, Player ai, Player human) = AWorld();
			City c = ACity(human, 40);
			c.AddBuilding(new PoliceStation());

			IUnit spy = g.CreateUnit(UnitType.Diplomat, 41, 25, g.PlayerNumber(ai))!;
			(spy as Diplomat)!.Sabotage(c);

			Assert.DoesNotContain(g.GetUnits(), u => u == spy);
		}

		[Fact]
		public void TheRuleReadsTheSameBothWays()
		{
			(Game g, Player ai, Player human) = AWorld();
			City policed = ACity(human, 40);
			City open = ACity(human, 44);
			policed.AddBuilding(new PoliceStation());

			Assert.True(Diplomat.SabotageProof(policed));
			Assert.False(Diplomat.SabotageProof(open));
		}

		// ── the Hospital ────────────────────────────────────────────────────

		// The organism is refused where medicine is waiting for it.
		[Fact]
		public void AHospitalRefusesTheThing()
		{
			(Game g, Player ai, Player human) = AWorld();
			City c = ACity(ai, 40);
			c.AddBuilding(new Hospital());
			byte ownerBefore = c.Owner;

			g.InfectCity(c);

			Assert.Equal(ownerBefore, c.Owner);
			Assert.DoesNotContain(g.ThingOutbreaks.Keys, k => k == (c.X, c.Y));
		}

		// ...and without one it is taken, or the test above proves nothing.
		[Fact]
		public void WithoutAHospitalTheCityIsTaken()
		{
			(Game g, Player ai, Player human) = AWorld();
			City c = ACity(ai, 40);
			byte ownerBefore = c.Owner;

			g.InfectCity(c);

			Assert.NotEqual(ownerBefore, c.Owner);
			Assert.Contains(g.ThingOutbreaks.Keys, k => k == (c.X, c.Y));
		}

		// A hospital in each neighbour is a firebreak: the outbreak spreads to the two nearest
		// cities, so refusing both is what actually ends it.
		[Fact]
		public void HospitalsInTheNeighboursContainTheOutbreak()
		{
			(Game g, Player ai, Player human) = AWorld();
			City ground = ACity(ai, 40);
			City n1 = ACity(ai, 44);
			City n2 = ACity(ai, 48);
			n1.AddBuilding(new Hospital());
			n2.AddBuilding(new Hospital());

			g.InfectCity(ground);
			g.InfectCity(n1);
			g.InfectCity(n2);

			byte tnum = g.PlayerNumber(g.Players.First(p => p is not null
			                              && p.Civilization is Civilizations.TheThing));
			Assert.Equal(tnum, ground.Owner);
			Assert.NotEqual(tnum, n1.Owner);
			Assert.NotEqual(tnum, n2.Owner);
		}

		// ── the cap ─────────────────────────────────────────────────────────

		// Three agents is a spy service; six across sixteen civs is a standing campaign.
		[Fact]
		public void ACivWithItsAgentsAbroadPlansNoMore()
		{
			(Game g, Player ai, Player human) = AWorld();
			foreach (IAdvance a in Common.Advances) ai.AddAdvance(a, false);

			// Twenty-four cities and four agents already abroad. Sized so the OLD ceiling and
			// the new one disagree: 6 there (min(6, 24/4)) against 3 here (min(3, 24/6)). At a
			// dozen cities both say "enough" and the test proves nothing — which is exactly
			// what a first version of it did.
			for (int i = 0; i < 24; i++) ACity(ai, 6 + (i % 12) * 3, y: 30 + (i / 12) * 6, size: 4);
			byte n = g.PlayerNumber(ai);
			for (int i = 0; i < 4; i++) g.CreateUnit(UnitType.Diplomat, 6, 30, n);

			Assert.True(ai.Cities.Length >= 24, "the empire must be big enough to tempt the old cap");
			City c = ai.Cities.First();
			Assert.DoesNotContain(AI.Instance(ai).ProductionPlan(c), p => p is Diplomat);
		}
	}
}
