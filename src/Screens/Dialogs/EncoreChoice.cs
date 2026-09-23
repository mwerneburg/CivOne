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
	// Play on, or retire? Asked after a peaceful win while the visitors are still on their
	// way, and again when they land. The win is already banked (Game.BankedVictory) before
	// this appears, so neither answer can lose it. ConfirmRetire's layout and keys.
	[OwnPalette]
	internal class EncoreChoice : BaseScreen
	{
		private readonly string _title;
		private readonly string[] _lines;
		private readonly Action _retire;
		private bool _update = true;

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			int fh = Resources.GetFontHeight(0);
			int pw = 240, ph = 30 + fh * (_lines.Length + 2) + 8;
			int px = (320 - pw) / 2, py = (200 - ph) / 2;

			this.FillRectangle(px,          py,          pw, ph, CassetteTheme.BG1);
			this.FillRectangle(px,          py,          pw, 1,  CassetteTheme.BORDER);
			this.FillRectangle(px,          py + ph - 1, pw, 1,  CassetteTheme.BORDER);
			this.FillRectangle(px,          py,          1, ph,  CassetteTheme.BORDER);
			this.FillRectangle(px + pw - 1, py,          1, ph,  CassetteTheme.BORDER);

			this.FillRectangle(px + 1, py + 1,  pw - 2, 14, CassetteTheme.BG3);
			this.FillRectangle(px + 1, py + 14, pw - 2, 1,  CassetteTheme.BORDER);
			this.DrawText(_title, 0, CassetteTheme.PHOS, px + pw / 2, py + 4, TextAlign.Center);

			for (int i = 0; i < _lines.Length; i++)
				this.DrawText(_lines[i], 0, CassetteTheme.INK_HIGH, px + 5, py + 22 + i * fh, TextAlign.Left);

			this.DrawText("Y / ENTER - PLAY ON", 0, CassetteTheme.OK,
				px + 5, py + ph - fh * 2 - 10, TextAlign.Left);
			this.DrawText("N / ESC - RETIRE", 0, CassetteTheme.INK_MID,
				px + 5, py + ph - fh - 6, TextAlign.Left);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (Char.ToUpper(args.KeyChar) == 'Y' || args.Key == Key.Enter)
			{
				Destroy();
				return true;
			}
			if (args.Key == Key.Escape || Char.ToUpper(args.KeyChar) == 'N')
			{
				Destroy();
				_retire();
				return true;
			}
			return false;
		}

		internal EncoreChoice(string title, string[] lines, Action retire) : base(MouseCursor.Pointer)
		{
			_title = title;
			_lines = lines;
			_retire = retire;
			using Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
