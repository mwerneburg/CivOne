// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens
{
	[OwnPalette]
	internal class PopupMessage : BaseScreen
	{
		private bool _update = true;

		protected override bool HasUpdate(uint gameTick)
		{
			if (_update)
			{
				_update = false;
				return true;
			}
			return false;
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

		public PopupMessage(byte colour, string title, string[] message) : base(MouseCursor.Pointer)
		{
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			int lineHeight = Resources.GetFontHeight(0);
			int lineCount  = message.Length + (title != null ? 1 : 0);
			int innerW     = 207;
			int innerH     = lineCount * lineHeight + 8;
			int px         = 57;
			int py         = 16;

			// Panel
			this.FillRectangle(px, py, innerW, innerH, CassetteTheme.BG1);
			this.FillRectangle(px,             py,             innerW, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,             py + innerH - 1, innerW, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,             py,             1, innerH, CassetteTheme.BORDER);
			this.FillRectangle(px + innerW - 1, py,            1, innerH, CassetteTheme.BORDER);

			// Alert stripe (top band colored by severity)
			byte stripe = (colour == 4) ? CassetteTheme.ALERT : CassetteTheme.PHOS_FAINT;
			this.FillRectangle(px + 1, py + 1, innerW - 2, lineHeight + 3, stripe);

			int yy = py + 4;
			if (title != null)
			{
				this.DrawText(title, 0, CassetteTheme.INK_HIGH, px + innerW / 2, yy, TextAlign.Center);
				yy += lineHeight;
			}
			for (int i = 0; i < message.Length; i++)
				this.DrawText(message[i], 0, CassetteTheme.INK_MID, px + 8, yy + i * lineHeight);
		}
	}
}
