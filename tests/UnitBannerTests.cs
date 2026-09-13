// CivOne tests
//
// The owner banner on a unit sprite.
//
// It used to be QUARTERED — two rows of primary|accent, then a third row repeating the pair
// inverted. Reported from a game: at sixteen pixels the mirrored row reads as noise rather
// than as information, since both halves of the pair are already on show in the rows above,
// and it costs the artwork a row it can ill afford.
//
// So the banner is two rows and the art runs one row further down. The pair still identifies
// the civ (Common.BannerSecondary is chosen so every pair is unique); it simply isn't said
// twice. The city icon has since followed suit (CityBannerTests).

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.IO;

namespace CivOne.Tests
{
	public class UnitBannerTests
	{
		private const byte Player = 3;

		private static Bytemap Sprite()
		{
			Sim.EnsureRuntime();
			return Graphics.Sprites.Unit.Base(UnitType.Musketeers, Player).Bitmap;
		}

		private static byte[] Row(Bytemap map, int y) =>
			Enumerable.Range(0, 16).Select(x => map[x, y]).ToArray();

		// The two banner rows carry the pair: primary on the left, accent on the right.
		[Fact]
		public void TheBannerShowsThePairOnce()
		{
			Bytemap sprite = Sprite();
			byte primary = Common.ColourLight[Player], accent = Common.BannerSecondary[Player];

			foreach (int y in new[] { 14, 15 })
			{
				byte[] row = Row(sprite, y);
				Assert.All(row.Take(8),  c => Assert.Equal(primary, c));
				Assert.All(row.Skip(8),  c => Assert.Equal(accent,  c));
			}
		}

		// The inverted row is gone: the last row must not be the mirror of the one above it.
		// This is the half that fails if the quartered banner comes back.
		[Fact]
		public void TheLastRowIsNotTheMirrorOfTheOneAboveIt()
		{
			Bytemap sprite = Sprite();

			Assert.Equal(Row(sprite, 14), Row(sprite, 15));
		}

		// ...and the row it used to occupy belongs to the artwork again — it is no longer a
		// band of banner colour from edge to edge.
		[Fact]
		public void TheArtworkGotTheRowBack()
		{
			Bytemap sprite = Sprite();
			byte primary = Common.ColourLight[Player], accent = Common.BannerSecondary[Player];
			byte[] row13 = Row(sprite, 13);

			Assert.False(row13.Take(8).All(c => c == primary) && row13.Skip(8).All(c => c == accent),
				"row 13 is still banner, so the sprite did not gain a row");
			// The side trim still frames it, which is what makes it part of the picture.
			Assert.Equal(CassetteTheme.PHOS, row13[0]);
			Assert.Equal(CassetteTheme.PHOS, row13[15]);
		}
	}
}
