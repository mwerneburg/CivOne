// CivOne tests
//
// Civ 1 priced a revolt as (owner's gold + 1000) / (distance from their capital + 3). Only
// the OWNER'S TREASURY entered — nothing about the city. AI treasuries run near empty, so any
// city sixteen or more tiles from its capital cost (0 + 1000) / 19 = 52 gold, whether it was a
// size-1 hamlet or a size-12 metropolis with a temple, a library, an aqueduct and a wonder.
//
// The rewrite prices the city itself — citizens, improvements, wonders, a quarter of the
// treasury — and applies the owner's GRIP as a multiplier: tighter near the seat of
// government, tighter again for a large empire. Distance modulates the grip, not the worth,
// because a wonder twenty tiles from the palace is still a wonder.
//
// Note on sizes: a city above about size 6 with no happiness buildings falls into disorder,
// which halves the price by design. Comparisons below keep both sides calm unless the test is
// specifically about that halving.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;
using CivOne.Wonders;
using Xunit.Abstractions;

namespace CivOne.Tests
{
	public class InciteCostTests
	{
		private readonly ITestOutputHelper _out;
		public InciteCostTests(ITestOutputHelper output) => _out = output;

		private static (Game, Player) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0 && q != g.HumanPlayer);
			return (g, p);
		}

		// Cities sit on grassland at distinct name ids so nothing is skipped for terrain.
		private static City ACity(Player owner, int x, int size, bool capital = false, int y = 25)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(x, y, range: 3);
			City c = g.AddCity(owner, x + y, x, y)!;
			c.Size = (byte)size;
			if (capital) c.AddBuilding(new Palace());
			return c;
		}

		private static City Develop(City c, params IBuilding[] buildings)
		{
			foreach (IBuilding b in buildings) c.AddBuilding(b);
			return c;
		}

		// The defect, stated directly: a hamlet and a developed city the same distance from the
		// same capital used to carry an identical price, because neither entered the formula.
		[Fact]
		public void ADevelopedCityCostsMoreThanAHamlet()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 4, capital: true);

			City hamlet = ACity(ai, 40, 1);
			City developed = Develop(ACity(ai, 41, 6),
				new Temple(), new Library(), new Aqueduct(), new MarketPlace());

			int cheap = Diplomat.InciteCost(hamlet);
			int dear = Diplomat.InciteCost(developed);
			Assert.False(hamlet.IsInDisorder || developed.IsInDisorder, "both must be calm to compare");

			_out.WriteLine($"size 1, bare:               {cheap}");
			_out.WriteLine($"size 6, 4 improvements:     {dear}");
			Assert.True(dear > cheap * 4, $"a developed city must cost far more ({dear} vs {cheap})");
		}

		// The ask, directly: a city near the seat of government is not for sale.
		[Fact]
		public void ACityNearTheCapitalCostsMoreThanAFrontierTown()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 6, capital: true);

			City home = Develop(ACity(ai, 22, 5), new Temple(), new Library());
			City frontier = Develop(ACity(ai, 60, 5), new Temple(), new Library());

			int near = Diplomat.InciteCost(home);
			int far = Diplomat.InciteCost(frontier);

			_out.WriteLine($"2 tiles from the palace:    {near}");
			_out.WriteLine($"40 tiles from the palace:   {far}");
			Assert.True(near > far * 3 / 2, $"the home provinces must be dear ({near} vs {far})");
		}

		// ...but the frontier is no longer worthless. Civ 1 divided the whole price by distance,
		// so a developed remote city went for pocket change; only the grip should thin out.
		[Fact]
		public void AFrontierCityIsCheaperButNotWorthless()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 6, capital: true);
			City frontier = Develop(ACity(ai, 60, 6),
				new Temple(), new Library(), new Aqueduct());

			int cost = Diplomat.InciteCost(frontier);
			_out.WriteLine($"size 6, 3 improvements, 40 tiles out: {cost}");
			Assert.True(cost > 1000, $"a developed frontier city must still cost real money (got {cost})");
		}

		// A wonder is the largest thing a city can carry; losing one to a bag of gold should be
		// close to unthinkable.
		[Fact]
		public void AWonderMakesACityFarDearer()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 6, capital: true);
			City c = ACity(ai, 30, 5);

			int without = Diplomat.InciteCost(c);
			c.AddWonder(new Pyramids());
			int with = Diplomat.InciteCost(c);

			_out.WriteLine($"without a wonder: {without}   with the Pyramids: {with}");
			Assert.True(with > without * 2, $"a wonder must dominate the price ({with} vs {without})");
		}

		// Loyalty: the same city is harder to turn when its owner is a going concern rather than
		// a rump state one defeat from disappearing.
		[Fact]
		public void AStrongEmpireHoldsItsCitiesBetter()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 6, capital: true);
			City target = Develop(ACity(ai, 30, 5), new Temple());
			int asARumpState = Diplomat.InciteCost(target);

			for (int i = 0; i < 20; i++) ACity(ai, 40 + i, 4, y: 30);
			int asAnEmpire = Diplomat.InciteCost(target);

			_out.WriteLine($"2-city state: {asARumpState}   22-city empire: {asAnEmpire}");
			Assert.True(asAnEmpire > asARumpState, "a large empire's cities must cost more");
		}

		// Unrest stays the player's lever, and it is strong enough to invert the ordering: a
		// bigger, richer city in disorder goes for less than a smaller calm one.
		[Fact]
		public void DisorderStillHalvesThePrice()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 6, capital: true);

			City calm = ACity(ai, 30, 6);
			City rioting = ACity(ai, 31, 9);
			Assert.False(calm.IsInDisorder, "the size-6 city should be calm");
			Assert.True(rioting.IsInDisorder, "a bare size-9 city should be in disorder");

			int calmCost = Diplomat.InciteCost(calm);
			int riotingCost = Diplomat.InciteCost(rioting);

			_out.WriteLine($"size 6 calm: {calmCost}   size 9 in disorder: {riotingCost}");
			Assert.True(riotingCost < calmCost,
				$"unrest must be worth engineering ({riotingCost} vs {calmCost})");
		}

		// The headline, in the shape of the 580 AD game: a real city of a real empire is no
		// longer pocket change.
		[Fact]
		public void ADevelopedCityIsNoLongerFiftyTwoGold()
		{
			(Game g, Player ai) = AWorld();
			ACity(ai, 20, 6, capital: true);
			for (int i = 0; i < 14; i++) ACity(ai, 40 + i, 4, y: 30);

			City target = Develop(ACity(ai, 34, 6),
				new Temple(), new Library(), new Aqueduct(), new MarketPlace(), new CityWalls());

			int cost = Diplomat.InciteCost(target);
			_out.WriteLine($"size 6, 5 improvements, 14 tiles out, 16-city empire, empty treasury: {cost}");
			Assert.True(cost > 52 * 20, $"a developed city must not go for pocket change (got {cost})");
		}
	}
}
