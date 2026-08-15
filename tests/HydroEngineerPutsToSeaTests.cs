// CivOne tests
//
// The Hydro Engineer can leave port.
//
// BestFloatingSite scored candidate tiles `nearestCity - dist` and accepted only a POSITIVE
// score — a tile further from every city than from the engineer. While the engineer stands in
// a city that is arithmetically impossible: nearestCity(tile) <= distance(tile, thisCity) =
// dist for every candidate, so the best attainable score is zero. It returned null, Goto
// stayed empty, the engineer skipped its turn, and repeated that for the rest of the game.
//
// Measured at turn 750 of a live game: 69 Hydro Engineers alive across eleven civs, EVERY ONE
// standing in a city, zero transport tubes anywhere on the map, and not a single
// `found-floating`, `sea-tube` or `sea-aquafarm` action in the whole decision log. Floating
// cities, sea aquafarms and the tube network had never run once.
//
// Why the Olvir were unaffected: their engineers have a separate branch that lays a tube
// wherever they happen to stand on bare ocean and shuttles between sister cities, so their
// movement never depended on this function.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class HydroEngineerPutsToSeaTests
	{
		// A coastal city with open ocean to the east, which is where an engineer should go.
		private static (Game g, Player p, City port, IUnit hydro) APortAndAnOcean()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 20);
			for (int y = 12; y <= 38; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 23; y <= 27; y++)
			for (int x = 36; x <= 40; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City port = g.AddCity(p, 0, 40, 25)!;   // on the shore, ocean to the east
			port.Size = 6;
			IUnit hydro = g.CreateUnit(UnitType.HydroEngineer, port.X, port.Y, g.PlayerNumber(p))!;
			hydro.MovesLeft = hydro.Move;
			Sim.ClearTasks();
			return (g, p, port, hydro);
		}

		private static ITile? FloatingSite(Player p, IUnit unit)
			=> (ITile?)typeof(AI).GetMethod("BestFloatingSite",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { unit });

		// The bug, stated as the thing it prevented.
		[Fact]
		public void AnEngineerSittingInPortIsGivenSomewhereToGo()
		{
			(Game g, Player p, City port, IUnit hydro) = APortAndAnOcean();
			Assert.Equal((port.X, port.Y), (hydro.X, hydro.Y));

			ITile? site = FloatingSite(p, hydro);

			Assert.NotNull(site);
			Assert.True(site!.IsOcean, "a floating site must be water");
		}

		// ...and the AI actually sends it, which is the half that matters in play.
		[Fact]
		public void TheAiSendsItOutOfPort()
		{
			(Game g, Player p, City port, IUnit hydro) = APortAndAnOcean();

			typeof(AI).GetMethod("Move",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { hydro });

			bool leftPort = hydro.X != port.X || hydro.Y != port.Y;
			Assert.True(leftPort || !hydro.Goto.IsEmpty,
				"the engineer neither moved nor set a destination — it is still stuck in port");
		}

		// The scoring still means something: given a choice, it prefers water further from
		// civilisation. Without this the fix could have been "wander anywhere".
		[Fact]
		public void ItStillPrefersOpenWaterToTheHarbourMouth()
		{
			(Game g, Player p, City port, IUnit hydro) = APortAndAnOcean();

			ITile site = FloatingSite(p, hydro)!;

			int chosen = Common.DistanceToTile(port.X, port.Y, site.X, site.Y);
			Assert.True(chosen > 1,
				$"it picked the tile just outside the harbour ({site.X},{site.Y}), distance {chosen}");
		}

		// No water in range is still no answer — the fix must not invent one.
		[Fact]
		public void AnInlandEngineerGetsNothing()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 20);
			for (int y = 10; y <= 40; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City inland = g.AddCity(p, 0, 40, 25)!;
			inland.Size = 4;
			IUnit hydro = g.CreateUnit(UnitType.HydroEngineer, inland.X, inland.Y, g.PlayerNumber(p))!;
			Sim.ClearTasks();

			Assert.Null(FloatingSite(p, hydro));
		}
	}
}
