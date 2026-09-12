// CivOne tests
//
// Giving a city back to the civilization that founded it.
//
// Reported at turn 462 of a live game: a Chinese city taken by diplomat, and no way to hand
// it back — it never appeared in the offer list. The list is ordered by distance to the
// recipient's capital and then CUT to the rows that fit, because the menu does not scroll, so
// a city below the cut does not exist as far as the player can tell.
//
// Distance answers the wrong question for a returned city. A civ that planted a colony far
// from home has that colony ranked by where its HOMELAND is, so it sorts behind every city of
// yours that happens to sit nearer their capital — sixty of them, in the game that reported
// this. Provenance is already on the city (City.OriginalOwner), and "give it back" is exactly
// what the distance heuristic was standing in for.

using System.Linq;
using CivOne;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Screens;

namespace CivOne.Tests
{
	public class CityGiftOrderTests
	{
		// Their homeland sits in the east. Our own cities are strung out just west of it, all
		// of them NEARER their capital than the distant colony is — which is the whole point.
		private static (Game game, Player me, Player them, City colony) AWorld(bool capital = true)
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 5; y <= 45; y++)
			for (int x = 5; x <= 75; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			foreach (City c in g.GetCities().ToArray()) g.DestroyCity(c);

			Player me = g.HumanPlayer;
			byte num = g.PlayerNumber(me);
			Player them = g.Players.First(p => p is not null && g.PlayerNumber(p) != num
			                                                 && g.PlayerNumber(p) != 0);
			byte theirNum = g.PlayerNumber(them);

			// AddCity gives a player's FIRST city a Palace of its own, so the no-capital case
			// is made by taking it away again rather than by withholding it.
			City home = g.AddCity(them, 0, 68, 22)!;
			home.Size = 8;
			if (!capital) home.RemoveBuilding<Palace>();
			Assert.Equal(capital, home.HasBuilding<Palace>());
			for (int i = 1; i <= 3; i++) { City c = g.AddCity(them, i, 68, 26 + 4 * i)!; c.Size = 6; }

			// Ours, close to their realm.
			for (int i = 0; i < 8; i++)
			{
				City c = g.AddCity(me, 100 + i, 52 + 2 * (i % 4), 14 + 4 * (i / 4))!;
				c.Size = 5;
			}

			// Their colony on the far side of the map, then incited away from them: the owner
			// changes, the founder does not.
			City colony = g.AddCity(them, 20, 10, 40)!;
			colony.Size = 6;
			Assert.Equal(theirNum, colony.OriginalOwner);
			colony.Owner = num;

			Assert.True(me.Cities.Length >= 8, $"fixture: only {me.Cities.Length} cities of ours");
			Assert.Contains(colony, me.Cities);
			return (g, me, them, colony);
		}

		private static int RankOf(City[] list, City city) => System.Array.IndexOf(list, city);

		// The report: the city they founded is offered first, wherever it sits.
		[Fact]
		public void ACityTheyFoundedIsOfferedFirst()
		{
			var (g, me, them, colony) = AWorld();

			City[] offer = King.GiftableCities(me, them, rows: 30);

			Assert.Equal(0, RankOf(offer, colony));
		}

		// ...and it survives the cut, which is the part the player actually experiences. Three
		// rows is the floor the menu clamps to, and the colony is last by distance.
		[Fact]
		public void ItSurvivesATruncatedMenu()
		{
			var (g, me, them, colony) = AWorld();

			City[] offer = King.GiftableCities(me, them, rows: 3);

			Assert.Equal(3, offer.Length);
			Assert.Contains(colony, offer);
		}

		// The fixture has to be one where distance alone would bury it, or the test above
		// proves nothing: every city of ours is nearer their capital than the colony is.
		[Fact]
		public void DistanceAloneWouldHaveBuriedIt()
		{
			var (g, me, them, colony) = AWorld();
			City anchor = them.Cities.First(c => c.HasBuilding<Palace>());
			int colonyRange = Common.DistanceToTile(colony.X, colony.Y, anchor.X, anchor.Y);

			Assert.All(me.Cities.Where(c => c != colony), c =>
				Assert.True(Common.DistanceToTile(c.X, c.Y, anchor.X, anchor.Y) < colonyRange,
					$"{c.Name} is further from their capital than the colony"));
		}

		// Everything that was never theirs stays in geographic order behind it.
		[Fact]
		public void TheRestOfTheListStaysGeographic()
		{
			var (g, me, them, colony) = AWorld();
			City anchor = them.Cities.First(c => c.HasBuilding<Palace>());

			City[] offer = King.GiftableCities(me, them, rows: 30);
			int[] ranges = offer.Skip(1)
				.Select(c => Common.DistanceToTile(c.X, c.Y, anchor.X, anchor.Y)).ToArray();

			Assert.Equal(ranges.OrderBy(r => r).ToArray(), ranges);
		}

		// A recipient with no capital still anchors on their oldest standing city, so the rest
		// of the list is still geographic rather than alphabetical. (The Chinese in the
		// reported game had no Palace at all.)
		[Fact]
		public void ARecipientWithNoCapitalStillSortsByDistance()
		{
			var (g, me, them, colony) = AWorld(capital: false);
			Assert.DoesNotContain(them.Cities, c => c.HasBuilding<Palace>());
			City anchor = them.Cities.First();

			City[] offer = King.GiftableCities(me, them, rows: 30);
			int[] ranges = offer.Skip(1)
				.Select(c => Common.DistanceToTile(c.X, c.Y, anchor.X, anchor.Y)).ToArray();

			Assert.Equal(0, RankOf(offer, colony));
			Assert.Equal(ranges.OrderBy(r => r).ToArray(), ranges);
		}

		// Our own capital is never on the block, promotion or not.
		[Fact]
		public void OurOwnPalaceIsNeverOffered()
		{
			var (g, me, them, colony) = AWorld();
			City seat = me.Cities.First(c => c != colony);
			seat.AddBuilding(new Palace());

			Assert.DoesNotContain(seat, King.GiftableCities(me, them, rows: 30));
		}
	}
}
