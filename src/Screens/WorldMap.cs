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
	internal class WorldMap : BaseScreen
	{
		private bool _update = true;

		protected override bool HasUpdate(uint gameTick)
		{
			if (_update) return false;
			_update = false;
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		public WorldMap()
		{
			Palette = Resources.WorldMapTiles.Palette;
			this.Clear(5);

			int tileW = Math.Max(1, Width / Map.WIDTH);
			int tileH = Math.Max(1, Height / Map.HEIGHT);
			int ox = (Width - Map.WIDTH * tileW) / 2;
			int oy = (Height - Map.HEIGHT * tileH) / 2;

			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				if (!Settings.RevealWorld && !Human.Visible(x, y)) continue;

				ITile tile = Map[x, y];
				Terrain type = tile.Type;
				if (type == Terrain.Grassland2) type = Terrain.Grassland1;
				bool altTile = ((x + y) % 2 == 1);
				int tx = ((int)type) * 4;
				int ty = altTile ? 4 : 0;
				byte colour = Resources.WorldMapTiles.Bitmap[tx, ty];

				int dx = ox + x * tileW;
				int dy = oy + y * tileH;
				this.FillRectangle(dx, dy, tileW, tileH, colour);

				City city = tile.City;
				if (city != null && city.Size > 0)
				{
					this.FillRectangle(dx, dy, tileW, tileH, Common.ColourLight[city.Owner]);
				}
				else
				{
					IUnit[] units = tile.Units;
					if (units.Length > 0)
					{
						int iW = Math.Max(1, tileW - 1);
						int iH = Math.Max(1, tileH - 1);
						this.FillRectangle(dx + 1, dy + 1, iW, iH, 5)
							.FillRectangle(dx, dy, iW, iH, Common.ColourLight[units[0].Owner]);
					}
				}
			}
		}
	}
}
