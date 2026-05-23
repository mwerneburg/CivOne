// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Graphics.Sprites;
using CivOne.Wonders;

using UniversityBuilding = CivOne.Buildings.University;
using ObservatoryBuilding = CivOne.Buildings.Observatory;

namespace CivOne.Screens
{
	[Modal, Expand]
	internal class CityView : BaseScreen
	{
		private const float FADE_STEP = 0.1f;
		private const int NOISE_COUNT = 40;

		private readonly TextSettings _dialogText;

		private readonly City _city;
		private readonly IProduction _production;
		private readonly Picture _background;
		private readonly bool _founded;
		private readonly bool _firstView;
		private readonly bool _captured;
		private readonly bool _disorder;
		private readonly bool _weLovePresidentDay;
		private readonly byte[,] _noiseMap;

		private int _noiseCounter = NOISE_COUNT + 15;

		private int OX => (Width - 320) / 2;
		private int OY => (Height - 200) / 2;

		private int _houseType = 0;

		private readonly Picture _overlay;
		private readonly Picture[] _invadersOrRevolters;

		private bool _update = true;

		private int _x = 80, _y = 138;
		private float _fadeStep = 1.0f;

		// ── view-only panorama mode ───────────────────────────────────────────
		private readonly bool _viewOnly;
		private readonly bool _viewCelebrate;
		private readonly bool _viewDisorder;

		private struct FireworkBurst { public int X, Y, Age; public byte Col; }
		private readonly System.Collections.Generic.List<FireworkBurst> _bursts
			= new System.Collections.Generic.List<FireworkBurst>();

		private struct SmokeParticle { public float X, Y; public int Age; }
		private readonly System.Collections.Generic.List<SmokeParticle> _smokeParticles
			= new System.Collections.Generic.List<SmokeParticle>();
		private (int X, int Y)[] _smokeSources;

		public event EventHandler Skipped;

		// ── native palette / background ──────────────────────────────────────

		private static Palette BuildNativePalette()
		{
			Palette p = Common.DefaultPalette;
			using (Palette c = CassetteTheme.CreatePalette())
				p.MergePalette(c, 1, 17);
			return p;
		}

		private static Picture NativeBackground()
		{
			using Palette pal = BuildNativePalette();
			var pic = new Picture(320, 200, pal);

			// Sky
			pic.FillRectangle(0, 0, 320, 74, CassetteTheme.BG0);
			// Horizon band
			pic.FillRectangle(0, 74, 320, 4, CassetteTheme.BG1);
			// Ground
			pic.FillRectangle(0, 78, 320, 122, CassetteTheme.BG2);

			// Deterministic stars and ground pebbles
			var rng = new System.Random(0x4A3C);
			for (int i = 0; i < 25; i++)
				pic.FillRectangle(rng.Next(320), rng.Next(55), 1, 1, CassetteTheme.PHOS_GHOST);
			for (int i = 0; i < 80; i++)
				pic.FillRectangle(rng.Next(320), 78 + rng.Next(122), 2, 1, CassetteTheme.BG1);

			return pic;
		}

		// ── native house tiles (31×31, index 0 = transparent) ────────────────

		private static Picture NativeHouse(int stage, int houseType)
		{
			var picture = new Picture(31, 31);
			if (stage < 2)
			{
				// Ancient hut
				picture.FillRectangle(6, 14, 19, 17, CassetteTheme.BG3);
				// Roof triangle
				for (int row = 0; row < 6; row++)
				{
					int left = 10 - row;
					int w    = 11 + row * 2;
					picture.FillRectangle(left, 8 + row, w, 1, CassetteTheme.BORDER);
				}
				// Door
				picture.FillRectangle(13, 22, 5, 9, CassetteTheme.BG0);
			}
			else if (stage < 8)
			{
				// Classical house
				picture.FillRectangle(4, 12, 23, 19, CassetteTheme.BORDER);
				// Roof triangle
				for (int row = 0; row < 8; row++)
				{
					int left = 15 - row;
					int w    = row * 2 + 1;
					picture.FillRectangle(left, 4 + row, w, 1, CassetteTheme.INK_LOW);
				}
				// Windows
				picture.FillRectangle(7, 14, 4, 4, CassetteTheme.PHOS_GHOST);
				picture.FillRectangle(18, 14, 4, 4, CassetteTheme.PHOS_GHOST);
				// Door
				picture.FillRectangle(12, 22, 7, 9, CassetteTheme.BG0);
			}
			else if (stage < 16)
			{
				// Merchant house — houseType gives slight colour variation
				byte body = houseType == 0 ? CassetteTheme.INK_LOW : CassetteTheme.BORDER;
				picture.FillRectangle(2, 8, 27, 23, body);
				// Roof
				for (int row = 0; row < 6; row++)
				{
					int left = 15 - row * 2;
					int w    = row * 4 + 1;
					if (left < 0) { w += left * 2; left = 0; }
					picture.FillRectangle(left, 2 + row, w, 1, CassetteTheme.BORDER);
				}
				// Windows (two rows)
				picture.FillRectangle(5,  10, 5, 5, CassetteTheme.PHOS_FAINT);
				picture.FillRectangle(19, 10, 5, 5, CassetteTheme.PHOS_FAINT);
				picture.FillRectangle(5,  18, 5, 5, CassetteTheme.PHOS_FAINT);
				picture.FillRectangle(19, 18, 5, 5, CassetteTheme.PHOS_FAINT);
				// Door
				picture.FillRectangle(12, 22, 7, 9, CassetteTheme.BG0);
			}
			else if (stage < 20)
			{
				// Industrial flat
				picture.FillRectangle(0, 6, 31, 25, CassetteTheme.INK_LOW);
				// Roof bar
				picture.FillRectangle(0, 4, 31, 2, CassetteTheme.BORDER);
				// Chimney
				picture.FillRectangle(22, 0, 5, 6, CassetteTheme.INK_MID);
				// Windows
				picture.FillRectangle(3,  10, 5, 6, CassetteTheme.PHOS_FAINT);
				picture.FillRectangle(11, 10, 5, 6, CassetteTheme.PHOS_FAINT);
				picture.FillRectangle(19, 10, 5, 6, CassetteTheme.PHOS_FAINT);
				// Door
				picture.FillRectangle(12, 22, 7, 9, CassetteTheme.BG0);
			}
			else
			{
				// Glass tower
				picture.FillRectangle(4, 0, 23, 31, CassetteTheme.BG3);
				// Vertical dividers
				picture.FillRectangle(4,  0, 1, 31, CassetteTheme.BORDER);
				picture.FillRectangle(12, 0, 1, 31, CassetteTheme.BORDER);
				picture.FillRectangle(19, 0, 1, 31, CassetteTheme.BORDER);
				picture.FillRectangle(26, 0, 1, 31, CassetteTheme.BORDER);
				// Horizontal bands
				picture.FillRectangle(4, 0,  23, 1, CassetteTheme.BORDER);
				picture.FillRectangle(4, 6,  23, 1, CassetteTheme.BORDER);
				picture.FillRectangle(4, 12, 23, 1, CassetteTheme.BORDER);
				picture.FillRectangle(4, 18, 23, 1, CassetteTheme.BORDER);
				picture.FillRectangle(4, 24, 23, 1, CassetteTheme.BORDER);
				picture.FillRectangle(4, 30, 23, 1, CassetteTheme.BORDER);
				// Window glass cells
				for (int col = 0; col < 3; col++)
				for (int row = 0; row < 5; row++)
					picture.FillRectangle(5 + col * 7, 1 + row * 6, 6, 5, CassetteTheme.PHOS_FAINT);
				// Dark ground-floor door
				picture.FillRectangle(12, 24, 7, 7, CassetteTheme.BG0);
			}
			return picture;
		}

