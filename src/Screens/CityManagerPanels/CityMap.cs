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
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;
using CivOne.Tiles;

namespace CivOne.Screens.CityManagerPanels
{
	internal class CityMap : BaseScreen
	{
		private readonly City _city;

		private bool _update = true;
		private int _tileSize = 16;

		public event EventHandler? MapUpdate;

		// Lay the yield icons (food/shield/trade) out in a centred, non-overlapping
		// grid, scaled up to fill the tile — tiles are much larger than the icons'
		// native 8px, so a fixed 8px block left them cramped and overlapping.
		private void DrawResources(ITile tile, int tx, int ty)
		{
			int food = _city.FoodValue(tile);
			int shield = _city.ShieldValue(tile);
			int trade = _city.TradeValue(tile);
			int count = food + shield + trade;
			int size = _tileSize;

			if (count == 0)
			{
				int us = IconScale(1, 1, size);
				DrawIcon(Icons.Unhappy, tx + (size - 8 * us) / 2, ty + (size - 8 * us) / 2, us);
				return;
			}

			int cols = count <= 3 ? count : (int)Math.Ceiling(Math.Sqrt(count));
			int rows = (count + cols - 1) / cols;
			int scale = IconScale(cols, rows, size);
			int icon = 8 * scale, gap = scale;
			int gw = cols * icon + (cols - 1) * gap;
			int gh = rows * icon + (rows - 1) * gap;
			int ox = tx + (size - gw) / 2;
			int oy = ty + (size - gh) / 2;

			for (int i = 0; i < count; i++)
			{
				IBitmap art = (i >= food + shield) ? Icons.Trade : (i >= food) ? Icons.Shield : Icons.Food;
				int c = i % cols, r = i / cols;
				DrawIcon(art, ox + c * (icon + gap), oy + r * (icon + gap), scale);
			}
		}

		// Largest integer icon scale (1-4) that fits a cols×rows grid of 8px icons
		// (plus 1px-per-scale gaps) inside a tile, leaving a small margin.
		private static int IconScale(int cols, int rows, int size)
		{
			int avail = size - 2;
			return Math.Max(1, Math.Min(4, Math.Min(avail / (cols * 9), avail / (rows * 9))));
		}

		private void DrawIcon(IBitmap icon, int x, int y, int scale)
		{
			if (scale <= 1) { this.AddLayer(icon, x, y); return; }
			using (Bytemap b = icon.Bitmap.Scale(scale))
				this.AddLayer(b, x, y);
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			// Cassette dark background with CRT scanline effect visible in corner dead-zones
			int bw = Bitmap.Width, bh = Bitmap.Height;
			this.FillRectangle(0, 0, bw, bh, CassetteTheme.BG0);
			for (int scanY = 0; scanY < bh; scanY += 2)
				this.FillRectangle(0, scanY, bw, 1, CassetteTheme.BG1);
			this.FillRectangle(0,      0,      bw, 1, CassetteTheme.BORDER);
			this.FillRectangle(0,      bh - 1, bw, 1, CassetteTheme.BORDER);
			this.FillRectangle(0,      0,      1,  bh, CassetteTheme.BORDER);
			this.FillRectangle(bw - 1, 0,      1,  bh, CassetteTheme.BORDER);

			ITile[,] tiles = _city.CityRadius;
			// Snapshot ResourceTiles once to avoid repeated CityRadius allocations per tile check
			var resourceSet = new System.Collections.Generic.HashSet<ITile>(_city.ResourceTiles);
			int scale = _tileSize / 16;
			using (IBitmap rawMap = tiles.ToBitmap(TileSettings.CityManager, Settings.RevealWorld ? null : Game.GetPlayer(_city.Owner)))
			{
				if (scale > 1)
				{
					using (Bytemap scaled = rawMap.Bitmap.Scale(scale))
						this.AddLayer(scaled, 1, 1);
				}
				else
				{
					this.AddLayer(rawMap, 1, 1, dispose: true);
				}
			}

			// Palette index 0 is transparent in SDL; fill unexplored/null tile slots with a
			// solid dark colour so they never bleed through to the background game map.
			if (!Settings.RevealWorld)
			{
				Player owner = Game.GetPlayer(_city.Owner);
				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					ITile tile = tiles[xx, yy];
					if (tile is not null && owner is not null && owner.Visible(tile)) continue;
					this.FillRectangle(1 + xx * _tileSize, 1 + yy * _tileSize, _tileSize, _tileSize, CassetteTheme.BG0);
				}
			}

			for (int xx = 0; xx < 5; xx++)
			for (int yy = 0; yy < 5; yy++)
			{
				ITile tile = tiles[xx, yy];
				if (tile is null) continue;

				int px = (xx * _tileSize) + 1;
				int py = (yy * _tileSize) + 1;

				if (_city.OccupiedTile(tile))
				{
					this.FillRectangle(px, py, _tileSize, 1, 12)
						.FillRectangle(px, py + 1, 1, _tileSize - 2, 12)
						.FillRectangle(px, py + _tileSize - 1, _tileSize, 1, 12)
						.FillRectangle(px + _tileSize - 1, py + 1, 1, _tileSize - 2, 12);
				}

				if (resourceSet.Contains(tile))
					DrawResources(tile, px, py);
			}

			return true;
		}

		public void Update()
		{
			_update = true;
		}
		
		public void Resize(int size)
		{
			_tileSize = (size - 2) / 5;
			Bitmap = new Bytemap(size, size);
			_update = true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			int mapEdge = 1 + 5 * _tileSize;
			if (args.X < 1 || args.X > mapEdge || args.Y < 1 || args.Y > mapEdge) return false;
			int tileX = (int)Math.Floor(((double)args.X - 1) / _tileSize);
			int tileY = (int)Math.Floor(((double)args.Y - 1) / _tileSize);

			if (tileX < 0 || tileY < 0 || tileX > 4 || tileY > 4) return false;

			_city.SetResourceTile(_city.CityRadius[tileX, tileY]);
			_update = true;
			if (MapUpdate is not null) MapUpdate(this, null);
			return true;
		}

		public CityMap(City city) : base(82, 82)
		{
			_city = city;
		}
	}
}