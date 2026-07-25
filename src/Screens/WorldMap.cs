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
			using (Palette cassette = CassetteTheme.CreatePalette())
				Palette.MergePalette(cassette, 1, 18);
			this.Clear(CassetteTheme.BG0);

			int px = Math.Max(1, Math.Min(Width / Map.WIDTH, Height / Map.HEIGHT));
			int ox = (Width - Map.WIDTH * px) / 2;
			int oy = (Height - Map.HEIGHT * px) / 2;

			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				if (!Settings.RevealWorld && !Human.Visible(x, y)) continue;

				ITile tile = Map[x, y];
				int dx = ox + x * px;
				int dy = oy + y * px;
				this.FillRectangle(dx, dy, px, px, MiniMap.TerrainColour(tile));

				City city = tile.City;
				if (city is not null && city.Size > 0)
				{
					this.FillRectangle(dx, dy, px, px, Common.ColourLight[city.Owner]);
				}
				else
				{
					IUnit[] units = tile.Units;
					if (units.Length > 0)
					{
						int iS = Math.Max(1, px - 1);
						this.FillRectangle(dx + 1, dy + 1, iS, iS, CassetteTheme.BORDER)
							.FillRectangle(dx, dy, iS, iS, Common.ColourLight[units[0].Owner]);
					}
				}
			}
		}
	}
}
