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
using CivOne.Graphics.Sprites;
using CivOne.IO;
using CivOne.Tiles;

namespace CivOne.Screens.CityManagerPanels
{
	internal class CityMap : BaseScreen
	{
		private readonly City _city;

		private bool _update = true;
		private int _tileSize = 16;

		public event EventHandler MapUpdate;

		private void DrawResources(ITile tile, int x, int y)
		{
			int food = _city.FoodValue(tile);
			int shield = _city.ShieldValue(tile);
			int trade = _city.TradeValue(tile);
			int count = food + shield + trade;

			if (count == 0)
			{
				this.AddLayer(Icons.Unhappy, x + 4, y + 4);
				return;
			}

			int iconsPerLine = 2;
			int iconWidth = 8;
			if (count > 4) iconsPerLine = (int)Math.Ceiling((double)count / 2);
			if (iconsPerLine == 3) iconWidth = 4;
			if (iconsPerLine >= 4) iconWidth = 2;

			for (int i = 0; i < count; i++)
			{
				IBitmap icon;
				if (i >= food + shield) icon = Icons.Trade;
				else if (i >= food) icon = Icons.Shield;
				else icon = Icons.Food; 

				int xx = (x + ((i % iconsPerLine) * iconWidth));
				int yy = (y + (((i - (i % iconsPerLine)) / iconsPerLine) * 8));
				this.AddLayer(icon, xx, yy);
			}
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (_update)
			{
				this.Tile(Pattern.PanelBlue)
					.DrawRectangle(colour: 1);

				ITile[,] tiles = _city.CityRadius;
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

				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					ITile tile = tiles[xx, yy];
					if (tile == null) continue;

					int px = (xx * _tileSize) + 1;
					int py = (yy * _tileSize) + 1;

					if (_city.OccupiedTile(tile))
					{
						this.FillRectangle(px, py, _tileSize, 1, 12)
							.FillRectangle(px, py + 1, 1, _tileSize - 2, 12)
							.FillRectangle(px, py + _tileSize - 1, _tileSize, 1, 12)
							.FillRectangle(px + _tileSize - 1, py + 1, 1, _tileSize - 2, 12);
					}

					if (_city.ResourceTiles.Contains(tile))
						DrawResources(tile, px + (_tileSize - 16) / 2, py + (_tileSize - 16) / 2);
				}

				_update = false;
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
			if (MapUpdate != null) MapUpdate(this, null);
			return true;
		}

		public CityMap(City city) : base(82, 82)
		{
			_city = city;
		}
	}
}