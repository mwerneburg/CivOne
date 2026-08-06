// CivOne tests
//
// Player.Cities was recomputed from scratch on every access: Game.GetCities() allocating a
// fresh array of every city in the world (543 late-game), a Where().ToArray() on top, and an
// owner test routed through the Player==byte operator, which does a player-table lookup per
// city. It is read from 109 sites, 70 of them in the AI and several per unit move.
//
// A 750-turn run with 543 cities and 2,045 units spent 74% of each 25-second turn in
// "ai_move" while pathfinding accounted for 4% and site scans 0.0% — the cost was not in
// deciding where to go, it was in asking who owns what, over and over.
//
// It is now cached against Game.CityRosterVersion. That makes staleness the new failure mode,
// and a stale roster is silent: the AI simply reasons about a world that no longer exists.
// These pin every way the roster can move.

using System.Linq;
using CivOne;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CityRosterCacheTests
	{
		private static (Game, Player, Player) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player a = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			Player b = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
			                             && p != g.HumanPlayer && p != a);
			return (g, a, b);
		}

		private static City ACity(Player owner, int x, int y = 25)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(x, y, range: 3);
			City c = g.AddCity(owner, x + y, x, y)!;
			c.Size = 4;
			return c;
		}

		// Founding.
		[Fact]
		public void FoundingACityShowsUpImmediately()
		{
			(Game g, Player a, Player b) = AWorld();
			int before = a.Cities.Length;      // populates the cache

			ACity(a, 40);

			Assert.Equal(before + 1, a.Cities.Length);
		}

		// Changing hands — the case that matters most, since both sides must move at once.
		[Fact]
		public void CaptureMovesTheCityBetweenBothRosters()
		{
			(Game g, Player a, Player b) = AWorld();
			City c = ACity(a, 40);
			int aBefore = a.Cities.Length, bBefore = b.Cities.Length;
			Assert.Contains(c, a.Cities);      // populate both caches

			c.Owner = g.PlayerNumber(b);

			Assert.Equal(aBefore - 1, a.Cities.Length);
			Assert.Equal(bBefore + 1, b.Cities.Length);
			Assert.DoesNotContain(c, a.Cities);
			Assert.Contains(c, b.Cities);
		}

		// Destruction.
		[Fact]
		public void DestroyingACityLeavesTheRoster()
		{
			(Game g, Player a, Player b) = AWorld();
			City c = ACity(a, 40);
			Assert.Contains(c, a.Cities);

			g.DestroyCity(c);

			Assert.DoesNotContain(c, a.Cities);
		}

		// Player.Cities filters on Size > 0, so a crossing of zero moves the roster even when
		// no city is added, destroyed or handed over.
		[Fact]
		public void ACityShrinkingToNothingLeavesTheRoster()
		{
			(Game g, Player a, Player b) = AWorld();
			City c = ACity(a, 40);
			int before = a.Cities.Length;

			c.Size = 0;

			Assert.Equal(before - 1, a.Cities.Length);
		}

		// The cache must not leak between players — each holds its own.
		[Fact]
		public void EachPlayerSeesOnlyItsOwn()
		{
			(Game g, Player a, Player b) = AWorld();
			City ca = ACity(a, 40), cb = ACity(b, 44);

			Assert.Contains(ca, a.Cities);
			Assert.DoesNotContain(cb, a.Cities);
			Assert.Contains(cb, b.Cities);
			Assert.DoesNotContain(ca, b.Cities);
		}

		// ...and it really is a cache: repeated reads with no world change hand back the same
		// array rather than rebuilding it. This is the whole point of the exercise.
		[Fact]
		public void RepeatedReadsReuseTheSameArray()
		{
			(Game g, Player a, Player b) = AWorld();
			ACity(a, 40);

			City[] first = a.Cities;
			City[] second = a.Cities;
			Assert.Same(first, second);

			ACity(a, 44);
			Assert.NotSame(first, a.Cities);
		}
	}
}
