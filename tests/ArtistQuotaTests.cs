// CivOne tests
//
// Culture bought on purpose.
//
// Steps 2 and 3 of the citizen governor only ever produce a specialist as a BYPRODUCT: a
// rioting city buys order with entertainers, a growth-capped one converts the food surplus it
// was throwing away. So a civilization chasing Cultural Ascendancy could not invest in culture
// at all — it could only be paid back for its own problems. Measured across the 7-game batch
// of 18 Aug 2026: the Culture-path Babylonians (3 cities), Persians (19 small ones) and Khmer
// made ZERO artists all game, while the Russians made 95 purely because their cities were
// capped.
//
// The artist is the only specialist worth buying deliberately, and the arithmetic says which:
// a worked tile contributes nothing to culture, and the victory is per HEAD, so the trade
// gains on the measure twice and loses only food and shields. The same move for a taxman goes
// backwards — EconomicOutput adds his 2 gold AFTER the Marketplace and Bank multipliers.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ArtistQuotaTests
	{
		// Irrigated grassland under MONARCHY, and happiness buildings enough that the city is
		// NOT rioting. Both are load-bearing. Under Despotism the tile penalty claws back any
		// yield above 2, so no city runs a surplus and the pull can never happen; and a city
		// already in disorder gets its specialists from step 2, which would let every test
		// here pass with step 4 deleted.
		private static (Game game, Player ai, City city) AHealthyCity(int size, bool aqueduct = true,
			bool irrigated = true)
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
			{
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
				Map.Instance[x, y].Irrigation = irrigated;
			}
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Government = new Monarchy();
			ai.Explore(40, 25, range: 20);
			City c = g.AddCity(ai, 0, 40, 25)!;
			c.Size = (byte)size;
			c.AddBuilding(new Temple());
			c.AddBuilding(new Colosseum());
			c.AddBuilding(new Cathedral());
			if (aqueduct) c.AddBuilding(new Aqueduct());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, ai, c);
		}

		// Setting _path alone does NOT stick: the Path property re-derives through ChoosePath
		// whenever GameTurn - _pathChosenTurn >= PathReviewInterval, and the field starts at
		// -interval-1, so the very first read discards it. Same helper as GrowthUnblockTests,
		// and the same trap it documents.
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

		private static int Artists(City c) => c.Citizens.Count(z => z == Citizen.Artist);
		private static int AllSpecialists(City c) => c.Citizens.Count(z =>
			z == Citizen.Artist || z == Citizen.Taxman || z == Citizen.Scientist || z == Citizen.Entertainer);

		// The whole point: a city with nothing wrong with it still spends citizens on culture.
		[Fact]
		public void AHealthyCultureCityBuysArtists()
		{
			(Game g, Player ai, City c) = AHealthyCity(10);
			SetPath(ai, "Culture");
			Assert.False(c.IsInDisorder, "scenario: nothing is wrong with this city");
			Assert.False(c.GrowthBlocked, "scenario: it is not capped either");
			Assert.Equal(0, AllSpecialists(c));

			AI.Instance(ai).ConsiderCitizens();

			Assert.Equal(10 / City.ArtistPerPopulace, Artists(c));
		}

		// ...and it is still a city afterwards. A food floor of 1, where the involuntary pulls
		// accept 0: culture per head rewards a civ for freezing its own growth, so the one
		// deliberate pull in the governor is the one that must not be allowed to.
		//
		// The fixture is built to put the floor on a knife edge, and it took measurement to get
		// there. The floor only ever separates two outcomes when a pull would land the surplus
		// exactly on zero: at 13 food a size-10 city pays 6 for its quota and never notices, and
		// at 3 food on plain grassland the second artist would take it to -1, which a floor of 0
		// refuses as well. Both of those pass with the floor deleted, and the negative check
		// caught the first version doing exactly that.
		//
		// So: irrigated grassland (a pull costs 3), size 6 (quota 1), and three settlers eating
		// 2 apiece under Monarchy to bring the surplus to exactly 3. The one artist the quota
		// allows would land the city on zero food — growth stopped forever, which the game
		// permits and culture per head actively rewards — and only the floor says no.
		[Fact]
		public void ItLeavesTheCityGrowing()
		{
			(Game g, Player ai, City c) = AHealthyCity(6);
			SetPath(ai, "Culture");
			for (int i = 0; i < 3; i++)
				g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(ai), false);
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == g.PlayerNumber(ai) && u is Settlers))
				u.SetHome();
			c.InvalidateCache();

			Assert.False(c.GrowthBlocked, "fixture: a capped city is allowed to stop growing");
			Assert.Equal(3, c.FoodIncome);   // exactly one pull from standing still

			AI.Instance(ai).ConsiderCitizens();

			Assert.True(c.FoodIncome > 0, $"the city stopped growing to buy culture ({c.FoodIncome})");
		}

		// A fifth of the citizens, not all of them.
		//
		// Sizes chosen to stay under the growth cap: an Aqueduct carries a city to 12 and no
		// further, so a size-15 fixture is CAPPED and step 3 hands it specialists on top of the
		// quota — which is correct behaviour and useless for measuring the quota.
		[Theory]
		[InlineData(6, 1)]
		[InlineData(10, 2)]
		[InlineData(11, 2)]
		public void TheQuotaIsAFifth(int size, int expected)
		{
			(Game g, Player ai, City c) = AHealthyCity(size);
			SetPath(ai, "Culture");
			Assert.False(c.GrowthBlocked, "fixture: a capped city gets specialists for free");

			AI.Instance(ai).ConsiderCitizens();

			Assert.Equal(expected, Artists(c));
		}

		// Below the floor every citizen is load-bearing and a city that gives one up stops
		// being a city.
		[Fact]
		public void ASmallCityIsLeftAlone()
		{
			(Game g, Player ai, City c) = AHealthyCity(City.ArtistCityFloor - 1);
			SetPath(ai, "Culture");

			AI.Instance(ai).ConsiderCitizens();

			Assert.Equal(0, AllSpecialists(c));
		}

		// Nobody else buys them. The taxman version of this trade LOSES output, and the
		// scientist version wins nothing any victory reads.
		[Theory]
		[InlineData("Diaspora")]
		[InlineData("Commerce")]
		[InlineData("Conquest")]
		[InlineData("Endurance")]
		public void CivsOnOtherPathsBuyNoSpecialists(string path)
		{
			(Game g, Player ai, City c) = AHealthyCity(10);
			SetPath(ai, path);

			AI.Instance(ai).ConsiderCitizens();

			Assert.Equal(0, AllSpecialists(c));
		}

		// The allocation is stable across turns — the release pass in step 1 runs before the
		// pull and puts specialists back to work while it is safe to.
		[Fact]
		public void TheArtistsSurviveTheNextTurnsPass()
		{
			(Game g, Player ai, City c) = AHealthyCity(11);
			SetPath(ai, "Culture");
			AI.Instance(ai).ConsiderCitizens();
			int first = Artists(c);
			Assert.True(first > 0, "scenario: it bought none to begin with");

			AI.Instance(ai).ConsiderCitizens();
			AI.Instance(ai).ConsiderCitizens();

			Assert.Equal(first, Artists(c));
		}

		// ...and it gets there without churning. Stopping the release at the quota rather than
		// at zero has NO effect on the allocation — release-then-rebuy converges on the same
		// citizens, which is why the test above passes either way and why this one is pinned on
		// the source instead. What it changes is the work: every artist released and bought
		// back is two tile toggles and two cache invalidations, per city, per turn, on the path
		// that produced the turn-328 governor hang. Step 1's own comment makes the same
		// argument about the same loop.
		[Fact]
		public void TheReleasePassStopsAtTheQuotaNotAtZero()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "City.cs"));

			Assert.Contains("while (_specialists.Count > artistQuota)", src);
		}

		// A growth cap still gets its full rebate — the quota is a floor under the release
		// pass, not a ceiling on the byproduct.
		[Fact]
		public void ACappedCityStillConvertsItsWholeWastedSurplus()
		{
			(Game g, Player ai, City c) = AHealthyCity(12, aqueduct: false);
			SetPath(ai, "Culture");
			Assert.True(c.GrowthBlocked, "scenario: this city is supposed to be capped");

			AI.Instance(ai).ConsiderCitizens();

			Assert.True(Artists(c) > 12 / City.ArtistPerPopulace,
				$"the cap should give more than the quota, got {Artists(c)}");
		}

		// The free half of the change: a Commerce civ's EXISTING specialists — the ones a cap
		// or a riot produced anyway — are typed as taxmen whatever its treasury looks like.
		// Their gold now reaches GrossOutput, which is the measure Pax Mercatoria reads, so
		// for that civ the choice is no longer gold-versus-research but progress-versus-none.
		[Fact]
		public void ACommerceCivTypesItsSpecialistsAsTaxmen()
		{
			(Game g, Player ai, City c) = AHealthyCity(12, aqueduct: false);
			SetPath(ai, "Commerce");
			ai.Gold = 5000;   // a thin treasury would pick the taxman for the old reason
			Assert.True(c.GrowthBlocked, "scenario: the cap is what produces the specialists");

			AI.Instance(ai).ConsiderCitizens();

			Assert.True(c.Citizens.Count(z => z == Citizen.Taxman) > 0, "no taxmen at all");
			Assert.Equal(0, c.Citizens.Count(z => z == Citizen.Scientist));
		}
	}
}
