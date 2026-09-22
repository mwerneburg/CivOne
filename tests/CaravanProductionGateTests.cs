// CivOne tests
//
// A caravan with nowhere to deliver is shields burned and a build slot held.
//
// The production gate asked only "do I know Trade, and am I under my cap" — it never asked
// whether a deliverable partner existed. Measured on a 1625 AD save: the Zulus' nearest
// foreign city was 52 tiles away and they held three caravans continuously, forming 1.5
// routes in 325 turns. The Aztecs had exactly ONE candidate partner city in the world and
// formed none. Meanwhile the random fallback in the same method already excluded ICaravan
// as "needs a destination" — only the deliberate path failed to ask.
//
// The predicate mirrors the two rules the delivery path actually enforces: same NAMED
// continent (the targeting), and at least CaravanMinRange tiles from home (Caravan.MoveTo).

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CaravanProductionGateTests
	{
		// One continent, our city at x=30. `partnerX` places the only foreign city; pass a
		// negative to leave us alone in the world.
		private static (Game g, Player us) AWorldWithAPartnerAt(int partnerX)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player us = ps[0], them = ps[1];
			foreach (Player p in new[] { us, them })
			{
				p.Government = new CivOne.Governments.Monarchy();
				p.Explore(45, 25, range: 40);
			}
			us.AddAdvance(new Trade(), false);

			City home = g.AddCity(us, 0, 30, 25)!;
			home.Size = 8;
			if (partnerX >= 0)
			{
				City theirs = g.AddCity(them, 1, partnerX, 25)!;
				theirs.Size = 8;
			}
			Sim.ClearTasks();
			return (g, us);
		}

		private static bool Reaches(Game g, Player p) => AI.Instance(p).HasTradePartner();

		// The fixture has to be able to say yes, or every negative below passes for the
		// wrong reason.
		[Fact]
		public void ADistantPartnerIsReachable()
		{
			(Game g, Player us) = AWorldWithAPartnerAt(60);   // 30 tiles off

			Assert.True(Reaches(g, us), "a partner 30 tiles away was not seen");
		}

		// Alone in the world: nobody to trade with, so nothing to build a caravan for.
		[Fact]
		public void NoForeignCityMeansNoPartner()
		{
			(Game g, Player us) = AWorldWithAPartnerAt(-1);

			Assert.False(Reaches(g, us), "a civ alone in the world thinks it can trade");
		}

		// The Caravan.MoveTo rule, mirrored: a partner inside the refusal radius is not a
		// partner. This is the Roman case from the save — neighbours 6 tiles away.
		[Fact]
		public void APartnerInsideTheRefusalRadiusDoesNotCount()
		{
			// CaravanMinRange is 10, so 8 tiles off is refused on arrival.
			(Game g, Player us) = AWorldWithAPartnerAt(38);

			Assert.False(Reaches(g, us),
				$"a partner {38 - 30} tiles away counts, but delivery refuses under "
				+ $"{AI.CaravanMinRange}");
		}

		// ...and exactly at the boundary it does count, so the predicate and the mover agree
		// on the edge case rather than differing by one.
		[Fact]
		public void TheBoundaryItselfCounts()
		{
			(Game g, Player us) = AWorldWithAPartnerAt(30 + AI.CaravanMinRange);

			Assert.True(Reaches(g, us), "the mover trades at exactly CaravanMinRange");
		}

		// A partner across water is no partner for a land unit. The targeting only considers
		// the same NAMED continent for exactly this reason — "so we don't dispatch the unit
		// on an impossible walk across the ocean".
		//
		// A negative check found this untested: deleting the continent rule broke nothing,
		// because every other fixture here is a single landmass.
		[Fact]
		public void APartnerAcrossTheWaterDoesNotCount()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			// Two landmasses, a nine-tile channel between them, both wide enough to be a
			// continent in their own right rather than falling into the "misc" bucket.
			for (int y = 15; y <= 35; y++)
			{
				for (int x = 10; x <= 34; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				for (int x = 35; x <= 43; x++) Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
				for (int x = 44; x <= 68; x++) Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player us = ps[0], them = ps[1];
			foreach (Player p in new[] { us, them })
			{
				p.Government = new CivOne.Governments.Monarchy();
				p.Explore(40, 25, range: 40);
			}
			us.AddAdvance(new Trade(), false);

			City home = g.AddCity(us, 0, 22, 25)!;     home.Size = 8;
			City theirs = g.AddCity(them, 1, 56, 25)!; theirs.Size = 8;
			Sim.ClearTasks();

			// The fixture is only meaningful if they really are separated.
			Assert.NotEqual(home.Tile!.ContinentId, theirs.Tile!.ContinentId);

			Assert.False(Reaches(g, us), "an overseas city counts as a caravan partner");
		}

		// The whole point: the planner stops commissioning them.
		[Fact]
		public void AnIsolatedCivDoesNotBuildCaravans()
		{
			(Game g, Player us) = AWorldWithAPartnerAt(-1);
			City home = us.Cities[0];

            Assert.DoesNotContain(AI.Instance(us).ProductionPlan(home), p => p is CivOne.Units.Caravan);
		}

		// ...and a civ with a real partner still does, so the gate narrows rather than bans.
		[Fact]
		public void ACivWithAPartnerStillBuildsThem()
		{
			(Game g, Player us) = AWorldWithAPartnerAt(60);
			City home = us.Cities[0];

			Assert.Contains(AI.Instance(us).ProductionPlan(home), p => p is CivOne.Units.Caravan);
		}
	}
}
