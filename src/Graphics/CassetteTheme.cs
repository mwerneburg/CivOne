// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Graphics
{
	// Cassette-futurism design tokens — palette indices 1-17 reserved for the theme.
	// Portrait/sprite graphics use indices 64+ so there is no overlap.
	internal static class CassetteTheme
	{
		public const byte BG0        =  1;  // #0a0806 deepest background
		public const byte BG1        =  2;  // #121009 panel fill
		public const byte BG2        =  3;  // #1b1810 raised surface
		public const byte BG3        =  4;  // #26221a input well
		public const byte BORDER     =  5;  // #3a3122 outlines / dividers
		public const byte INK_LOW    =  6;  // #6a5a3c disabled
		public const byte INK_MID    =  7;  // #b39c72 labels
		public const byte INK_HIGH   =  8;  // #f4e6c8 main text
		public const byte PHOS_GHOST =  9;  // #201508 very faint amber
		public const byte PHOS_FAINT = 10;  // #3d2a10 faint amber (selection bg)
		public const byte PHOS_DIM   = 11;  // #c07818 dim amber (meter fill)
		public const byte PHOS       = 12;  // #f0a030 phosphor accent (amber)
		public const byte PHOS_GLOW  = 13;  // #f8c060 bright amber
		public const byte OK         = 14;  // #5db536 green status
		public const byte WARN       = 15;  // #d0541a orange warning
		public const byte ALERT      = 16;  // #c42820 red alert
		public const byte CYAN       = 17;  // #3aaccc info / cold accent

		// Build a fresh Palette populated with the cassette design-token colors.
		// Caller is responsible for merging any game-asset palette ranges on top.
		public static Palette CreatePalette()
		{
			var p = new Palette();
			p[BG0]        = new Colour( 10,   8,   6);
			p[BG1]        = new Colour( 18,  16,   9);
			p[BG2]        = new Colour( 27,  24,  16);
			p[BG3]        = new Colour( 38,  34,  26);
			p[BORDER]     = new Colour( 58,  49,  34);
			p[INK_LOW]    = new Colour(106,  90,  60);
			p[INK_MID]    = new Colour(179, 156, 114);
			p[INK_HIGH]   = new Colour(244, 230, 200);
			p[PHOS_GHOST] = new Colour( 32,  21,   8);
			p[PHOS_FAINT] = new Colour( 61,  42,  16);
			p[PHOS_DIM]   = new Colour(192, 120,  24);
			p[PHOS]       = new Colour(240, 160,  48);
			p[PHOS_GLOW]  = new Colour(248, 192,  96);
			p[OK]         = new Colour( 93, 181,  54);
			p[WARN]       = new Colour(208,  84,  26);
			p[ALERT]      = new Colour(196,  40,  32);
			p[CYAN]       = new Colour( 58, 172, 204);
			return p;
		}
	}
}
