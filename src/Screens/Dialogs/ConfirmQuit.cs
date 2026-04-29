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

namespace CivOne.Screens.Dialogs
{
	[OwnPalette]
	internal class ConfirmQuit : BaseScreen
	{
		private bool _update = true;

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			const int pw = 200, ph = 68; // dimensions of the text box (I think)
			const int px = (320 - pw) / 2;
			const int py = (200 - ph) / 2;

			this.FillRectangle(px, py, pw, ph, CassetteTheme.BG1);
			this.FillRectangle(px,          py,          pw, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,          py + ph - 1, pw, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,          py,          1, ph, CassetteTheme.BORDER);
			this.FillRectangle(px + pw - 1, py,          1, ph, CassetteTheme.BORDER);

			// Title band
			this.FillRectangle(px + 1, py + 1, pw - 2, 14, CassetteTheme.BG3);
			this.FillRectangle(px + 1, py + 14, pw - 2, 1, CassetteTheme.BORDER);
			this.DrawText("QUIT GAME", 0, CassetteTheme.WARN, px + pw / 2, py + 4, TextAlign.Center);

			int fh = Resources.GetFontHeight(0);
			this.DrawText("Are you sure you want to quit?", 0, CassetteTheme.INK_HIGH,
				px + 5, py + 22, TextAlign.Left);
				// px + pw / 2, py + 22, TextAlign.Left);

			this.DrawText("Y / ENTER - QUIT", 0, CassetteTheme.ALERT,
				px + 5, py + ph - fh * 2 - 10, TextAlign.Left);
				// px + pw / 2, py + ph - fh * 2 - 10, TextAlign.Left);
			this.DrawText("ESC / N - KEEP PLAYING", 0, CassetteTheme.INK_MID,
				px + 5, py + ph - fh - 6, TextAlign.Left);
				// px + pw / 2, py + ph - fh - 6, TextAlign.Left);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (args.Key == Key.Escape || Char.ToUpper(args.KeyChar) == 'N')
			{
				Destroy();
				return true;
			}
			if (Char.ToUpper(args.KeyChar) == 'Y' || args.Key == Key.Enter)
			{
				Runtime.Quit();
				Destroy();
				return true;
			}
			return false;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		public ConfirmQuit() : base(MouseCursor.Pointer)
		{
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
