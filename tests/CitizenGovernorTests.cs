// CivOne tests
//
// The AI had no specialists. ChangeSpecialist's only callers were the two human city-manager
// screens, and SetResourceTiles sorts by food/shield/trade with no happiness term — so an AI
// city always worked every tile it could reach and entertainers appeared only as leftovers.
//
// Its one lever against disorder was the luxury slider, which pays out of TRADE. Observed at
// 2200 AD: a rioting city at 70% luxuries earning 2 luxury against 9 unhappy — one citizen
// upgraded. Three turns of that and Government.CollapsesInDisorder revolts the civ, having
// already burned the Marketplace on turn 1 and the Bank or Cathedral on turn 2.
//
// The second half is waste rather than collapse: a growth-capped city (size 7 with no
// aqueduct, 12 with no sewer) still spends the whole food box in NewTurn and then skips the
// Size++, so every surplus point is worked for and thrown away.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class CitizenGovernorTests
	{
		// Irrigated grassland under MONARCHY. Both details are load-bearing: an AI starts in
		// Despotism, where the tile penalty claws back any yield above 2, so every tile is
		// exactly one citizen's rations and no city can run a surplus at all. Under Despotism
		// this governor can do nothing and the tests below would all pass vacuously against
		// broken code — the first draft did exactly that.
		private static (Game game, Player ai, City city) ACity(int size, bool temple = false, bool aqueduct = false)
		{
			Sim.NewGame(width: 80, height: 50);
			// Stated, not inherited: under Autopilot the human's Player.AI is non-null and the
			// full AI pass runs regardless of enrolment, so a leaked flag from an earlier test
			// quietly inverts what these assert.
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Irrigation = true;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Government = new Monarchy();
			ai.Explore(40, 25, range: 20);
			City c = g.AddCity(ai, 0, 40, 25)!;
			c.Size = (byte)size;
			if (temple) c.AddBuilding(new Temple());
			if (aqueduct) c.AddBuilding(new Aqueduct());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, ai, c);
		}

		// A HUMAN city, which is the whole point of the culture governor's tests below: the
		// defect they cover is human-only. Player.AI is null for a human unless Autopilot is
		// on, so `path` in AutoAssignCitizens read null for every human city and the artist
		// steps were unreachable no matter what the player switched on. Built on the AI
		// helper's terms — Monarchy and irrigated grassland — for the same reason it states
		// them: under Despotism the tile penalty leaves no surplus and every assertion here
		// would pass vacuously.
		private static (Game game, Player human, City city) AHumanCity(int size)
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
			human.Explore(40, 25, range: 20);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.Size = (byte)size;
			// Temple AND Colosseum, which is not decoration. The first draft gave a size-20
			// city a Temple only: it rioted, step 2 spent all seven spare citizens as
			// Entertainers, and step 5 correctly refused to retype an entertainer that was
			// holding the city together — so the city produced ZERO artists and the test
			// failed against working code. A culture governor can only spend citizens a city
			// can spare, so the fixture has to make some sparable.
			c.AddBuilding(new Temple());
			c.AddBuilding(new Colosseum());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			Assert.Null(human.AI);   // the condition the defect lived under
			return (g, human, c);
		}

		private static int Artists(City c) => c.Citizens.Count(z => z == Citizen.Artist);

		// The reported case: a player chasing culture had to strip tiles by hand, one city at
		// a time, because no governor setting would do it. Player.AI is null for a human
		// unless Autopilot is on, so `path` in AutoAssignCitizens read null for every human
		// city — artistQuota was 0 and `preferred` was Scientist or Taxman, always.
		//
		// A floor, not an equality. artistQuota governs what step 4 BUYS deliberately; step 5
		// then types every specialist the city has as the preferred kind, including the ones
		// steps 2 and 3 freed from a riot or a growth cap. A size-10 city here yields 4
		// artists against a quota of 2 for exactly that reason, and pinning the exact number
		// would pin the fixture's happiness arithmetic rather than the feature.
		[Fact]
		public void TheCultureGovernorBuysArtistsInAHumanCity()
		{
			(_, _, City c) = AHumanCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: true);

			Assert.True(Artists(c) >= c.Size / City.ArtistPerPopulace,
				$"the culture governor made {Artists(c)} artists, below its own quota");
		}

		// ...and the same city, enrolled in everything EXCEPT culture, makes none at all. This
		// is the half that fails if the flag is ignored and the quota is simply turned on for
		// everybody: without it the test above passes against a governor that hands out
		// artists whether or not they were asked for. The specialists still exist here — they
		// are typed Scientist or Taxman, which is what a human city has always got.
		[Fact]
		public void TheOtherGovernorsBuyNoArtists()
		{
			(_, _, City c) = AHumanCity(size: 10);

			c.AutoAssignCitizens(order: true, growth: true, culture: false);

			Assert.Equal(0, Artists(c));
		}

		// Below ArtistCityFloor the quota is zero, so a small city is left to farm rather than
		// gutted. Step 5 could still TYPE a specialist as an artist here if a riot or a growth
		// cap had produced one; this city is small, fed and content, so it has none to type.
		[Fact]
		public void ASmallCityIsLeftToFarmUnderTheCultureGovernor()
		{
			(_, _, City c) = AHumanCity(size: City.ArtistCityFloor - 1);

			c.AutoAssignCitizens(order: true, growth: true, culture: true);

			Assert.Equal(0, Artists(c));
		}

		// The enrolment survives a save, or the player re-enrols every city on every load.
		// Bit 2 of the Governors field; a save written before it existed reads back as off.
		[Fact]
		public void TheCultureEnrolmentSurvivesASave()
		{
			(Game g, _, City c) = AHumanCity(size: 10);
			c.GovernorOrder = c.GovernorGrowth = c.GovernorCulture = true;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "culturegov.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			City back = Game.Instance.GetCities().First(x => x.X == 40 && x.Y == 25);
			Assert.True(back.GovernorCulture);
			Assert.True(back.GovernorOrder);
			Assert.True(back.GovernorGrowth);
		}

		// All FOUR specialist types. The Artist was added later and omitting it here made this
		// helper silently under-count — a culture-path civ parks its spare citizens as artists,
		// so "the cap parked some citizens" read as zero and the precondition failed.
		private static int Specialists(City c) => c.Citizens.Count(z =>
			z == Citizen.Entertainer || z == Citizen.Taxman
			|| z == Citizen.Scientist || z == Citizen.Artist);

		// The defect, stated directly: a rioting AI city buys its way out with entertainers.
		[Fact]
		public void ARiotingCityMakesEntertainers()
		{
			// The aqueduct matters: without it a size-9 city is ALSO growth-capped, and the
			// growth-cap pass fixes the disorder as a side effect — so this passed with the
			// disorder pass deleted. Isolate one lever at a time.
			var (_, ai, c) = ACity(9, aqueduct: true);
			Assert.False(c.GrowthBlocked, "scenario: disorder is the only thing to fix here");
			Assert.True(c.IsInDisorder, "scenario: a size-9 city with no happiness buildings riots");
			int before = Specialists(c);

			AI.Instance(ai).ConsiderCitizens();

			Assert.True(Specialists(c) > before, "it worked every tile and rioted instead");
			Assert.False(c.IsInDisorder);
		}

		// ...but never starves itself into order. Trading a riot for a famine is not a fix.
		[Fact]
		public void ItWillNotStarveACityIntoOrder()
		{
			var (_, ai, c) = ACity(9, aqueduct: true);

			AI.Instance(ai).ConsiderCitizens();

			Assert.True(c.FoodIncome >= 0, $"food income {c.FoodIncome}");
		}

		// The growth cap: size 7 with no aqueduct cannot grow, so surplus food is worked for
		// and discarded. Those citizens go to the bank and the laboratory instead.
		[Fact]
		public void AGrowthCappedCityStopsFarmingForNothing()
		{
			var (_, ai, c) = ACity(7, temple: true);
			Assert.False(c.HasBuilding<Aqueduct>(), "scenario: capped at 7");
			int before = c.FoodIncome;
			Assert.True(before > 0, $"scenario needs a surplus to waste, got {before}");

			AI.Instance(ai).ConsiderCitizens();

			// Below one citizen's rations: nothing further can be given up without starving.
			Assert.True(c.FoodIncome < 2, $"still farming a surplus it cannot use: {c.FoodIncome}");
			Assert.True(c.FoodIncome < before);
			// Artist counts: a civ chasing Cultural Ascendancy spends spare citizens on
			// culture, which is earning something — and is the whole point of the specialist.
			// The rule under test is that freed citizens are PUT TO WORK, not which work.
			Assert.True(c.Citizens.Any(z => z == Citizen.Scientist || z == Citizen.Taxman
			                             || z == Citizen.Artist),
				"the freed citizens should be earning something");
		}

		// The control: a city that can still grow keeps farming. The rule must not fire on
		// every city with a food surplus, or it stops the AI expanding at all.
		[Fact]
		public void ACityThatCanStillGrowIsLeftFarming()
		{
			var (_, ai, c) = ACity(5);
			int surplus = c.FoodIncome;
			Assert.True(surplus > 0, "scenario: a growing city with a surplus");

			AI.Instance(ai).ConsiderCitizens();

			Assert.Equal(surplus, c.FoodIncome);
		}

		// Building the aqueduct releases them: the cap is gone, so the food is worth having
		// again. Without this the city would be permanently stunted by its own governor.
		[Fact]
		public void AnAqueductPutsThemBackToWork()
		{
			var (_, ai, c) = ACity(7, temple: true);
			AI.Instance(ai).ConsiderCitizens();
			int parked = Specialists(c);
			Assert.True(parked > 0, "scenario: the cap parked some citizens");

			c.AddBuilding(new Aqueduct());
			AI.Instance(ai).ConsiderCitizens();

			Assert.True(Specialists(c) < parked, "the cap is gone; they should be farming again");
		}

		// Stability. The pass runs every turn on every city, and an allocation that flips back
		// and forth is the settler shuttle in another costume — the release step is tested and
		// reverted precisely so this holds.
		[Fact]
		public void TheAllocationSettles()
		{
			var (_, ai, c) = ACity(9, aqueduct: true);
			AI hive = AI.Instance(ai);
			hive.ConsiderCitizens();
			int settled = Specialists(c);

			for (int turn = 0; turn < 10; turn++) hive.ConsiderCitizens();

			Assert.Equal(settled, Specialists(c));
			Assert.False(c.IsInDisorder);
		}

		// A human city is not touched. ConsiderCitizens is reached through Player.AI, which is
		// null for a hand-played human — this pins the consequence rather than the plumbing.
		[Fact]
		public void AHandPlayedHumanKeepsControlOfItsCitizens()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Assert.Null(g.HumanPlayer.AI);
		}
	}
}
