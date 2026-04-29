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
	internal class ConfirmBuy : BaseScreen
	{
		private bool _update = true;

		public event EventHandler Buy;

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			const int pw = 200, ph = 84;
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
			this.DrawText("BUY NOW?", 0, CassetteTheme.PHOS, px + pw / 2, py + 4, TextAlign.Center);

			int fh = Resources.GetFontHeight(0);
			this.DrawText(_line1, 0, CassetteTheme.INK_MID,  px + pw / 2, py + 20, TextAlign.Center);
			this.DrawText(_line2, 0, CassetteTheme.INK_HIGH, px + pw / 2, py + 20 + fh + 2, TextAlign.Center);
			this.DrawText(_line3, 0, CassetteTheme.INK_MID,  px + pw / 2, py + 20 + (fh + 2) * 2, TextAlign.Center);

			this.DrawText("Y / ENTER - BUY", 0, CassetteTheme.PHOS_GLOW,
				px + 5, py + ph - fh * 2 - 10, TextAlign.Left);
				// px + pw / 2, py + ph - fh * 2 - 10, TextAlign.Center);
			this.DrawText("ESC / N - CANCEL", 0, CassetteTheme.INK_MID,
				px + 5, py + ph - fh - 6, TextAlign.Left);
				// px + pw / 2, py + ph - fh - 6, TextAlign.Center);

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
				Buy?.Invoke(this, EventArgs.Empty);
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

		private readonly string _line1, _line2, _line3;

		public ConfirmBuy(string name, short price, short treasury) : base(MouseCursor.Pointer)
		{
			_line1 = "Cost to complete";
			_line2 = $"{name}: ${price}";
			_line3 = $"Treasury: ${treasury}";

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
