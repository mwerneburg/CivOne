#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Screens
{
	[Expand]
	internal class CityUnitMap : BaseScreen
	{
		private readonly City _city;
		private readonly Picture _terrain;
		private bool _dirty = true;

		private int TileW => Math.Max(1, Width  / Map.WIDTH);
		private int TileH => Math.Max(1, Height / Map.HEIGHT);
		private int OX    => (Width  - Map.WIDTH  * TileW) / 2;
		private int OY    => (Height - Map.HEIGHT * TileH) / 2;

		private void BuildTerrain()
		{
			int tw = TileW, th = TileH, ox = OX, oy = OY;
			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				if (!Settings.RevealWorld && !Human.Visible(x, y)) continue;
				ITile tile = Map[x, y];
				Terrain type = tile.Type;
				if (type == Terrain.Grassland2) type = Terrain.Grassland1;
				bool alt = ((x + y) % 2 == 1);
				byte colour = Resources.WorldMapTiles.Bitmap[((int)type) * 4, alt ? 4 : 0];
				_terrain.FillRectangle(ox + x * tw, oy + y * th, tw, th, colour);
			}
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_dirty) return false;
			_dirty = false;

			// Solid background BEFORE the terrain layer: palette index 0 is
			// treated as transparent in SDL, so ocean tiles and unrevealed
			// regions in _terrain would otherwise show the CityManager behind
			// us through to the player (see CityMap.cs:93 for the same gotcha).
			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.AddLayer(_terrain, 0, 0);

			int tw = TileW, th = TileH, ox = OX, oy = OY;
			int dotW = Math.Max(2, tw);
			int dotH = Math.Max(2, th);

			// Supported unit dots
			foreach (IUnit unit in _city.Units)
				this.FillRectangle(ox + unit.X * tw, oy + unit.Y * th, dotW, dotH, CassetteTheme.PHOS);

			// Home city dot (drawn on top so it's always visible)
			this.FillRectangle(ox + _city.X * tw, oy + _city.Y * th, dotW, dotH, CassetteTheme.PHOS_GLOW);

			// Label panel
			int fh = Resources.GetFontHeight(0);
			int units = _city.Units.Length;
			string label = $"{_city.Name.ToUpper()} — {units} SUPPORTED UNIT{(units != 1 ? "S" : "")}";
			int lw = Resources.GetTextSize(0, label).Width + 16;
			int lx = (Width - lw) / 2;
			this.DrawCassettePanel(lx, 4, lw, fh + 8);
			this.DrawText(label, 0, CassetteTheme.PHOS, Width / 2, 8, TextAlign.Center);

			// Dismiss hint
			this.DrawText("ANY KEY OR CLICK TO CLOSE", 0, CassetteTheme.INK_LOW,
				Width / 2, Height - fh - 4, TextAlign.Center);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args) { Destroy(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		public CityUnitMap(City city) : base(MouseCursor.Pointer)
		{
			_city = city;
			Palette = Resources.WorldMapTiles.Palette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				Palette.MergePalette(cassette, 1, 17);
			_terrain = new Picture(Width, Height, Palette);
			BuildTerrain();
		}
	}
}
