// CivOne tests
//
// The mirror of the culture governor, for the other victory a spare citizen can be spent on.
//
// EconomicOutput counts a Taxman at 2, added after the Marketplace and Bank multipliers, and
// the AI's Commerce path types its specialists as taxmen. A human could not: Player.AI is null
// without Autopilot, so `preferred` fell through to `Gold < LeanTreasury ? Taxman : Scientist`
// — and the civ chasing Pax Mercatoria is exactly the one that is never short of gold. Every
// spare citizen in a merchant empire was typed Scientist, which counts toward no victory.
//
// Measured in game 553f5adc at turn 420: 97 artists on a Guarani civ 480 output short of the
// economic bar, and unable to win the culture race those artists were paying for (93.5 per
// head against Russia's 137.5, with a 1.10x margin required on top). Retyped: +194.
//
// The treasury is set FAT in every fixture here on purpose. At the default the taxman fallback
// fires on its own and every assertion below would pass against a governor that does nothing.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class CommerceGovernorTests
	{
		// Deliberately the culture governor's fixture, on the same terms: Monarchy and
		// irrigated grassland, because under Despotism the tile penalty leaves no surplus and
		// a city with nothing to spare proves nothing. Temple and Colosseum so the spare
		// citizens are sparable rather than holding a riot together.
		private static (Game game, Player human, City city) AWealthyHumanCity(int size)
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Irrigation = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Government = new Monarchy();
			human.Gold = 5000;          // see the header: at 0 gold the fallback types taxmen anyway
			human.Explore(40, 25, range: 20);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.Size = (byte)size;
			c.AddBuilding(new Temple());
			c.AddBuilding(new Colosseum());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			Assert.Null(human.AI);      // the condition the whole defect lives under
			return (g, human, c);
		}

		private static int Taxmen(City c) => c.Citizens.Count(z => z == Citizen.Taxman);
		private static int Scientists(City c) => c.Citizens.Count(z => z == Citizen.Scientist);
		private static int Specialists(City c) => c.Citizens.Count(z =>
			z == Citizen.Entertainer || z == Citizen.Taxman
			|| z == Citizen.Scientist || z == Citizen.Artist);

		// The fixture has to produce spare citizens at all, or every test below is vacuous.
		[Fact]
		public void TheCityHasCitizensToSpare()
		{
			(_, _, City c) = AWealthyHumanCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: false, commerce: false);

			Assert.True(Specialists(c) > 0, "the fixture city spares no citizens");
		}

		[Fact]
		public void TheCommerceGovernorTypesSpecialistsAsTaxmen()
		{
			(_, _, City c) = AWealthyHumanCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: false, commerce: true);

			Assert.True(Taxmen(c) > 0, "the commerce governor made no taxmen");
			Assert.Equal(0, Scientists(c));
		}

		// The half that fails if the flag is ignored and the treasury test is doing the work:
		// the same wealthy city, not enrolled, still gets scientists.
		[Fact]
		public void AWealthyCityWithoutItStillGetsScientists()
		{
			(_, _, City c) = AWealthyHumanCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: false, commerce: false);

			Assert.True(Scientists(c) > 0, "fixture: this city was not producing scientists to begin with");
			Assert.Equal(0, Taxmen(c));
		}

		// It must NOT buy. The artist quota is gated on the Culture path for a reason spelled
		// out at step 4: a worked tile pays no culture, so an artist is a clean gain, while the
		// taxman's 2 gold lands AFTER the Marketplace and Bank multipliers and a citizen worth
		// 6 on a trade tile is worth 2 as a specialist. A commerce governor that pulled workers
		// would LOWER the standing it exists to raise.
		//
		// The city needs an AQUEDUCT, and that is the whole test. Without one a size-10 city is
		// capped at 8, step 3 frees more citizens than the quota would ever buy, and the quota
		// never bites — so a commerce governor wired to buy passes anyway. The first draft of
		// this test did exactly that, and the negative check that deliberately turned buying ON
		// still went green. Uncapped, the city spares nothing on its own, and every specialist
		// present is one that was BOUGHT.
		private static City AnUncappedCity(int size)
		{
			(_, _, City c) = AWealthyHumanCity(size);
			c.AddBuilding(new Aqueduct());
			c.ResetResourceTiles();
			return c;
		}

		[Fact]
		public void TheCultureGovernorBuysInThisFixture()
		{
			City c = AnUncappedCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: true, commerce: false);

			Assert.True(Specialists(c) >= 10 / City.ArtistPerPopulace,
				"fixture: the quota does not bite here, so the test below proves nothing");
		}

		[Fact]
		public void TheCommerceGovernorBuysNoCitizens()
		{
			City c = AnUncappedCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: false, commerce: true);

			Assert.Equal(0, Specialists(c));
		}

		// The point of the feature, in the unit the victory is measured in.
		[Fact]
		public void TheTaxmenReachTheEconomicMeasure()
		{
			(_, _, City plain) = AWealthyHumanCity(size: 10);
			plain.AutoAssignCitizens(order: true, growth: true, culture: false, commerce: false);
			int before = plain.EconomicOutput;

			(_, _, City c) = AWealthyHumanCity(size: 10);
			c.AutoAssignCitizens(order: true, growth: true, culture: false, commerce: true);

			// Without this the assertion below reads "before + 0 == before" the moment the
			// governor stops making taxmen, and passes against the very defect it covers —
			// which is what the negative check found it doing.
			Assert.True(Taxmen(c) > 0, "no taxmen: the measure test has nothing to measure");
			Assert.Equal(before + Taxmen(c) * City.TaxmanOutput, c.EconomicOutput);
		}

		// Enrolment survives a save, or the player re-enrols every city on every load. Bit 3 of
		// the Governors field; a save written before it existed reads back as off.
		[Fact]
		public void TheCommerceEnrolmentSurvivesASave()
		{
			(Game g, _, City c) = AWealthyHumanCity(size: 10);
			c.GovernorOrder = c.GovernorGrowth = c.GovernorCommerce = true;
			c.GovernorCulture = false;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "commercegov.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			City back = Game.Instance.GetCities().First(x => x.X == 40 && x.Y == 25);
			Assert.True(back.GovernorCommerce);
			Assert.False(back.GovernorCulture);
			Assert.True(back.GovernorOrder);
			Assert.True(back.GovernorGrowth);
		}

		// What the player reads on the city screen.
		[Fact]
		public void TheCityScreenNamesIt()
		{
			(_, _, City c) = AWealthyHumanCity(size: 10);
			c.GovernorOrder = c.GovernorGrowth = c.GovernorCommerce = true;

			Assert.Equal("COMMERCE", CivOne.Screens.CityManager.GovernorLabel(c));
		}
	}
}
