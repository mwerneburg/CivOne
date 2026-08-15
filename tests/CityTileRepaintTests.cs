// CivOne tests
//
// Repainting the map tile when a city changes how it looks.
//
// The city-size numeral is drawn in three colours — red rioting, cream celebrating, amber
// otherwise — but it is baked into the city's 16x16 icon when the TILE is rendered. Nothing
// repainted the tile when a city changed state, so the numeral kept whatever it had until
// something else forced a redraw. Quelling a riot from the city screen left the number red
// until the player happened to move a unit, which is exactly how this was noticed; a
// celebration would not have shown at all until then.
//
// The check compares against the last appearance rather than signalling blindly, because
// RefreshMap sets a FULL-recompose flag and this is reachable from the citizen governor and
// every screen that edits a city. Firing on every call would recompose the viewport
// constantly — the waste this bug was mistaken for.

using System.Linq;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CityTileRepaintTests
	{
		private static City ACity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			p.Explore(40, 25, range: 8);
			for (int y = 23; y <= 27; y++)
			for (int x = 38; x <= 42; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			Sim.ClearTasks();
			return c;
		}

		// AppearanceChanged IS the decision — true means a repaint is asked for. Watching the
		// remembered fields instead could not tell that apart from "decided not to", and an
		// earlier version of this file did exactly that and passed with the guard deleted.
		private static bool Signalled(City c) => c.AppearanceChanged();

		// The first look always counts: nothing has been recorded yet.
		[Fact]
		public void TheFirstCheckNotesTheAppearance()
		{
			City c = ACity();

			Assert.True(Signalled(c));
		}

		// ...and a second look at an unchanged city asks for nothing. This is the guard that
		// keeps the map from recomposing on every citizen reshuffle.
		[Fact]
		public void AnUnchangedCityAsksForNothing()
		{
			City c = ACity();
			Signalled(c);

			Assert.False(Signalled(c));
			Assert.False(Signalled(c));
		}

		// The case that started it: a riot begins, and the tile must be repainted.
		[Fact]
		public void FallingIntoDisorderIsNoticed()
		{
			City c = ACity();
			Signalled(c);
			Assert.False(c.IsInDisorder, "fixture: should start calm");

			c.Size = 20;              // well past the content floor, with no Temple
			c.InvalidateCache();
			Assert.True(c.IsInDisorder, "fixture: should now be rioting");

			Assert.True(Signalled(c));
		}

		// ...and quelling it is noticed too, which is the half the player actually saw fail.
		[Fact]
		public void QuellingARiotIsNoticed()
		{
			City c = ACity();
			c.Size = 20;
			c.InvalidateCache();
			Signalled(c);
			Assert.True(c.IsInDisorder, "fixture: should be rioting");

			c.Size = 6;
			c.InvalidateCache();
			Assert.False(c.IsInDisorder);

			Assert.True(Signalled(c));
		}

		[Fact]
		public void StartingToCelebrateIsNoticed()
		{
			City c = ACity();
			Signalled(c);

			c.WasWeLoveKing = true;

			Assert.True(Signalled(c));
		}

		// The city screen is where a player fixes a riot by hand, and that never goes through
		// City.NewTurn — so the screen has to ask on its way out or the whole fix misses the
		// case it was written for.
		[Fact]
		public void TheCityScreenAsksOnItsWayOut()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "CityManager.cs"));

			Assert.Contains("_city.RefreshTileIfAppearanceChanged();", src);
		}
	}
}
