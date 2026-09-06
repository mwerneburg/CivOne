// CivOne tests
//
// The dirigible is the answer to a trade route cut by somebody else's tube network.
//
// Sea tubes are claimed by whoever lays them first (Common.TubeBarred), so a rival line
// across your route is a wall a caravan has no right to cross — and a caravan that cannot
// reach a foreign city cannot trade with it. Flying over is the counterplay that does not
// require declaring war.
//
// It is UnitClass.Air but deliberately NOT a BaseUnitAir: that class disbands a unit that
// ends a turn away from a city or Carrier, which for a freighter drowns the cargo, and the
// long crossing is the entire point of the unit.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class DirigibleTests
	{
		private static (Game game, Player human, Dirigible ship) ADirigible()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.ChangeTileType(41, 25, Terrain.Ocean);
			Sim.ClearTasks();
			Player human = g.HumanPlayer;
			Dirigible d = (Dirigible)g.CreateUnit(UnitType.Dirigible, 40, 25, g.PlayerNumber(human))!;
			return (g, human, d);
		}

		[Fact]
		public void ItFliesAndCarries()
		{
			(_, _, Dirigible d) = ADirigible();

			Assert.Equal(UnitClass.Air, d.Class);
			Assert.Equal(UnitRole.Transport, d.Role);
			Assert.Equal(4, d.Cargo);
		}

		// The fuel rule is the reason it is not a BaseUnitAir. A freighter that vanishes at
		// the end of a long crossing takes its cargo with it.
		[Fact]
		public void ItCarriesNoFuelRule()
		{
			(_, _, Dirigible d) = ADirigible();

			Assert.False(d is BaseUnitAir, "a fuel rule would disband it mid-crossing");
		}

		// Every tile is a legal destination — that is what flying over a claimed tube means.
		[Fact]
		public void AClaimedSeaTubeDoesNotBarIt()
		{
			(Game g, _, Dirigible d) = ADirigible();
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ITile t = Map.Instance[41, 25];
			t.TransportTube = true;
			t.TubeOwner = g.PlayerNumber(ai);

			// The claim bars a LAND unit of ours...
			IUnit caravan = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(g.HumanPlayer))!;
			Assert.True(Common.TubeBarred(t, caravan.Owner));

			// ...and the airship goes over it.
			Assert.True(d.MoveTo(1, 0), "the dirigible was stopped by a tube it flies above");
		}

		// Unloading over open water would put a land unit where it cannot stand.
		[Fact]
		public void ItWillNotUnloadOverOpenWater()
		{
			(Game g, Player human, Dirigible d) = ADirigible();
			byte me = g.PlayerNumber(human);
			IUnit cargo = g.CreateUnit(UnitType.Caravan, 41, 25, me)!;
			cargo.Sentry = true;
			d.X = 41; d.Y = 25;

			d.Unload();

			Assert.True(cargo.Sentry, "it was tipped into the sea");
		}
	}
}
