// CivOne tests
//
// A sea tube is a tunnel, not a causeway: you board it at a city.
//
// Land units could previously step onto any sea tube from any adjacent tile, so a tube line
// was a free bridge the moment one tile of it touched a coast. Boarding at cities makes the
// line something that has to be planned to its terminations — which is the whole reason to
// speed-run a network to a port rather than sprawl it.
//
// What is deliberately NOT restricted is leaving. A unit at sea can always step ashore, so
// nothing is stranded mid-ocean and units standing on tube lines in older saves travel and
// disembark exactly as they did. HydroEngineer is a BaseUnitSea and never consults this rule
// at all, so the Olvir tube-layers are untouched.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SeaTubeBoardingTests
	{
		// A west-east strip at y=25: x=38 land, x=39 land, x=40..42 ocean.
		private static (Game game, Player human) AShoreline()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			for (int x = 36; x <= 44; x++)
				Map.Instance.ChangeTileType(x, 25, x <= 39 ? Terrain.Grassland1 : Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			Game g = Game.Instance;
			g.HumanPlayer.Explore(40, 25, range: 8);
			Sim.ClearTasks();
			return (g, g.HumanPlayer);
		}

		private static bool CanStep(IUnit u, int dx, int dy)
			=> u.MoveTo(dx, dy);

		// The rule: no boarding from open country.
		[Fact]
		public void ALandUnitCannotStepOntoASeaTubeFromOpenGround()
		{
			(Game g, Player human) = AShoreline();
			Map.Instance[40, 25].TransportTube = true;
			IUnit u = g.CreateUnit(UnitType.Caravan, 39, 25, g.PlayerNumber(human))!;

			Assert.False(CanStep(u, 1, 0), "it boarded the tube from a beach");
		}

		// ...but a city is a terminal.
		[Fact]
		public void ALandUnitBoardsFromACity()
		{
			(Game g, Player human) = AShoreline();
			Map.Instance[40, 25].TransportTube = true;
			g.AddCity(human, 0, 39, 25);
			IUnit u = g.CreateUnit(UnitType.Caravan, 39, 25, g.PlayerNumber(human))!;

			Assert.True(CanStep(u, 1, 0), "a coastal city is the port and it refused to board");
		}

		// Once in the tunnel, travel along it is free.
		[Fact]
		public void TravelAlongTheLineIsUnrestricted()
		{
			(Game g, Player human) = AShoreline();
			Map.Instance[40, 25].TransportTube = true;
			Map.Instance[41, 25].TransportTube = true;
			IUnit u = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(human))!;

			Assert.True(CanStep(u, 1, 0), "a unit already in the tube could not continue");
		}

		// Leaving is always allowed: nothing is ever stranded at sea, and old saves keep
		// working. This is the half that fails if the rule is made symmetric.
		[Fact]
		public void SteppingAshoreFromTheLineIsAlwaysAllowed()
		{
			(Game g, Player human) = AShoreline();
			Map.Instance[40, 25].TransportTube = true;
			IUnit u = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(human))!;

			Assert.True(CanStep(u, -1, 0), "a unit at sea was refused the shore");
		}

		// A land tube reaching the coast is not a way in — the junction needs a city. This is
		// what makes the rule "at cities" rather than "anywhere the network touches water".
		[Fact]
		public void ALandTubeIsNotABoardingPoint()
		{
			(Game g, Player human) = AShoreline();
			Map.Instance[39, 25].TransportTube = true;   // land tube on the shore
			Map.Instance[40, 25].TransportTube = true;   // sea tube beside it
			IUnit u = g.CreateUnit(UnitType.Caravan, 39, 25, g.PlayerNumber(human))!;

			Assert.False(CanStep(u, 1, 0), "a land tube let it into the undersea section");
		}
	}
}
