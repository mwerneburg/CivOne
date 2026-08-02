// CivOne tests
//
// A 438-turn epic game finished 1888 AD with "Aztecs: 20 (0c)" still on the score
// graph — zero cities, and not one AI decision of any kind logged for them in the
// whole game. Player.IsDestroyed (Player.cs:662) keeps a player alive while it holds
// a single unsupported Settlers, and on a map with 300 cities on it no tile passes
// the ordinary `nearestCity > 3` siting bar. So the civ could neither found nor die.
//
// A cityless civ founds where it stands. Better a twelfth competitor than a zombie.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CitylessCivTests
	{
		// Grassland everywhere, one foreign city, and a lone homeless settler two tiles
		// from it — close enough that the ordinary siting bar rejects every tile in reach.
		private static (Player stranded, IUnit settler) Stranded(bool giveOwnCity)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] players = Game.Instance.Players
				.Where(p => p is not null && Game.Instance.PlayerNumber(p) != 0)
				.ToArray();
			Player neighbour = players[0];
			Player stranded = players[1];

			neighbour.Explore(42, 25, range: 8);
			Game.Instance.AddCity(neighbour, 0, 42, 25);

			if (giveOwnCity)
			{
				stranded.Explore(48, 25, range: 6);
				Game.Instance.AddCity(stranded, 1, 48, 25);
			}

			stranded.Explore(44, 25, range: 6);
			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, 44, 25,
				Game.Instance.PlayerNumber(stranded))!;
			Sim.ClearTasks();
			return (stranded, settler);
		}

		private static void PumpTasks(int steps = 40)
		{
			for (int i = 0; i < steps; i++) GameTask.Update();
		}

		// The finding: with nowhere that passes the ordinary bar, it founds anyway.
		[Fact]
		public void ACitylessCiv_FoundsWhereItStands()
		{
			var (stranded, settler) = Stranded(giveOwnCity: false);
			Assert.Empty(stranded.Cities);

			AI.Instance(stranded).Move(settler);
			PumpTasks();

			Assert.True(stranded.Cities.Length > 0,
				"a civ with no cities left should found on the spot rather than wander forever");
		}

		// And having founded, it is no longer a zombie kept alive by a stray settler.
		[Fact]
		public void HavingFounded_ItIsAliveForRealReasons()
		{
			var (stranded, settler) = Stranded(giveOwnCity: false);

			AI.Instance(stranded).Move(settler);
			PumpTasks();

			Assert.False(stranded.IsDestroyed());
			Assert.True(stranded.Cities.Length > 0);
		}

		// The control, and the thing that must not regress: a civ that already has a city
		// still respects the spacing bar and does NOT squat beside a foreign one.
		[Fact]
		public void ACivWithACity_StillRespectsTheSpacingBar()
		{
			var (stranded, settler) = Stranded(giveOwnCity: true);
			int before = stranded.Cities.Length;

			AI.Instance(stranded).Move(settler);
			PumpTasks();

			Assert.True(stranded.Cities.All(c => Game.Instance.GetCities()
					.Where(o => o != c)
					.All(o => Common.DistanceToTile(o.X, o.Y, c.X, c.Y) >= 4)),
				"a civ with cities must not found within 4 of an existing one");
			Assert.Equal(before, stranded.Cities.Length);
		}
	}
}
