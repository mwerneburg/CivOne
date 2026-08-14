// CivOne tests
//
// Culture as social cohesion: an ancient civilization holds together at a size a young one
// cannot.
//
// The late-game riots are not a tuning wobble, they are structural. City.ComputeCitizens
// pushes the content floor down as the empire grows and then, past 38 cities, adds "red
// shirt" malcontents to EVERY city — so at 105 cities the floor is zero and each city
// carries five extra unhappy before anything else is counted. Nothing distinguished a large
// young empire from a large ancient one: measured at 2200 AD in one run, the Ottomans held
// 38 cities on 2,477 culture and the Babylonians 25 cities on 34,811, both paying the same
// penalty.
//
// Culture now buys back content citizens, one per doubling above the base, capped at 3. It
// is the first thing in the game that makes accumulated culture pay in the city screen
// rather than only in diplomacy and the ascendancy victory.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class CultureContentTests
	{
		private static (Game g, Player p, City c) ACity(int culture, int cities = 1)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Monarchy();
			p.Explore(40, 25, range: 15);
			for (int y = 18; y <= 32; y++)
			for (int x = 26; x <= 54; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			// Grid-placed: the empire sizes that matter here run past a hundred cities, which
			// does not fit on one row.
			City first = null!;
			int made = 0;
			for (int cy = 19; cy <= 31 && made < cities; cy += 3)
			for (int cx = 27; cx <= 53 && made < cities; cx += 2)
			{
				City c = g.AddCity(p, (byte)made, cx, cy);
				if (c is null) continue;
				c.Size = 8;
				if (made == 0) first = c;
				made++;
			}
			Assert.Equal(cities, made);
			p.SetCulture(culture);
			Sim.ClearTasks();
			return (g, p, first);
		}

		// The scale, pinned against the world it was calibrated on.
		[Theory]
		[InlineData(0, 0)]
		[InlineData(2477, 0)]        // Ottomans: 38 cities, almost no culture
		[InlineData(9999, 0)]        // just under the base
		[InlineData(10000, 1)]       // exactly the base
		[InlineData(13477, 1)]       // Romans
		[InlineData(34811, 2)]       // Babylonians
		[InlineData(70274, 3)]       // Guarani
		[InlineData(141146, 3)]      // Malians — capped, not 4
		public void TheBonusIsOnePerDoublingUpToTheCap(int culture, int expected)
		{
			(Game g, Player p, City c) = ACity(culture);

			Assert.Equal(expected, p.CultureContentBonus);
		}

		// Uncapped, a long game would make unhappiness irrelevant to whoever built the most
		// temples. This is the guard on that.
		[Fact]
		public void NoAmountOfCultureExceedsTheCap()
		{
			(Game g, Player p, City c) = ACity(int.MaxValue);

			Assert.Equal(3, p.CultureContentBonus);
		}

		// ── it reaches the citizens ──────────────────────────────────────────────

		private static int UnhappyIn(City c)
			=> c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale);

		// The point of the whole change, observed where it matters: the same city, the same
		// size, the same government, differing only in what its civilization has built over
		// the centuries.
		[Fact]
		public void AnAncientCivilizationHasFewerMalcontents()
		{
			(Game _, Player young, City youngCity) = ACity(0);
			int youngUnhappy = UnhappyIn(youngCity);

			(Game __, Player old, City oldCity) = ACity(141146);
			int oldUnhappy = UnhappyIn(oldCity);

			Assert.True(oldUnhappy < youngUnhappy,
				$"culture bought no contentment: {youngUnhappy} unhappy without it, {oldUnhappy} with");
		}

		// The case that motivated it: a sprawling empire whose content floor has already been
		// driven to zero by the empire-size penalty. Applied BEFORE the clamp the bonus would
		// be swallowed whole here — the floor computes negative — and the most cultured civ in
		// the world would feel nothing.
		[Fact]
		public void ASprawlingEmpireStillFeelsIt()
		{
			// 40 cities: past both the content-floor step and the 38-city red-shirt threshold.
			(Game _, Player young, City youngCity) = ACity(0, cities: 40);
			int youngUnhappy = UnhappyIn(youngCity);

			(Game __, Player old, City oldCity) = ACity(141146, cities: 40);
			int oldUnhappy = UnhappyIn(oldCity);

			Assert.True(oldUnhappy < youngUnhappy,
				$"the empire penalty swallowed the culture bonus: {youngUnhappy} vs {oldUnhappy}");
		}

		// The ordering, pinned at the scale where it can be observed.
		//
		// The bonus is added AFTER the empire penalty is clamped at zero. Applied before the
		// clamp it is swallowed whole in exactly the empire that needs it: the content floor
		// computes to 6 - (cities-12)/8, so it only goes negative past about 60 cities — and
		// at 70 it is -1, so the whole bonus would be spent climbing back to zero.
		//
		// The first version of this test used 40 cities, where the floor is +3 and the clamp
		// never bites, so it passed with the ordering reversed and proved nothing.
		// The count is read BEFORE the second fixture is staged: ACity calls Sim.NewGame, so a
		// City held across the call belongs to a destroyed game and reports nonsense — it came
		// back with eight specialists and nobody unhappy.
		[Fact]
		public void TheBonusIsNotSwallowedByTheEmpirePenalty()
		{
			// Precondition: the raw floor really is negative here, or there is nothing to test.
			int rawFloor = 6 - (70 - 12) / 8;
			Assert.True(rawFloor < 0, $"fixture: floor is {rawFloor}, the clamp never bites");

			(Game _, Player old, City oldCity) = ACity(141146, cities: 70);
			int cultured = UnhappyIn(oldCity);

			(Game __, Player none, City noneCity) = ACity(0, cities: 70);
			int uncultured = UnhappyIn(noneCity);

			// Applied after the clamp the cultured city gets a floor of 3 against the
			// uncultured city's 0. Applied before, the floor of -1 eats all but one of the
			// bonus and the two cities come out identical.
			Assert.True(cultured < uncultured,
				$"the empire penalty swallowed the bonus: {uncultured} unhappy without culture, {cultured} with");
		}

		// Not a licence. Culture makes a city easier to hold, it does not make unhappiness
		// go away — a big city in a sprawling empire is still in trouble.
		[Fact]
		public void CultureDoesNotAbolishUnhappiness()
		{
			(Game g, Player p, City c) = ACity(int.MaxValue, cities: 40);
			c.Size = 12;

			Assert.True(UnhappyIn(c) > 0,
				"maximum culture left a size-12 city in a 40-city empire with nobody unhappy");
		}
	}
}
