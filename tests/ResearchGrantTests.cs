// CivOne tests
//
// The Research Grant is a "building" that must never be built: it converts a
// city's shield income into empire research every turn and stays in production
// forever. Both halves of that are easy to break silently — a completed grant
// would hand the city a phantom improvement, and a grant that banked its shields
// would quietly do nothing at all.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class ResearchGrantTests
	{
		private static City GrantCity(out Player player)
		{
			Sim.NewGame(width: 80, height: 50);
			player = Game.Instance.HumanPlayer;
			player.AddAdvance(new Writing(), false);

			// Put the city on real land: a random map may well have ocean at any fixed
			// coordinate, and a city with no workable tiles produces nothing to convert.
			ITile site = Map.Instance.AllTiles().First(t => !t.IsOcean && t.City is null
			                                    && t.Y > 5 && t.Y < Map.HEIGHT - 5);
			// CityRadius hides tiles the owner cannot see, so an unexplored site works no
			// tiles at all and yields nothing. Founding normally reveals the surroundings.
			player.Explore(site.X, site.Y, range: 3);
			City city = Game.Instance.AddCity(player, 0, site.X, site.Y);
			Assert.NotNull(city);
			city.Size = 4;
			city.ResetResourceTiles();   // pick worked tiles now; NewTurn assumes they exist
			city.SetProduction(new ResearchGrant());
			return city;
		}

		// The conversion: shield income becomes research, and no shields are banked.
		[Fact]
		public void ResearchGrant_ConvertsShieldsToScience()
		{
			City city = GrantCity(out Player player);
			player.Science = 0;
			city.Shields = 0;
			int income = city.ShieldIncome;

			city.NewTurn();

			Assert.True(income > 0, "test city should produce shields to convert");
			Assert.Equal(0, city.Shields);
			Assert.True(player.Science >= income,
				$"expected at least {income} science from the grant, got {player.Science}");
		}

		// The standing commitment: it is never completed, even when shields arrive from
		// outside the city (the Adam Smith bond pool can donate them).
		[Fact]
		public void ResearchGrant_NeverCompletes()
		{
			City city = GrantCity(out _);
			city.Shields = 9999;          // far past any conceivable production cost

			city.NewTurn();

			Assert.False(city.HasBuilding<ResearchGrant>(), "the grant must never be built");
			Assert.True(city.CurrentProduction is ResearchGrant, "it should stay in production");
		}

		// Availability: it is offered to anyone with Writing, and stays offered — it is
		// never in the city's building list, so it can never filter itself out.
		[Fact]
		public void ResearchGrant_StaysAvailable()
		{
			City city = GrantCity(out _);
			city.NewTurn();
			Assert.Contains(city.AvailableProduction, p => p is ResearchGrant);
		}
	}
}
