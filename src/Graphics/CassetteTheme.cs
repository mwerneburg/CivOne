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
		public const byte WHITE      = 15;  // #f8f4ec near-white (unit icon white player; keep at 15 to match ColourLight)
		public const byte ALERT      = 16;  // #c42820 red alert
		public const byte CYAN       = 17;  // #3aaccc info / cold accent
		public const byte OCEAN      = 18;  // #165080 static deep ocean (not part of wave cycle)

		// Shore-wave cycling palette (96–103 — the range cycled in GamePlay.HasUpdate).
		// Pixel index 96 starts as the foam crest; with right-shift cycling it travels
		// through indices 97→98→99 toward the shore edge (reversed assignment in shore tiles).
		// Indices 100–103 fill the dark trough between wave crests.
		public const byte WAVE_FOAM  = 96;  // crest / surf white
		public const byte WAVE_SURF  = 97;  // light cyan-blue
		public const byte WAVE_MID   = 98;  // mid-blue shallows (≈ CYAN)
		public const byte WAVE_BASE  = 99;  // dark wave base
		// 100–103 are the trough phase — no named constants needed in drawing code.

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
			p[WHITE]      = new Colour(248, 244, 236);
			p[ALERT]      = new Colour(196,  40,  32);
			p[CYAN]       = new Colour( 58, 172, 204);
		p[OCEAN]      = new Colour( 22,  80, 128);  // static deep ocean — mid-way between WAVE_BASE and trough
			// Wave cycling range 96–103 (right-shift in GamePlay; foam travels 96→97→98→99 toward shore)
			p[96]  = new Colour(210, 235, 248);  // WAVE_FOAM  — near-white surf
			p[97]  = new Colour(130, 200, 230);  // WAVE_SURF  — light cyan-blue
			p[98]  = new Colour( 58, 172, 204);  // WAVE_MID   — matches CYAN / ocean base
			p[99]  = new Colour( 28,  95, 150);  // WAVE_BASE  — dark approaching wave
			p[100] = new Colour( 20,  70, 120);  // trough 1
			p[101] = new Colour( 15,  55,  98);  // trough 2
			p[102] = new Colour( 12,  45,  80);  // trough 3
			p[103] = new Colour( 10,  35,  65);  // trough deepest
			return p;
		}

		// Merge all Cassette palette ranges (tokens 1-18 and wave cycling 96-103) into p.
		internal static void ApplyTo(Palette p)
		{
			using (Palette cassette = CreatePalette())
			{
				p.MergePalette(cassette, 1, 18);
				p.MergePalette(cassette, 96, 8);
			}
		}
	}
}
