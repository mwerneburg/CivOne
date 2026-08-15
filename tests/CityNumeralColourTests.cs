// CivOne tests
//
// The city-size numeral on the map tile.
//
// Red when the city is rioting was already there — written out twice, identically, once for
// the ordinary city icon and once for the domed one. That duplication is why the celebration
// colour existed in neither: there was no single place to add it.
//
//   ALERT     rioting
//   INK_HIGH  We Love the King Day — palette index 8, #f4e6c8, bright cream
//   PHOS      the ordinary amber
//
// Disorder wins over celebration, which is not a preference but a fact about the game: a city
// that tips into disorder has stopped celebrating, and CityView reads
// `WasWeLoveKing && !IsInDisorder` for the same reason.

using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Tests
{
	public class CityNumeralColourTests
	{
		private static City ACity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			p.Explore(40, 25, range: 6);
			for (int y = 23; y <= 27; y++)
			for (int x = 38; x <= 42; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			Sim.ClearTasks();
			return c;
		}

		[Fact]
		public void AnOrdinaryCityIsAmber()
		{
			City c = ACity();
			Assert.False(c.IsInDisorder, "fixture: this city should be calm");
			Assert.False(c.WasWeLoveKing, "fixture: and not celebrating");

			Assert.Equal(CassetteTheme.PHOS, Icons.CityNumeralColour(c));
		}

		[Fact]
		public void ACelebratingCityIsCream()
		{
			City c = ACity();
			c.WasWeLoveKing = true;

			Assert.Equal(CassetteTheme.INK_HIGH, Icons.CityNumeralColour(c));
		}

		// IsInDisorder is DERIVED — `UnhappyCitizens > HappyCitizens` — so the flag cannot be
		// set directly; the city has to be genuinely miserable. A size-20 town with no Temple
		// is well past the content floor.
		[Fact]
		public void ARiotingCityIsRed()
		{
			City c = ACity();
			c.Size = 20;
			c.InvalidateCache();
			Assert.True(c.IsInDisorder, "fixture: the city should be rioting");

			Assert.Equal(CassetteTheme.ALERT, Icons.CityNumeralColour(c));
		}

		// The precedence, which is the only part that needed a decision: a city cannot both
		// riot and celebrate, and if the flags disagree the riot is the truth worth showing.
		[Fact]
		public void DisorderBeatsCelebration()
		{
			City c = ACity();
			c.Size = 20;
			c.InvalidateCache();
			c.WasWeLoveKing = true;
			Assert.True(c.IsInDisorder, "fixture: the city should be rioting");

			Assert.Equal(CassetteTheme.ALERT, Icons.CityNumeralColour(c));
		}

		// The three states must be visually distinct, or the colour carries no information.
		[Fact]
		public void TheThreeStatesAreThreeColours()
		{
			City c = ACity();
			byte calm = Icons.CityNumeralColour(c);
			c.WasWeLoveKing = true;
			byte celebrating = Icons.CityNumeralColour(c);
			c.WasWeLoveKing = false;
			c.Size = 20;
			c.InvalidateCache();
			byte rioting = Icons.CityNumeralColour(c);

			Assert.Equal(3, new[] { calm, celebrating, rioting }.Distinct().Count());
		}
	}
}