		// ── native tree sprite (24×8) ─────────────────────────────────────────

		private static Picture NativeTree()
		{
			var picture = new Picture(24, 8);
			// Foliage blob
			picture.FillRectangle(4, 0, 16, 5, CassetteTheme.OK);
			// Trim corners for oval-ish look
			picture.FillRectangle(4, 0, 2, 2, 0);
			picture.FillRectangle(18, 0, 2, 2, 0);
			picture.FillRectangle(4, 3, 2, 2, 0);
			picture.FillRectangle(18, 3, 2, 2, 0);
			// Trunk
			picture.FillRectangle(10, 5, 4, 3, CassetteTheme.INK_LOW);
			return picture;
		}

		// ── native road sprite (24×8) ─────────────────────────────────────────

		private static Picture NativeRoad(Direction road)
		{
			var picture = new Picture(24, 8);
			picture.FillRectangle(0, 0, 24, 8, CassetteTheme.BG2);
			// Center strip
			picture.FillRectangle(0, 3, 24, 2, CassetteTheme.BORDER);
			if ((road & Direction.North) != 0) picture.FillRectangle(10, 0, 4, 3, CassetteTheme.BORDER);
			if ((road & Direction.South) != 0) picture.FillRectangle(10, 5, 4, 3, CassetteTheme.BORDER);
			if ((road & Direction.East)  != 0) picture.FillRectangle(14, 3, 10, 2, CassetteTheme.BORDER);
			if ((road & Direction.West)  != 0) picture.FillRectangle(0,  3, 10, 2, CassetteTheme.BORDER);
			return picture;
		}

		// ── glyph drawing helper for 15×15 centered in 49×49 ─────────────────

		private static void DrawGlyph(Picture p, int bx, int by, string glyph, byte col)
		{
			// glyph area: bx+17 .. bx+31, by+17 .. by+31
			int ox = bx + 17;
			int oy = by + 17;
			switch (glyph)
			{
				case "+":
					p.FillRectangle(ox,     oy + 6, 15, 3, col);
					p.FillRectangle(ox + 6, oy,     3, 15, col);
					break;
				case "o":
					p.FillRectangle(ox,     oy,     15, 3, col);
					p.FillRectangle(ox,     oy + 12, 15, 3, col);
					p.FillRectangle(ox,     oy,     3, 15, col);
					p.FillRectangle(ox + 12, oy,    3, 15, col);
					break;
				case "oo":
					// double concentric ring
					p.FillRectangle(ox,     oy,     15, 2, col);
					p.FillRectangle(ox,     oy + 13, 15, 2, col);
					p.FillRectangle(ox,     oy,     2, 15, col);
					p.FillRectangle(ox + 13, oy,    2, 15, col);
					p.FillRectangle(ox + 3, oy + 3, 9, 2, col);
					p.FillRectangle(ox + 3, oy + 10, 9, 2, col);
					p.FillRectangle(ox + 3, oy + 3, 2, 9, col);
					p.FillRectangle(ox + 10, oy + 3, 2, 9, col);
					break;
				case "X":
					p.DrawLine(ox, oy, ox + 14, oy + 14, col);
					p.DrawLine(ox + 14, oy, ox, oy + 14, col);
					break;
				case "$":
					p.FillRectangle(ox + 6, oy,      3, 15, col);
					p.FillRectangle(ox + 1, oy + 1,  13, 3, col);
					p.FillRectangle(ox + 1, oy + 11, 13, 3, col);
					break;
				case "$$":
					// bank double-dollar
					p.FillRectangle(ox + 3, oy,     3, 15, col);
					p.FillRectangle(ox + 9, oy,     3, 15, col);
					p.FillRectangle(ox,     oy + 1, 7, 2, col);
					p.FillRectangle(ox + 8, oy + 1, 7, 2, col);
					p.FillRectangle(ox,     oy + 12, 7, 2, col);
					p.FillRectangle(ox + 8, oy + 12, 7, 2, col);
					break;
				case "L":
					p.FillRectangle(ox + 1, oy,     3, 15, col);
					p.FillRectangle(ox + 1, oy + 12, 13, 3, col);
					break;
				case "#":
					p.FillRectangle(ox,     oy + 3, 15, 3, col);
					p.FillRectangle(ox,     oy + 9, 15, 3, col);
					p.FillRectangle(ox + 3, oy,     3, 15, col);
					p.FillRectangle(ox + 9, oy,     3, 15, col);
					break;
				case "^":
					p.FillRectangle(ox + 6, oy,      3, 3, col);
					p.FillRectangle(ox + 4, oy + 3,  7, 3, col);
					p.FillRectangle(ox + 2, oy + 6, 11, 3, col);
					p.FillRectangle(ox,     oy + 9, 15, 3, col);
					break;
				case "*":
					p.FillRectangle(ox,     oy + 6, 15, 3, col);
					p.FillRectangle(ox + 6, oy,     3, 15, col);
					p.DrawLine(ox, oy, ox + 14, oy + 14, col);
					p.DrawLine(ox + 14, oy, ox, oy + 14, col);
					break;
				case "~":
					p.FillRectangle(ox,     oy + 4, 15, 2, col);
					p.FillRectangle(ox,     oy + 9, 15, 2, col);
					break;
				case "T":
					p.FillRectangle(ox,     oy,     15, 3, col);
					p.FillRectangle(ox + 6, oy + 3, 3, 12, col);
					break;
			}
		}

		// ── native special-building drawing (49×49 onto picture at x,y) ───────

