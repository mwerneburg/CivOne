// CivOne tests
//
// The owner banner on a city icon.
//
// Like the unit sprite (UnitBannerTests), the city icon's banner used to be QUARTERED: two rows
// of primary|accent, then a third row repeating the pair inverted. The third row said nothing
// the two above it had not, so it went. The banner is now the unit's: two rows, primary on the
// left, accent on the right, on the bottom two rows. Both icons are drawn: the standard city
// and the Olvir dome, which has its own copy of the banner.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.IO;

namespace CivOne.Tests
{
	public class CityBannerTests
	{
		private static byte[] Row(Bytemap map, int y) =>
			Enumerable.Range(0, 16).Select(x => map[x, y]).ToArray();

		private static City ACity(bool olvir)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player owner = g.HumanPlayer;
			if (olvir)
			{
				owner = g.Players.FirstOrDefault(p => p is not null && p.Civilization is Civilizations.Olvir)!;
				if (owner is null)
				{
					owner = new Player(Common.Civilizations.First(c => c is Civilizations.Olvir));
					g.AddPlayer(owner);
				}
			}
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			return g.AddCity(owner, 0, 40, 25)!;
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TheBannerIsTwoRowsOfThePair(bool olvir)
		{
			City city = ACity(olvir);
			Bytemap icon = Icons.City(city).Bitmap;
			byte primary = Common.ColourLight[city.Owner], accent = Common.BannerSecondary[city.Owner];

			foreach (int y in new[] { 14, 15 })
			{
				byte[] row = Row(icon, y);
				Assert.All(row.Take(8), c => Assert.Equal(primary, c));
				Assert.All(row.Skip(8), c => Assert.Equal(accent, c));
			}
			// Row 13 is no longer banner.
			byte[] row13 = Row(icon, 13);
			Assert.False(row13.Take(8).All(c => c == primary) && row13.Skip(8).All(c => c == accent),
				"row 13 is still banner, so the banner is still three rows");
		}
	}
}
