// CivOne tests
//
// The same citizen governor as the AI's, offered to the player per city and off by default.
//
// Off by default is the whole safety property. Human cities get deliberate hands-off treatment
// throughout City.cs — _resourceTiles is never refilled for a human, because a gap may be a
// musician the player placed on purpose — and a governor that ran unasked would overwrite
// exactly those choices in cities nobody enrolled.
//
// The two halves are separate switches because they carry different risk. "This city is capped
// at 7, stop farming for nothing" is a fact about the rules a player can verify at a glance.
// Quelling disorder silently changes what a city PRODUCES, which is a strategy decision — a
// player building a wonder on a timer will not thank a governor that halves the shields to
// pacify one malcontent.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class PlayerGovernorTests
	{
		// A HUMAN city, unlike CitizenGovernorTests which builds an AI one. Monarchy and
		// irrigation for the same reason as there: under Despotism the tile penalty caps every
		// tile at one citizen's rations and no governor can do anything at all.
		private static (Game game, Player human, City city) AHumanCity(int size, bool temple = false,
		                                                               bool aqueduct = false)
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
			Player human = g.HumanPlayer;
			human.Government = new Monarchy();
			human.Explore(40, 25, range: 20);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.Size = (byte)size;
			if (temple) c.AddBuilding(new Temple());
			if (aqueduct) c.AddBuilding(new Aqueduct());
			c.ResetResourceTiles();
			Sim.ClearTasks();
			return (g, human, c);
		}

		private static int Specialists(City c) => c.Citizens.Count(z =>
			z == Citizen.Entertainer || z == Citizen.Taxman || z == Citizen.Scientist);

		// The safety property, stated directly: an un-enrolled city is not touched, even when
		// it is rioting and the governor would obviously "help".
		[Fact]
		public void AnUnenrolledCityIsLeftAlone()
		{
			var (_, human, c) = AHumanCity(9, aqueduct: true);
			Assert.True(c.IsInDisorder, "scenario: it is rioting and still must not be touched");
			var before = c.ResourceTiles.Select(t => (t.X, t.Y)).OrderBy(t => t).ToArray();

			human.NewTurn();

			Assert.Equal(before, c.ResourceTiles.Select(t => (t.X, t.Y)).OrderBy(t => t).ToArray());
			Assert.True(c.IsInDisorder);
		}

		// ...including a city the player has deliberately staffed with musicians. This is the
		// allocation City.cs protects everywhere else; the governor must not be the exception.
		[Fact]
		public void AHandPlacedMusicianSurvivesTheTurn()
		{
			var (_, human, c) = AHumanCity(6);
			c.SetResourceTile(c.ResourceTiles.First(t => t.X != c.X || t.Y != c.Y));
			int placed = Specialists(c);
			Assert.True(placed > 0, "scenario: the player parked a citizen by hand");

			human.NewTurn();

			Assert.Equal(placed, Specialists(c));
		}

		// Enrolled in GROWTH only: the capped city stops farming a surplus it cannot use.
		[Fact]
		public void GrowthEnrolmentParksTheWastedFarmers()
		{
			var (_, human, c) = AHumanCity(7, temple: true);
			c.GovernorGrowth = true;
			int before = c.FoodIncome;
			Assert.True(before > 0, "scenario: a surplus that the size-7 cap throws away");

			human.NewTurn();

			Assert.True(c.FoodIncome < before, $"still farming for nothing: {c.FoodIncome}");
		}

		// Enrolled in GROWTH only, and rioting: it must NOT quell the disorder. That is the
		// other switch, and conflating them is the thing a player would not forgive.
		[Fact]
		public void GrowthEnrolmentDoesNotTouchDisorder()
		{
			var (_, human, c) = AHumanCity(9, aqueduct: true);
			c.GovernorGrowth = true;
			Assert.False(c.GrowthBlocked, "scenario: nothing for the growth governor to do");
			Assert.True(c.IsInDisorder);

			human.NewTurn();

			Assert.True(c.IsInDisorder, "the growth switch quelled a riot it was not asked to");
		}

		// The governor must never leave a city WORSE than it found it. A capped, rioting city
		// enrolled in GROWTH only parks its surplus farmers as specialists, and those arrive as
		// entertainers whose luxury happens to end the riot. Retyping them all to taxmen — which
		// is what step 4 wants to do — drops that luxury and restarts it, so a player who asked
		// for the harmless switch would find the governor had started a riot.
		//
		// It is not that GROWTH should quell disorder; it is that it must not CAUSE it.
		[Fact]
		public void TheGrowthSwitchWillNotStartARiotOfItsOwn()
		{
			var (_, human, c) = AHumanCity(7);            // capped at 7, no temple, rioting
			c.GovernorGrowth = true;
			Assert.True(c.IsInDisorder && c.GrowthBlocked, "scenario: capped and rioting");

			human.NewTurn();

			Assert.False(c.IsInDisorder,
				"it spent the entertainer its own parking created and restarted the riot");
			Assert.Contains(c.Citizens, z => z == Citizen.Entertainer);
		}

		// Enrolled in ORDER: it does.
		[Fact]
		public void OrderEnrolmentQuellsTheRiot()
		{
			var (_, human, c) = AHumanCity(9, aqueduct: true);
			c.GovernorOrder = true;
			Assert.True(c.IsInDisorder);

			human.NewTurn();

			Assert.False(c.IsInDisorder);
		}

		// The enrolment is part of the city, so it survives a save. A governor that quietly
		// switched itself off on reload would be worse than not having one.
		[Fact]
		public void EnrolmentSurvivesASaveAndReload()
		{
			var (g, _, c) = AHumanCity(7);
			c.GovernorOrder = true;
			c.GovernorGrowth = true;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "governor.cos");

			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "fixture should reload");

			City loaded = Game.Instance.GetCities().First(x => x.X == 40 && x.Y == 25);
			Assert.True(loaded.GovernorOrder);
			Assert.True(loaded.GovernorGrowth);
		}

		// ...and an old save, which has no such field, loads with both off rather than
		// throwing or defaulting to on.
		[Fact]
		public void ASaveWithoutGovernorsLoadsThemOff()
		{
			var (g, _, c) = AHumanCity(7);
			Assert.False(c.GovernorOrder);
			Assert.False(c.GovernorGrowth);
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "nogov.cos");

			g.SaveCos(path);
			Assert.DoesNotContain("Governors", System.IO.File.ReadAllText(path));

			Sim.ResetState();
			Assert.True(Game.LoadCos(path));
			City loaded = Game.Instance.GetCities().First(x => x.X == 40 && x.Y == 25);
			Assert.False(loaded.GovernorOrder);
			Assert.False(loaded.GovernorGrowth);
		}
	}
}