		private static void DrawNativeBuilding(Picture picture, int x, int y,
		                                        byte bodyCol, byte accentCol, string glyph,
		                                        byte chimneyCol = 0, int chimneys = 0)
		{
			// Body fill
			picture.FillRectangle(x, y, 49, 49, bodyCol);
			// Outline
			picture.DrawRectangle(x, y, 49, 49, CassetteTheme.BORDER);
			// Chimney(s) above building
			if (chimneys == 1)
				picture.FillRectangle(x + 22, y - 10, 5, 12, chimneyCol);
			else if (chimneys == 2)
			{
				picture.FillRectangle(x + 13, y - 10, 5, 12, chimneyCol);
				picture.FillRectangle(x + 31, y - 10, 5, 12, chimneyCol);
			}
			// Glyph
			DrawGlyph(picture, x, y, glyph, accentCol);
		}

		// ── native wonder drawing onto picture ────────────────────────────────

		private void DrawWonder<T>(Picture picture = null, int x = -1, int y = -1) where T : IWonder
		{
			if (picture is null) picture = _background;

			if (typeof(T) == typeof(Pyramids))
			{
				// Two pyramid triangles near top centre
				for (int row = 0; row < 28; row++)
				{
					int leftW = row * 7;
					picture.FillRectangle(160 - leftW / 2, row, leftW / 2,     1, CassetteTheme.INK_MID);
					picture.FillRectangle(165,             row, leftW / 2 + 1, 1, CassetteTheme.BORDER);
				}
			}
			else if (typeof(T) == typeof(Colossus))
			{
				// Tall figure silhouette on right
				picture.FillRectangle(280, 0,  30, 60, CassetteTheme.BORDER);
				picture.FillRectangle(270, 55, 10, 25, CassetteTheme.INK_LOW);
				picture.FillRectangle(300, 55, 10, 25, CassetteTheme.INK_LOW);
			}
			else if (typeof(T) == typeof(GreatWall))
			{
				picture.FillRectangle(0, 0, 66, 80, CassetteTheme.INK_LOW);
				for (int xx = 0; xx < 66; xx += 6)
					picture.FillRectangle(xx, 0, 3, 4, CassetteTheme.BORDER);
			}
			else if (typeof(T) == typeof(HooverDam))
			{
				picture.FillRectangle(1, 9, 147, 20, CassetteTheme.INK_MID);
				picture.FillRectangle(1, 28, 147, 2, CassetteTheme.CYAN);
			}
			else if (typeof(T) == typeof(Lighthouse))
			{
				if (x < 0 || y < 0) return;
				// Tower
				picture.FillRectangle(x + 20, y, 8, 48, CassetteTheme.BORDER);
				// Beacon
				picture.FillRectangle(x + 17, y, 14, 6, CassetteTheme.PHOS_GLOW);
			}
			else if (typeof(T) == typeof(HangingGardens))
			{
				if (x < 0 || y < 0) return;
				// Three tiered terraces
				picture.FillRectangle(x,      y + 30, 60, 8, CassetteTheme.BORDER);
				picture.FillRectangle(x + 5,  y + 20, 50, 8, CassetteTheme.BORDER);
				picture.FillRectangle(x + 10, y + 10, 40, 8, CassetteTheme.BORDER);
				picture.FillRectangle(x + 2,  y + 24, 56, 6, CassetteTheme.OK);
				picture.FillRectangle(x + 7,  y + 14, 46, 6, CassetteTheme.OK);
				picture.FillRectangle(x + 12, y + 4,  36, 6, CassetteTheme.OK);
			}
			else if (typeof(T) == typeof(Oracle))
			{
				if (x < 0 || y < 0) return;
				// Angkor Wat: three spired towers in profile, stepped terrace, moat
				// Left tower
				picture.FillRectangle(x + 3,  y + 15, 2,  2, CassetteTheme.BORDER);
				picture.FillRectangle(x + 2,  y + 17, 4,  2, CassetteTheme.BORDER);
				picture.FillRectangle(x + 1,  y + 19, 6,  9, CassetteTheme.BORDER);
				// Center tower (tallest, stepped tiers)
				picture.FillRectangle(x + 20, y + 2,  4,  3, CassetteTheme.BORDER);
				picture.FillRectangle(x + 19, y + 5,  6,  3, CassetteTheme.BORDER);
				picture.FillRectangle(x + 17, y + 8,  10, 3, CassetteTheme.BORDER);
				picture.FillRectangle(x + 15, y + 11, 14, 17, CassetteTheme.BORDER);
				// Right tower
				picture.FillRectangle(x + 39, y + 15, 2,  2, CassetteTheme.BORDER);
				picture.FillRectangle(x + 38, y + 17, 4,  2, CassetteTheme.BORDER);
				picture.FillRectangle(x + 37, y + 19, 6,  9, CassetteTheme.BORDER);
				// Shared terrace platform
				picture.FillRectangle(x,      y + 28, 44, 4, CassetteTheme.BORDER);
				// Moat (reflecting pool)
				picture.FillRectangle(x,      y + 36, 44, 2, CassetteTheme.CYAN);
				// Ground
				picture.FillRectangle(x,      y + 44, 44, 4, CassetteTheme.BG3);
			}
			else if (typeof(T) == typeof(DarwinsVoyage))
			{
				if (x < 0 || y < 0) return;
				// Globe outline (oval) + equator
				picture.DrawRectangle(x + 5, y + 5, 52, 38, CassetteTheme.CYAN);
				picture.FillRectangle(x + 5, y + 22, 52, 2, CassetteTheme.INK_MID);
				// Prime meridian
				picture.FillRectangle(x + 30, y + 5, 2, 38, CassetteTheme.INK_MID);
			}
		}

		private void DrawWonderOverlay<T>(int x, int y, int offset) where T : IWonder
		{
			DrawWonder<T>(x: x, y: y + offset);
			if (!(_production is T))
				DrawWonder<T>(_overlay, x, y + offset);
		}

		// ── native building drawing ───────────────────────────────────────────

