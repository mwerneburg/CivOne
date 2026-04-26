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
using CivOne.Tasks;

namespace CivOne.Screens
{
	[Expand, Modal]
	internal class WeLovePresidentDayScreen : BaseScreen
	{
		private readonly City _city;
		private bool _update = true;

		private void GoToCity()
		{
			Show.DropAllWeLovePresidentDay();
			Common.GamePlay?.CenterOnPoint(_city.X, _city.Y);
			Destroy();
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);

			// Panel wide enough for the longest city name headline
			int cityNameLen = Resources.GetTextSize(1, $"DAY CELEBRATED IN {_city.Name.ToUpper()}!").Width;
			int panelW = Math.Max(300, Math.Min(Width - 20, cityNameLen + 32));
			int panelH = 100;
			int px = (Width  - panelW) / 2;
			int py = (Height - panelH) / 2;

			this.DrawCassettePanel(px, py, panelW, panelH, "WE LOVE THE PRESIDENT");

			int fh0 = Resources.GetFontHeight(0);
			int fh1 = Resources.GetFontHeight(1);

			// City name + celebration line
			int textY = py + 12;
			string cityLine = $"DAY CELEBRATED IN {_city.Name.ToUpper()}!";
			this.DrawText(cityLine, 1, CassetteTheme.PHOS_GLOW, px + panelW / 2, textY, TextAlign.Center);
			textY += fh1 + 4;

			// Skyline
			int skyW = panelW - 16;
			int skyX = px + 8;
			int skyY = textY + 10;
			int skyH = 18;
			int size = Math.Max(1, (int)_city.Size);
			int numBuildings = size * 2;
			int colW = Math.Max(4, (skyW - numBuildings) / numBuildings);
			int[] heights = { 6, 12, 8, 14, 7, 16, 9, 13, 5, 15, 10, 11 };

			for (int i = 0; i < numBuildings; i++)
			{
				int h = heights[i % heights.Length];
				int bx = skyX + i * (colW + 1);
				int by = skyY + skyH - h;
				byte col = (byte)((i % 3 == 0) ? CassetteTheme.PHOS : CassetteTheme.PHOS_DIM);
				this.FillRectangle(bx, by, colW, h, col);

				// Door at base of building (center, 2 wide, 3 tall)
				if (colW >= 5 && h >= 5)
				{
					int doorW = 2;
					int doorX = bx + (colW - doorW) / 2;
					this.FillRectangle(doorX, by + h - 3, doorW, 3, CassetteTheme.BG0);
				}

				// Windows (rows of 2px squares)
				if (colW >= 6 && h >= 8)
				{
					for (int wy = by + 2; wy + 2 < by + h - 3; wy += 4)
					{
						int winX = bx + 1;
						// left window
						this.FillRectangle(winX, wy, 2, 2, CassetteTheme.PHOS_GLOW);
						// right window if wide enough
						if (colW >= 8)
							this.FillRectangle(winX + colW - 4, wy, 2, 2, CassetteTheme.PHOS_GLOW);
					}
				}
			}

			// Ground line
			this.FillRectangle(skyX, skyY + skyH, skyW, 1, CassetteTheme.BORDER);

			// Footer
			int footerY = py + panelH - fh0 - 5;
			this.DrawText("G - GO TO CITY   ANY KEY - DISMISS", 0, CassetteTheme.INK_LOW,
				px + panelW / 2, footerY, TextAlign.Center);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (Char.ToUpper(args.KeyChar) == 'G')
				GoToCity();
			else
				Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		public WeLovePresidentDayScreen(City city) : base(MouseCursor.Pointer)
		{
			_city = city;

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
