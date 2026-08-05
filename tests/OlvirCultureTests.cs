// CivOne tests
//
// The Olvir finished a 750-turn game with 35 cities and 0 culture. Nothing excluded them:
// City.CultureRate counts Temple, Colosseum, Library, Cathedral, University, Civic
// Monument, The Internet and wonders — and the Olvir production list (AI.Strategy:3033)
// is defender, granary, settlers, HydroEngineer, Xenolab, Sea Platform. The two sets did
// not intersect at a single entry, so zero was the only value their culture could take.
//
// The Breeding Shrine and the Cascade Cathedral are entries they can reach: their own
// religion, gated by civilization rather than technology.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class OlvirCultureTests
	{
		// Mirrors Player.PointsPerColony; if that changes this must, deliberately.
		private const int PointsPerColony = 8;

		private static (Game, Player olvir, Player human) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player olvir = g.Players.FirstOrDefault(p => p is not null
			                    && p.Civilization is Civilizations.Olvir)!;
			if (olvir is null)
			{
				// The Olvir join mid-game; for a unit test, seat one directly.
				olvir = new Player(Common.Civilizations.First(c => c is Civilizations.Olvir));
				g.AddPlayer(olvir);
			}
			return (g, olvir, g.HumanPlayer);
		}

		private static City AnOlvirCity(Game g, Player olvir, int x = 40, int y = 25)
		{
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			olvir.Explore(x, y, range: 3);
			City c = g.AddCity(olvir, 0, x, y)!;
			c.Size = 8;
			return c;
		}

		// The defect, stated directly: an Olvir city with their own buildings earns culture.
		[Fact]
		public void AnOlvirCityWithAShrineEarnsCulture()
		{
			(Game g, Player olvir, Player human) = AWorld();
			City c = AnOlvirCity(g, olvir);
			Assert.Equal(0, c.CultureRate);

			c.AddBuilding(new BreedingShrine());
			Assert.True(c.CultureRate > 0, "a Breeding Shrine must earn culture");

			int withShrine = c.CultureRate;
			c.AddBuilding(new CascadeCathedral());
			Assert.True(c.CultureRate > withShrine, "the cathedral must add more");
		}

		// Only the Olvir. A human city must never be offered the spawning rite.
		[Fact]
		public void HumansCannotBuildTheOlvirReligion()
		{
			(Game g, Player olvir, Player human) = AWorld();
			Assert.False(human.ProductionAvailable(new BreedingShrine()));
			Assert.False(human.ProductionAvailable(new CascadeCathedral()));
		}

		[Fact]
		public void TheOlvirCanBuildTheShrine()
		{
			(Game g, Player olvir, Player human) = AWorld();
			AnOlvirCity(g, olvir);
			Assert.True(olvir.ProductionAvailable(new BreedingShrine()));
		}

		// ── happiness ───────────────────────────────────────────────────────
		//
		// The two buildings earned culture but did nothing for the colonists living in them,
		// which made them a scoring device rather than a religion. The shrine is a temple's
		// worth of peace; the cathedral rates with a human Cathedral.

		[Fact]
		public void TheShrineCalmsTheColony()
		{
			(Game g, Player olvir, Player human) = AWorld();
			City c = AnOlvirCity(g, olvir);
			int unhappyBefore = c.UnhappyCitizens;

			c.AddBuilding(new BreedingShrine());
			Assert.True(c.UnhappyCitizens < unhappyBefore || unhappyBefore == 0,
				$"the shrine must settle the colony ({c.UnhappyCitizens} vs {unhappyBefore})");
		}

		// The cathedral is the stronger of the two, as its cost and its culture both say.
		[Fact]
		public void TheCathedralCalmsMoreThanTheShrine()
		{
			(Game g, Player olvir, Player human) = AWorld();
			City shrine = AnOlvirCity(g, olvir, x: 40);
			City cathedral = AnOlvirCity(g, olvir, x: 44);
			shrine.Size = cathedral.Size = 12;   // big enough that there is unrest to settle

			shrine.AddBuilding(new BreedingShrine());
			cathedral.AddBuilding(new BreedingShrine());
			cathedral.AddBuilding(new CascadeCathedral());

			Assert.True(cathedral.UnhappyCitizens < shrine.UnhappyCitizens,
				$"the cathedral must calm more than the shrine ({cathedral.UnhappyCitizens} vs {shrine.UnhappyCitizens})");
		}

		// ── score ───────────────────────────────────────────────────────────

		// A refugee fleet that has settled the globe must not read as a failure. Every other
		// term in Score rewards monuments and research, which the Olvir deliberately skip.
		[Fact]
		public void TheOlvirAreScoredOnTheirSpread()
		{
			(Game g, Player olvir, Player human) = AWorld();
			Player ordinary = g.Players.First(p => p is not null && p != human && p != olvir
			                                    && g.PlayerNumber(p) != 0);

			// What each player GAINS from six identical cities. Absolute scores can't be
			// compared — the two start with different advances and territory — but the deltas
			// cancel that, leaving only the colony credit. Comparing the Olvir against their
			// own earlier score proves nothing either: six size-8 cities move the population
			// term on their own, which is how a weaker version of this passed against the
			// unfixed code.
			//
			// Both bands are explored for both players FIRST. Founding calls Explore, and the
			// two settle different ground, so otherwise each gains a different number of newly
			// revealed tiles — worth 2 points here, enough to make the comparison inexact.
			foreach (Player p in new[] { olvir, ordinary })
				for (int i = 0; i < 6; i++)
				{
					p.Explore(30 + i * 3, 20, range: 4);
					p.Explore(30 + i * 3, 40, range: 4);
				}

			int olvirBefore = olvir.Score, ordinaryBefore = ordinary.Score;
			for (int i = 0; i < 6; i++)
			{
				AnOlvirCity(g, olvir, x: 30 + i * 3, y: 20);
				City c = AnOlvirCity(g, ordinary, x: 30 + i * 3, y: 40);
				c.Size = 8;
			}
			int olvirGain = olvir.Score - olvirBefore;
			int ordinaryGain = ordinary.Score - ordinaryBefore;

			Assert.Equal(olvir.Cities.Length, ordinary.Cities.Length);
			Assert.Equal(olvir.Population, ordinary.Population);
			Assert.Equal(ordinaryGain + 6 * PointsPerColony, olvirGain);
		}

		// ...but only the Olvir. Nobody else is paid for planting cities.
		[Fact]
		public void NobodyElseIsPaidForColonies()
		{
			(Game g, Player olvir, Player human) = AWorld();
			Player other = g.Players.First(p => p is not null && p != human && p != olvir
			                                 && g.PlayerNumber(p) != 0);
			int before = other.Score;

			Map.Instance.ChangeTileType(60, 30, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			other.Explore(60, 30, range: 3);
			City c = g.AddCity(other, 5, 60, 30)!;

			// Population still counts, so allow that — just not a colony bounty on top.
			Assert.True(other.Score - before < 8, "colony credit is the refugee fleet's alone");
		}

		// The cathedral is the shrine grown monumental — no colony plumbs one first.
		[Fact]
		public void TheCathedralRequiresAShrineSomewhere()
		{
			(Game g, Player olvir, Player human) = AWorld();
			City c = AnOlvirCity(g, olvir);
			Assert.False(olvir.ProductionAvailable(new CascadeCathedral()));

			c.AddBuilding(new BreedingShrine());
			Assert.True(olvir.ProductionAvailable(new CascadeCathedral()));
		}
	}
}