		private void DrawBuilding<T>(Picture picture = null, int x = -1, int y = -1) where T : IBuilding
		{
			if (picture is null) picture = _background;

			if (typeof(T) == typeof(Aqueduct))
			{
				// Row of arches at y=72
				for (int i = 0; i < 5; i++)
				{
					int ax = 10 + i * 60;
					picture.FillRectangle(ax,      72, 4, 20, CassetteTheme.BORDER);
					picture.FillRectangle(ax + 16, 72, 4, 20, CassetteTheme.BORDER);
					picture.FillRectangle(ax,      72, 20, 4, CassetteTheme.BORDER);
					picture.FillRectangle(ax + 2,  76, 16, 12, CassetteTheme.BG3);
				}
				return;
			}

			if (typeof(T) == typeof(CityWalls))
			{
				// Wall sections on either side of gate
				picture.FillRectangle(0,   108, 142, 12, CassetteTheme.INK_LOW);
				picture.FillRectangle(191, 108, 129, 12, CassetteTheme.INK_LOW);
				// Battlements
				for (int xx = 0; xx < 142; xx += 8)
					picture.FillRectangle(xx, 108, 3, 4, CassetteTheme.BORDER);
				for (int xx = 192; xx < 320; xx += 8)
					picture.FillRectangle(xx, 108, 3, 4, CassetteTheme.BORDER);
				// Gate arch
				picture.FillRectangle(142, 108, 49, 12, CassetteTheme.BG3);
				picture.FillRectangle(155, 108, 6,  8,  CassetteTheme.BG0);
				picture.FillRectangle(161, 108, 6,  8,  CassetteTheme.BG0);
				return;
			}

			if (x < 0 || y < 0) return;

			if (typeof(T) == typeof(Barracks))
				DrawNativeBuilding(picture, x, y, CassetteTheme.INK_LOW, CassetteTheme.ALERT, "+");
			else if (typeof(T) == typeof(Granary))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BORDER, CassetteTheme.OK, "o");
			else if (typeof(T) == typeof(Temple))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG3, CassetteTheme.PHOS_GLOW, "*");
			else if (typeof(T) == typeof(MarketPlace))
				DrawNativeBuilding(picture, x, y, CassetteTheme.INK_LOW, CassetteTheme.PHOS_DIM, "$");
			else if (typeof(T) == typeof(Library))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG3, CassetteTheme.CYAN, "L");
			else if (typeof(T) == typeof(Courthouse))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BORDER, CassetteTheme.WHITE, "T");
			else if (typeof(T) == typeof(Bank))
				DrawNativeBuilding(picture, x, y, CassetteTheme.INK_LOW, CassetteTheme.PHOS_GLOW, "$$");
			else if (typeof(T) == typeof(Cathedral))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG3, CassetteTheme.WHITE, "^");
			else if (typeof(T) == typeof(ObservatoryBuilding))
				DrawNativeBuilding(picture, x, y, CassetteTheme.INK_LOW, CassetteTheme.CYAN, "@");
			else if (typeof(T) == typeof(UniversityBuilding))
				DrawNativeBuilding(picture, x, y, CassetteTheme.INK_LOW, CassetteTheme.CYAN, "#");
			else if (typeof(T) == typeof(Colosseum))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG3, CassetteTheme.INK_MID, "oo");
			else if (typeof(T) == typeof(Factory))
				DrawNativeBuilding(picture, x, y, CassetteTheme.INK_LOW, CassetteTheme.INK_MID, "~",
				                   CassetteTheme.INK_MID, 1);
			else if (typeof(T) == typeof(MfgPlant))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG1, CassetteTheme.INK_MID, "~",
				                   CassetteTheme.INK_MID, 2);
			else if (typeof(T) == typeof(SdiDefense))
			{
				// Dome shape
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG2, CassetteTheme.PHOS_GLOW, "o");
				// Beacon highlight
				picture.FillRectangle(x + 22, y + 4, 5, 5, CassetteTheme.PHOS_GLOW);
			}
			else if (typeof(T) == typeof(RecyclingCenter))
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG3, CassetteTheme.OK, "X");
			else if (typeof(T) == typeof(NuclearPlant))
			{
				DrawNativeBuilding(picture, x, y, CassetteTheme.BG1, CassetteTheme.CYAN, "oo",
				                   CassetteTheme.INK_MID, 2);
				// Inner accent ring: ALERT
				picture.FillRectangle(x + 20, y + 20, 9, 2, CassetteTheme.ALERT);
				picture.FillRectangle(x + 20, y + 27, 9, 2, CassetteTheme.ALERT);
				picture.FillRectangle(x + 20, y + 20, 2, 9, CassetteTheme.ALERT);
				picture.FillRectangle(x + 27, y + 20, 2, 9, CassetteTheme.ALERT);
			}
		}

		private void DrawBuildingOverlay<T>(int x, int y, int offset = -18) where T : IBuilding
		{
			DrawBuilding<T>(x: x, y: y + offset);
			if (!(_production is T))
				DrawBuilding<T>(_overlay, x, y + offset);
		}

		// ── city map (unchanged) ──────────────────────────────────────────────

		private CityViewMap[,] GetCityMap
		{
			get
			{
				Common.SetRandomSeedFromName(_city.Name);
				_houseType = Common.Random.Next(2);

				CityViewMap[,] cityMap = new CityViewMap[18, 11];
				for (int yy = 0; yy < 11; yy++)
				for (int xx = 0; xx < 18; xx++)
				{
					if (xx == 6 || xx == 11 || yy == 2 || yy == 6)
						cityMap[xx, yy] = CityViewMap.Road;
					if ((xx < 2 && yy < 3) || (xx > 16 && yy > 8))
						cityMap[xx, yy] = CityViewMap.Occupied;
				}

				int ww = 4 + _city.Size;
				int hh = 4 + (_city.Size - 1);
				if (ww > 18) ww = 18;
				if (hh > 11) hh = 11;

				int bx = (ww / 2) + ((18 - ww) / 2);
				int by = (hh / 2);
				for (int ii = 0; ii < _city.Size; ii++)
				{
					for (int t = 0; t < 16; t++)
					{
						int relX = Common.Random.Next(-1, 2);
						int relY = Common.Random.Next(-1, 2);
						if (relX == 0 && relY == 0) continue;
						bx += relX;
						by += relY;
						while (bx < ((18 - ww) / 2)) bx++;
						while (bx >= ww + ((18 - ww) / 2)) bx--;
						while (by < 0) by++;
						while (by >= hh) by--;
						int type = Common.Random.Next(8);
						if (cityMap[bx, by] != CityViewMap.Empty) continue;
						if (type < 6)
							cityMap[bx, by] = CityViewMap.House;
						else
							cityMap[bx, by] = CityViewMap.Tree;
					}
					for (int i = 0; i < 1000; i++)
					{
						bx = Common.Random.Next(ww) + ((18 - ww) / 2);
						by = Common.Random.Next(hh);
						if (cityMap[bx, by] != CityViewMap.Empty) continue;
						for (int ix = -1; ix < 2; ix++)
						for (int iy = -1; iy < 2; iy++)
						{
							if (Math.Abs(ix) == Math.Abs(iy)) continue;
							if (bx + ix < ((18 - ww) / 2)) continue;
							if (bx + ix >= ww + ((18 - ww) / 2)) continue;
							if (by + iy < 0) continue;
							if (by + iy >= hh) continue;
							if (cityMap[bx + ix, by + iy] != CityViewMap.Empty) { i = 1000; break; }
						}
					}
				}

				for (int yy = 0; yy < 11; yy++)
				for (int xx = 0; xx < 18; xx++)
				{
					if ((int)cityMap[xx, yy] > 1)
					{
						if ((xx == 0 || (cityMap[xx - 1, yy] != CityViewMap.House && cityMap[xx - 1, yy] != CityViewMap.Tree)) &&
							(xx == 17 || (cityMap[xx + 1, yy] != CityViewMap.House && cityMap[xx + 1, yy] != CityViewMap.Tree)) &&
							(yy == 0 || (cityMap[xx, yy - 1] != CityViewMap.House && cityMap[xx, yy - 1] != CityViewMap.Tree)) &&
							(yy == 10 || (cityMap[xx, yy + 1] != CityViewMap.House && cityMap[xx, yy + 1] != CityViewMap.Tree)))
							cityMap[xx, yy] = CityViewMap.Empty;
					}
					if (cityMap[xx, yy] != CityViewMap.Road) continue;
					if ((xx == 0 || (int)cityMap[xx - 1, yy] > 1) ||
						(xx == 17 || (int)cityMap[xx + 1, yy] > 1) ||
						(yy == 0 || (int)cityMap[xx, yy - 1] > 1) ||
						(yy == 10 || (int)cityMap[xx, yy + 1] > 1)) continue;
					cityMap[xx, yy] = CityViewMap.Empty;
				}

				for (int yy = 0; yy < 11; yy++)
				for (int xx = 0; xx < 18; xx++)
				{
					if (cityMap[xx, yy] != CityViewMap.Empty) continue;
					if (!(xx == 6 || xx == 11 || yy == 2 || yy == 6)) continue;
					if (((xx == 0 || (int)cityMap[xx - 1, yy] != 1) ? 1 : 0) +
						((xx == 17 || (int)cityMap[xx + 1, yy] != 1) ? 1 : 0) +
						((yy == 0 || (int)cityMap[xx, yy - 1] != 1) ? 1 : 0) +
						((yy == 10 || (int)cityMap[xx, yy + 1] != 1 ? 1 : 0)) > 1) continue;
					cityMap[xx, yy] = CityViewMap.Road;
				}

				foreach (Type type in (Type[])
				[
					typeof(Barracks), typeof(Granary), typeof(Temple), typeof(MarketPlace),
					typeof(Library), typeof(Courthouse), typeof(Bank), typeof(Cathedral),
                    typeof(ObservatoryBuilding),
					typeof(UniversityBuilding), typeof(Colosseum), typeof(Factory), typeof(MfgPlant),
					typeof(SdiDefense), typeof(RecyclingCenter), typeof(NuclearPlant),
					typeof(Lighthouse), typeof(HangingGardens), typeof(Oracle), typeof(DarwinsVoyage)
				])
				{
					if (_city.HasBuilding(type) || _city.HasWonder(type))
					{
						int sizeX = 2, sizeY = 2;
						CityViewMap id;
						if      (type == typeof(Barracks))         id = CityViewMap.Barracks;
						else if (type == typeof(Granary))          id = CityViewMap.Granary;
						else if (type == typeof(Temple))           id = CityViewMap.Temple;
						else if (type == typeof(MarketPlace))      id = CityViewMap.MarketPlace;
						else if (type == typeof(Library))          id = CityViewMap.Library;
						else if (type == typeof(Courthouse))       id = CityViewMap.Courthouse;
						else if (type == typeof(Bank))             id = CityViewMap.Bank;
						else if (type == typeof(Cathedral))        id = CityViewMap.Cathedral;
						else if (type == typeof(ObservatoryBuilding)) id = CityViewMap.Observatory;
						else if (type == typeof(UniversityBuilding)) id = CityViewMap.University;
						else if (type == typeof(Colosseum))        id = CityViewMap.Colosseum;
						else if (type == typeof(Factory))          id = CityViewMap.Factory;
						else if (type == typeof(MfgPlant))         id = CityViewMap.MfgPlant;
						else if (type == typeof(SdiDefense))       id = CityViewMap.SdiDefense;
						else if (type == typeof(RecyclingCenter))  id = CityViewMap.RecyclingCenter;
						else if (type == typeof(NuclearPlant))     id = CityViewMap.NuclearPlant;
						else if (type == typeof(Lighthouse))       id = CityViewMap.Lighthouse;
						else if (type == typeof(HangingGardens))  { id = CityViewMap.HangingGardens; sizeX = 3; sizeY = 3; }
						else if (type == typeof(Oracle))           { id = CityViewMap.AngkorWat;      sizeX = 3; sizeY = 3; }
						else if (type == typeof(DarwinsVoyage))    { id = CityViewMap.DarwinsVoyage;  sizeX = 3; sizeY = 3; }
						else continue;

						for (int i = 0; i < 1000; i++)
						{
							int xx = Common.Random.Next(15) + 1;
							int yy = Common.Random.Next(10);
							if (xx == 6 || xx == 11 || yy == 2 || yy == 6) continue;
							if (xx == 5 || xx == 10 || yy == 1 || yy == 5) continue;
							if (xx + sizeX > cityMap.GetLength(0) || yy + sizeY > cityMap.GetLength(1)) continue;
							if ((int)cityMap[xx, yy] > 3) continue;
							bool invalid = false;
							for (int oy = 0; oy < sizeY; oy++)
							for (int ox = 0; ox < sizeX; ox++)
							{
								if ((int)cityMap[xx + ox, yy + oy] <= 3) continue;
								invalid = true;
								break;
							}
							if (invalid) continue;
							cityMap[xx, yy] = id;
							for (int oy = 0; oy < sizeY; oy++)
							for (int ox = 0; ox < sizeX; ox++)
							{
								if (ox == 0 && oy == 0) continue;
								cityMap[xx + ox, yy + oy] = CityViewMap.Occupied;
							}
							break;
						}
					}
				}
				return cityMap;
			}
		}

		// ── DrawBuildings ─────────────────────────────────────────────────────

		private void DrawBuildings()
		{
			CityViewMap[,] cityMap = GetCityMap;

			if (_city.Wonders.Any(b => b is Pyramids))
			{
				DrawWonder<Pyramids>();
				if (!(_production is Pyramids)) DrawWonder<Pyramids>(_overlay);
			}
			if (_city.Wonders.Any(b => b is Colossus))
			{
				DrawWonder<Colossus>();
				if (!(_production is Colossus)) DrawWonder<Colossus>(_overlay);
			}
			if (_city.Wonders.Any(b => b is GreatWall))
			{
				DrawWonder<GreatWall>();
				if (!(_production is GreatWall)) DrawWonder<GreatWall>(_overlay);
			}
			if (_city.Wonders.Any(b => b is HooverDam))
			{
				DrawWonder<HooverDam>();
				if (!(_production is HooverDam)) DrawWonder<HooverDam>(_overlay);
			}

			if (_city.Buildings.Any(b => b is Aqueduct))
			{
				DrawBuilding<Aqueduct>();
				if (!(_production is Aqueduct)) DrawBuilding<Aqueduct>(_overlay);
			}

			int stage = (int)Math.Floor((double)(Game.GetPlayer(_city.Owner).Advances.Count() - 9) / 2);
			for (int xx = 0; xx < 18; xx++)
			for (int yy = 10; yy >= 0; yy--)
			{
				int dx = 0 + (16 * xx) + (yy * 8);
				int dy = 106 - (yy * 8);
				Picture building;
				switch (cityMap[xx, yy])
				{
					case CityViewMap.House:
						building = NativeHouse(stage, _houseType);
						break;
					case CityViewMap.Tree:
						building = NativeTree();
						dx -= 5;
						dy += 24;
						break;
					case CityViewMap.Road:
					{
						Direction road = 0;
						if (yy < cityMap.GetUpperBound(1) && cityMap[xx, yy + 1] == CityViewMap.Road) road |= Direction.North;
						if (xx < cityMap.GetUpperBound(0) && cityMap[xx + 1, yy] == CityViewMap.Road) road |= Direction.East;
						if (yy > 0 && cityMap[xx, yy - 1] == CityViewMap.Road)                        road |= Direction.South;
						if (xx > 0 && cityMap[xx - 1, yy] == CityViewMap.Road)                        road |= Direction.West;
						if (road == 0) continue;
						building = NativeRoad(road);
						dx -= 5;
						dy += 24;
						break;
					}
					case CityViewMap.Barracks:      DrawBuildingOverlay<Barracks>(dx, dy);         continue;
					case CityViewMap.Granary:        DrawBuildingOverlay<Granary>(dx, dy);          continue;
					case CityViewMap.Temple:         DrawBuildingOverlay<Temple>(dx, dy);           continue;
					case CityViewMap.MarketPlace:    DrawBuildingOverlay<MarketPlace>(dx, dy);      continue;
					case CityViewMap.Library:        DrawBuildingOverlay<Library>(dx, dy);          continue;
					case CityViewMap.Courthouse:     DrawBuildingOverlay<Courthouse>(dx, dy);       continue;
					case CityViewMap.Bank:           DrawBuildingOverlay<Bank>(dx, dy);             continue;
					case CityViewMap.Cathedral:      DrawBuildingOverlay<Cathedral>(dx, dy);        continue;
					case CityViewMap.Observatory:     DrawBuildingOverlay<ObservatoryBuilding>(dx, dy); continue;
					case CityViewMap.University:     DrawBuildingOverlay<UniversityBuilding>(dx, dy); continue;
					case CityViewMap.Colosseum:      DrawBuildingOverlay<Colosseum>(dx, dy);        continue;
					case CityViewMap.Factory:        DrawBuildingOverlay<Factory>(dx, dy);          continue;
					case CityViewMap.MfgPlant:       DrawBuildingOverlay<MfgPlant>(dx, dy);         continue;
					case CityViewMap.SdiDefense:     DrawBuildingOverlay<SdiDefense>(dx, dy);       continue;
					case CityViewMap.RecyclingCenter: DrawBuildingOverlay<RecyclingCenter>(dx, dy); continue;
					case CityViewMap.NuclearPlant:   DrawBuildingOverlay<NuclearPlant>(dx, dy);     continue;
					case CityViewMap.Lighthouse:     DrawWonderOverlay<Lighthouse>(dx, dy, -52);    continue;
					case CityViewMap.HangingGardens: DrawWonderOverlay<HangingGardens>(dx, dy, -19); continue;
					case CityViewMap.AngkorWat:      DrawWonderOverlay<Oracle>(dx, dy, -20);        continue;
					case CityViewMap.DarwinsVoyage:  DrawWonderOverlay<DarwinsVoyage>(dx, dy, -16); continue;
					default: continue;
				}
				_background.AddLayer(building, dx, dy);
				_overlay.AddLayer(building, dx, dy);
			}

			if (_city.Buildings.Any(b => b is CityWalls))
			{
				DrawBuilding<CityWalls>();
				if (!(_production is CityWalls)) DrawBuilding<CityWalls>(_overlay);
			}
		}

		// ── native animation frames ───────────────────────────────────────────

		private static void DrawStickFigure(Picture p, int x, int y, int frame, byte col)
		{
			// Head
			p.FillRectangle(x + 5, y,     4, 4, CassetteTheme.INK_MID);
			// Body
			p.FillRectangle(x + 6, y + 5, 2, 10, col);
			// Arms
			if (frame % 2 == 0)
			{
				p.FillRectangle(x + 2, y + 7, 4, 2, col);
				p.FillRectangle(x + 8, y + 7, 4, 2, col);
			}
			else
			{
				p.FillRectangle(x + 2, y + 9, 4, 2, col);
				p.FillRectangle(x + 8, y + 9, 4, 2, col);
			}
			// Legs
			if (frame % 2 == 0)
			{
				p.FillRectangle(x + 4, y + 15, 2, 10, col);
				p.FillRectangle(x + 8, y + 15, 2, 10, col);
			}
			else
			{
				p.FillRectangle(x + 3, y + 15, 2, 10, col);
				p.FillRectangle(x + 9, y + 15, 2, 10, col);
			}
		}

		private static Picture NativeAnimFrame(int frameIndex, bool isCapture, bool isLove)
		{
			byte col = isCapture ? CassetteTheme.ALERT
			         : isLove    ? CassetteTheme.PHOS_GLOW
			                     : CassetteTheme.PHOS_DIM;
			var picture = new Picture(78, 65);
			picture.FillRectangle(0, 0, 78, 65, CassetteTheme.BG0);
			for (int i = 0; i < 4; i++)
				DrawStickFigure(picture, 4 + i * 18, 30, frameIndex, col);
			return picture;
		}

		// ── palette / fade helpers ────────────────────────────────────────────

		private Colour FadeColour(Colour colour1, Colour colour2)
		{
			int r = (int)(colour1.R * (1.0F - _fadeStep) + colour2.R * _fadeStep);
			int g = (int)(colour1.G * (1.0F - _fadeStep) + colour2.G * _fadeStep);
			int b = (int)(colour1.B * (1.0F - _fadeStep) + colour2.B * _fadeStep);
			return new Colour(r, g, b);
		}

		private void FadeColours()
		{
			if (Settings.GraphicsMode != GraphicsMode.Graphics256) return;
			Palette palette = _background.Palette;
			for (int i = 1; i < 256; i++)
				palette[i] = FadeColour(new Colour(0, 0, 0), _background.OriginalColours[i]);
			this.SetPalette(palette);
		}

		// ── firework / smoke helpers ──────────────────────────────────────────

		private static readonly byte[] _fireworkCols
			= { CassetteTheme.PHOS_GLOW, CassetteTheme.CYAN, CassetteTheme.OK, CassetteTheme.PHOS };

		private void DrawBurstRing(int cx, int cy, int r, byte col)
		{
			int d = Math.Max(1, r * 7 / 10);
			int[,] pts = { {cx,cy-r},{cx,cy+r},{cx-r,cy},{cx+r,cy},
			               {cx-d,cy-d},{cx+d,cy-d},{cx-d,cy+d},{cx+d,cy+d} };
			for (int i = 0; i < 8; i++)
				this.FillRectangle(OX + pts[i, 0], OY + pts[i, 1], 1, 1, col);
		}

		private void UpdateFireworks(uint gameTick)
		{
			if (gameTick % 18 == 0)
				_bursts.Add(new FireworkBurst
				{
					X   = Common.Random.Next(240) + 40,
					Y   = Common.Random.Next(42) + 6,
					Age = 0,
					Col = _fireworkCols[Common.Random.Next(_fireworkCols.Length)]
				});
			for (int i = _bursts.Count - 1; i >= 0; i--)
			{
				var burst = _bursts[i];
				int radius = burst.Age + 1;
				DrawBurstRing(burst.X, burst.Y, radius, burst.Col);
				if (burst.Age > 2) DrawBurstRing(burst.X, burst.Y, radius - 2, CassetteTheme.PHOS_DIM);
				_bursts[i] = new FireworkBurst { X = burst.X, Y = burst.Y, Age = burst.Age + 1, Col = burst.Col };
				if (_bursts[i].Age >= 14) _bursts.RemoveAt(i);
			}
		}

		private void UpdateSmoke(uint gameTick)
		{
			if (gameTick % 3 == 0)
				foreach (var src in _smokeSources)
					_smokeParticles.Add(new SmokeParticle { X = src.X, Y = src.Y, Age = 0 });
			for (int i = _smokeParticles.Count - 1; i >= 0; i--)
			{
				var p = _smokeParticles[i];
				float nx = p.X + (Common.Random.Next(3) - 1) * 0.6f;
				float ny = p.Y - 0.7f;
				int   na = p.Age + 1;
				byte  col = na < 8 ? CassetteTheme.INK_MID : CassetteTheme.INK_LOW;
				this.FillRectangle(OX + (int)nx, OY + (int)ny, na < 6 ? 2 : 1, na < 6 ? 2 : 1, col);
				_smokeParticles[i] = new SmokeParticle { X = nx, Y = ny, Age = na };
				if (na > 22 || (int)ny < 2) _smokeParticles.RemoveAt(i);
			}
		}

		// ── native citizens ───────────────────────────────────────────────────

		private void DrawNativeCitizen(uint gameTick, Citizen citizen, int dx, int dy)
		{
			byte col;
			switch (citizen)
			{
				case Citizen.HappyMale:
				case Citizen.HappyFemale:
					col = CassetteTheme.PHOS_GLOW; break;
				case Citizen.UnhappyMale:
				case Citizen.UnhappyFemale:
					col = CassetteTheme.ALERT; break;
				default:
					col = CassetteTheme.INK_MID; break;
			}
			// Head
			this.FillRectangle(OX + dx + 6, OY + dy + 1, 4, 4, col);
			// Body
			this.FillRectangle(OX + dx + 5, OY + dy + 6, 6, 10, col);
			// Legs (animated)
			bool phase = (gameTick / 4) % 2 == 0;
			this.FillRectangle(OX + dx + 4, OY + dy + 17, 2, 8, col);
			this.FillRectangle(OX + dx + 9, OY + dy + (phase ? 17 : 18), 2, 8, col);
		}

		// ── HasUpdate ─────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (gameTick % 4 == 0)
			{
				this.Cycle(64, 79);
				_update = true;
			}

			if (_viewOnly)
			{
				this.AddLayer(_background, OX, OY);
				if (_viewCelebrate) UpdateFireworks(gameTick);
				if (_viewDisorder)  UpdateSmoke(gameTick);
				return true;
			}

			if (_captured || _disorder)
			{
				this.AddLayer(_background, OX, OY);
				int frame = ((_x % 30) + 30) % 30 / 3;
				for (int i = 7; i >= 0; i--)
				{
					int xx = (_x - 65) - (48 * i);
					if (xx + 78 <= 0) continue;
					this.AddLayer(_invadersOrRevolters[frame], OX + xx, OY + _y);
				}
				_x++;
				return true;
			}

			if (_weLovePresidentDay)
			{
				this.AddLayer(_background, OX, OY);
				int frame = (((_x + 600) % 30) + 30) % 30 / 3;
				for (int i = 0; i <= 7; i++)
				{
					int xx = (_x + 65) + (48 * i);
					this.AddLayer(_invadersOrRevolters[frame], OX + xx, OY + _y);
				}
				_x--;
				return true;
			}

			if (_noiseMap is not null)
			{
				if (_noiseCounter > 0)
				{
					_overlay.ApplyNoise(_noiseMap, _noiseCounter--);
					this.AddLayer(_background, OX, OY)
						.AddLayer(_overlay, OX, OY);
					return true;
				}
				return false;
			}

			if (_founded)
			{
				if (_fadeStep < 1.0f)
				{
					_fadeStep = Math.Min(1.0f, _fadeStep + FADE_STEP);
					FadeColours();
				}
				this.AddLayer(_background, OX, OY)
					.DrawText($"{_city.Name} founded: {Game.GameYear}.", 5, 5, OX + 161, OY + 3, TextAlign.Center);
				if (_fadeStep >= 1.0f && gameTick % 3 == 0 && ++_x > 25)
				{
					Destroy();
					return true;
				}
				return true;
			}

			if (_firstView && _fadeStep < 1.0f)
			{
				_fadeStep += FADE_STEP;
				if (_fadeStep > 1.0f) _fadeStep = 1.0f;
				FadeColours();
			}

			if (_update) _update = false;
			return true;
		}

		// ── input ─────────────────────────────────────────────────────────────

		private bool SkipAction()
		{
			if (_viewOnly) { Destroy(); return true; }
			if (_fadeStep != 0.0F && _fadeStep != 1.0F) return false;
			if (_noiseCounter > 0 && _noiseCounter < NOISE_COUNT) return false;
			Destroy();
			if (Skipped is not null)
				Skipped(this, null);
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)  => SkipAction();
		public override bool MouseDown(ScreenEventArgs args)  => SkipAction();

		// ── static factory methods ────────────────────────────────────────────

		public static CityView Capture(City city)           => new CityView(city, captured: true);
		public static CityView Disorder(City city)          => new CityView(city, disorder: true);
		public static CityView WeLovePresidentDay(City city) => new CityView(city, weLovePresidentDay: true);

		// ── constructor ───────────────────────────────────────────────────────

		public CityView(City city, bool founded = false, bool firstView = false,
		                IProduction production = null, bool captured = false,
		                bool disorder = false, bool weLovePresidentDay = false,
		                bool viewOnly = false)
		{
			_dialogText         = TextSettings.ShadowText(15, 5);
			_dialogText.FontId  = 5;

			_city       = city;
			_production = production;
			_founded    = founded;
			_firstView  = firstView;

			// Native background — no original game assets
			_background = NativeBackground();
			Palette     = _background.Palette;
			_overlay    = new Picture(_background);

			DrawBuildings();
			this.AddLayer(_background, OX, OY);

			// ── view-only panorama ────────────────────────────────────────────
			if (_viewOnly = viewOnly)
			{
				_viewCelebrate = city.WasWeLoveKing && !city.IsInDisorder;
				_viewDisorder  = city.IsInDisorder;
				_smokeSources  = new[]
				{
					(Common.Random.Next(60) + 70,  Common.Random.Next(12) + 68),
					(Common.Random.Next(60) + 140, Common.Random.Next(12) + 72),
					(Common.Random.Next(60) + 185, Common.Random.Next(12) + 66),
				};
				return;
			}

			// ── capture ───────────────────────────────────────────────────────
			if (_captured = captured)
			{
				_invadersOrRevolters = new Picture[10];
				for (int ii = 0; ii < 10; ii++)
					_invadersOrRevolters[ii] = NativeAnimFrame(ii, true, false);
				_x = 0;

				int totalLuxuries = Game.GetPlayer(_city.Owner).Cities.Sum(x => x.Luxuries);
				int totalGold     = Game.GetPlayer(_city.Owner).Gold;
				int cityLuxuries  = _city.Luxuries;
				if (cityLuxuries == 0) cityLuxuries = 1;
				int captureGold   = (totalLuxuries > 0)
					? (int)Math.Floor(((float)totalGold / totalLuxuries) * cityLuxuries)
					: totalGold;
				if (captureGold < 0) captureGold = 0;
				if (captureGold > totalGold) captureGold = totalGold;

				Game.GetPlayer(_city.Owner).Gold = (short)Math.Max(0, Game.GetPlayer(_city.Owner).Gold - captureGold);
				Game.CurrentPlayer.Gold          = (short)Math.Min(30000, Game.CurrentPlayer.Gold + captureGold);

				string[] lines = { $"{Game.CurrentPlayer.TribeNamePlural} capture",
				                   $"{city.Name}. {captureGold} gold", "pieces plundered." };
				int width = lines.Max(l => Resources.GetTextSize(5, l).Width) + 12;
				var dialog = new Picture(width, 54)
					.Tile(Pattern.PanelGrey, 1, 1)
					.DrawRectangle()
					.DrawRectangle3D(1, 1, width - 2, 52)
					.DrawText(lines[0], 5, 6, _dialogText)
					.DrawText(lines[1], 5, 21, _dialogText)
					.DrawText(lines[2], 5, 36, _dialogText)
					.As<Picture>();
				_background.AddLayer(dialog, 80, 8);
			}

			// ── disorder ──────────────────────────────────────────────────────
			if (_disorder = disorder)
			{
				_invadersOrRevolters = new Picture[10];
				for (int ii = 0; ii < 10; ii++)
					_invadersOrRevolters[ii] = NativeAnimFrame(ii, false, false);
				_x = 0;

				string[] lines = { "Civil disorder in", $"{city.Name}! Mayor", "flees in panic." };
				int width = lines.Max(l => Resources.GetTextSize(5, l).Width) + 12;
				var dialog = new Picture(width, 54)
					.Tile(Pattern.PanelGrey, 1, 1)
					.DrawRectangle()
					.DrawRectangle3D(1, 1, width - 2, 52)
					.DrawText(lines[0], 5, 6, _dialogText)
					.DrawText(lines[1], 5, 21, _dialogText)
					.DrawText(lines[2], 5, 36, _dialogText)
					.As<Picture>();
				_background.AddLayer(dialog, 80, 8);
			}

			// ── we love president day ─────────────────────────────────────────
			if (_weLovePresidentDay = weLovePresidentDay)
			{
				_invadersOrRevolters = new Picture[10];
				for (int ii = 0; ii < 10; ii++)
					_invadersOrRevolters[ii] = NativeAnimFrame(ii, false, true);
				_x = 240;

				string[] lines = { "'We Love the President'", "day celebrated in", $"{city.Name}!" };
				int width = lines.Max(l => Resources.GetTextSize(5, l).Width) + 12;
				var dialog = new Picture(width, 54)
					.Tile(Pattern.PanelGrey, 1, 1)
					.DrawRectangle()
					.DrawRectangle3D(1, 1, width - 2, 52)
					.DrawText(lines[0], 5, 6, _dialogText)
					.DrawText(lines[1], 5, 21, _dialogText)
					.DrawText(lines[2], 5, 36, _dialogText)
					.As<Picture>();
				_background.AddLayer(dialog, 80, 8);
			}

			// ── production complete noise wipe ────────────────────────────────
			if (production is not null)
			{
				_noiseMap = new byte[320, 200];
				for (int x = 0; x < 320; x++)
				for (int y = 0; y < 200; y++)
					_noiseMap[x, y] = (byte)Common.Random.Next(1, NOISE_COUNT);

				string[] lines = { $"{_city.Name} builds", $"{(production as ICivilopedia).Name}." };
				int width = lines.Max(l => Resources.GetTextSize(5, l).Width) + 12;
				var dialog = new Picture(width, 39)
					.Tile(Pattern.PanelGrey, 1, 1)
					.DrawRectangle()
					.DrawRectangle3D(1, 1, width - 2, 37)
					.DrawText(lines[0], 5, 6, _dialogText)
					.DrawText(lines[1], 5, 21, _dialogText)
					.As<Picture>();
				foreach (var pic in (Picture[])[_background, _overlay])
					pic.AddLayer(dialog, 80, 10);
				return;
			}

			if (captured) return;

			if (founded)
			{
				_fadeStep = 0.0f;
				FadeColours();
				return;
			}

			this.DrawText(_city.Name, 5, 5,  OX + 161, OY + 3,  TextAlign.Center)
				.DrawText(_city.Name, 5, 15, OX + 160, OY + 2,  TextAlign.Center)
				.DrawText(Game.GameYear, 5, 5,  OX + 161, OY + 16, TextAlign.Center)
				.DrawText(Game.GameYear, 5, 15, OX + 160, OY + 15, TextAlign.Center);

			if (firstView)
			{
				_fadeStep = 0.0f;
				FadeColours();
				return;
			}

			// ── citizens ──────────────────────────────────────────────────────
			int ci = 0;
			int group = -1;
			int offsetX = 24;
			foreach (Citizen citizen in _city.Citizens)
			{
				if (group != (group = Common.CitizenGroup(citizen)) && group > 0) offsetX += 8;
				int dx = (int)citizen + offsetX + (11 * ci++);
				DrawNativeCitizen(0, citizen, dx, 140);
			}
		}
	}
}
