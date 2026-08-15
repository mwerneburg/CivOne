// CivOne tests
//
// A garrison on our own resource camp is not "abroad".
//
// City.InShelter decides who costs a Republic or Democracy an unhappy citizen: units in one of
// OUR cities or on a fortress are free, everything else in the field is billed. Resource camps
// were not considered at all.
//
// That taxed the one behaviour the camp mechanic requires. ProcessResourceCamps hands a camp
// to "any unit standing on a rival's camp at turn's end — flags on mines, not ashes", so a
// garrison is the only defence a camp has. A democracy that guarded its iron paid for it at
// home.
//
// Deliberately OUR camp only, matching the city test directly above it: standing on a rival's
// camp is the field, exactly as standing in a rival's streets is.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CampShelterTests
	{
		private static (Game g, Player p, City c) ARepublicCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Republic();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 8;
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static int Unhappy(City c)
		{
			c.InvalidateCache();
			return c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale);
		}

		// Three musketeers well away from home, and where they stand is the only variable.
		private static int UnhappyWithGarrisonAt(int x, int y, System.Action<Game, Player> prepare)
		{
			(Game g, Player p, City c) = ARepublicCity();
			prepare(g, p);
			int before = Unhappy(c);
			for (int i = 0; i < 3; i++)
			{
				IUnit u = g.CreateUnit(UnitType.Musketeers, x, y, g.PlayerNumber(p))!;
				u.SetHome(c);
			}
			return Unhappy(c) - before;
		}

		// The baseline: open ground costs a republic.
		[Fact]
		public void UnitsInTheOpenCostTheRepublic()
		{
			int cost = UnhappyWithGarrisonAt(45, 29, (g, p) => { });

			Assert.True(cost > 0, "fixture: three musketeers abroad should cost a republic something");
		}

		// The change.
		[Fact]
		public void OurOwnCampShelters()
		{
			int cost = UnhappyWithGarrisonAt(45, 29,
				(g, p) => g.ResourceCamps[(45, 29)] = g.PlayerNumber(p));

			Assert.Equal(0, cost);
		}

		// A rival's camp is the field, exactly as a rival's city is.
		[Fact]
		public void ARivalsCampDoesNot()
		{
			int cost = UnhappyWithGarrisonAt(45, 29, (g, p) =>
			{
				Player rival = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
				                                 && g.PlayerNumber(x) != 0);
				g.ResourceCamps[(45, 29)] = g.PlayerNumber(rival);
			});

			Assert.True(cost > 0, "standing on somebody else's camp is being abroad");
		}

		// The rule this one was modelled on still holds.
		[Fact]
		public void AFortressStillShelters()
		{
			int cost = UnhappyWithGarrisonAt(45, 29,
				(g, p) => Map.Instance[45, 29].Fortress = true);

			Assert.Equal(0, cost);
		}
	}
}
