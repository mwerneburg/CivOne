// CivOne tests
//
// Republic and Democracy charge unhappiness for military units away from home — 1 and 2 per
// unit. The test the code applied was "not standing on THIS city's tile", so a Musketeer
// fortified in the city next door billed its home city as though it were campaigning abroad.
//
// AI civs shuffle defenders between their own cities constantly, so this taxed cities for
// moves nothing on the map explained, and the citizen governor then spent real food quelling
// the resulting riots. Measured on a size-10 Republic city with four Musketeers: 3 unhappy
// with them at home, 7 with them garrisoned six tiles away in one of our own cities.
//
// Shelter is now any city we own, or any fortress. Civ 1 shelters only cities; fortresses are
// deliberate here because this game has no borders, so a fortress is the only forward position
// a peaceful government can hold at all.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class GarrisonWearinessTests
	{
		// A well-fed Republic city big enough that a few units abroad show up clearly.
		private static (Game game, Player ai, City city) ARepublicCity()
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
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Government = new Republic();
			ai.Explore(40, 25, range: 20);
			City c = g.AddCity(ai, 0, 40, 25)!;
			c.Size = 10;
			c.AddBuilding(new Temple());
			c.AddBuilding(new Aqueduct());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, ai, c);
		}

		private static int UnhappyWithFourUnitsAt(int ux, int uy, System.Action<Game, Player>? arrange = null)
		{
			var (g, ai, c) = ARepublicCity();
			arrange?.Invoke(g, ai);
			for (int i = 0; i < 4; i++)
			{
				IUnit u = g.CreateUnit(UnitType.Musketeers, ux, uy, g.PlayerNumber(ai))!;
				u.SetHome(c);
			}
			Sim.ClearTasks();
			return c.UnhappyCitizens;
		}

		// The defect, stated directly.
		[Fact]
		public void AGarrisonInOurOwnCityIsNotAnExpedition()
		{
			int atHome = UnhappyWithFourUnitsAt(40, 25);
			int nextDoor = UnhappyWithFourUnitsAt(46, 25, (g, ai) => g.AddCity(ai, 1, 46, 25));

			Assert.Equal(atHome, nextDoor);
		}

		// Fortresses shelter too — the choice that separates this from Civ 1's city-only rule.
		[Fact]
		public void AFortressShelters()
		{
			int atHome = UnhappyWithFourUnitsAt(40, 25);
			int inFort = UnhappyWithFourUnitsAt(46, 25, (_, __) => Map.Instance[46, 25].Fortress = true);

			Assert.Equal(atHome, inFort);
		}

		// The control, and the whole point of war weariness: units in the FIELD still cost.
		// Without this the change would just be "delete the penalty".
		[Fact]
		public void UnitsInTheFieldStillCost()
		{
			int atHome = UnhappyWithFourUnitsAt(40, 25);
			int inField = UnhappyWithFourUnitsAt(46, 25);

			Assert.True(inField > atHome, $"field {inField} vs home {atHome}");
		}

		// Someone else's streets are not shelter. A stack sitting in a captured enemy capital
		// is the definition of an army abroad.
		[Fact]
		public void AnEnemyCityIsNotShelter()
		{
			int atHome = UnhappyWithFourUnitsAt(40, 25);
			int inEnemyCity = UnhappyWithFourUnitsAt(46, 25, (g, ai) =>
			{
				Player enemy = g.Players.First(p => p is not null && p != ai
					&& g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
				g.AddCity(enemy, 1, 46, 25);
			});

			Assert.True(inEnemyCity > atHome, $"enemy city {inEnemyCity} vs home {atHome}");
		}

		// Democracy pays double, and the shelter rule applies there too rather than only to
		// the government whose penalty is small enough not to matter.
		[Fact]
		public void TheShelterHoldsUnderDemocracyToo()
		{
			var (g, ai, c) = ARepublicCity();
			ai.Government = new Democracy();
			g.AddCity(ai, 1, 46, 25);
			for (int i = 0; i < 4; i++)
			{
				IUnit u = g.CreateUnit(UnitType.Musketeers, 46, 25, g.PlayerNumber(ai))!;
				u.SetHome(c);
			}
			Sim.ClearTasks();
			int sheltered = c.UnhappyCitizens;

			var (g2, ai2, c2) = ARepublicCity();
			ai2.Government = new Democracy();
			for (int i = 0; i < 4; i++)
			{
				IUnit u = g2.CreateUnit(UnitType.Musketeers, 46, 25, g2.PlayerNumber(ai2))!;
				u.SetHome(c2);
			}
			Sim.ClearTasks();

			Assert.True(sheltered < c2.UnhappyCitizens,
				$"sheltered {sheltered} vs field {c2.UnhappyCitizens}");
		}
	}
}
