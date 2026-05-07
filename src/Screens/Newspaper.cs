// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens
{
	[Expand]
	internal class Newspaper : BaseScreen
	{
		private bool _update = true;

		private readonly string[] _message;
		private readonly string _date;

		private void Render()
		{
			int fh = Resources.GetFontHeight(0);
			int lh = fh + 2;
			const string hint = "ANY KEY OR CLICK TO CLOSE";

			// Measure content width
			int contentW = Resources.GetTextSize(0, hint).Width;
			contentW = System.Math.Max(contentW, Resources.GetTextSize(0, _date).Width);
			foreach (string s in _message)
				contentW = System.Math.Max(contentW, Resources.GetTextSize(0, s).Width);
			contentW = System.Math.Max(contentW, 160);

			int pw = contentW + 24;
			int ph = 2                          // top/bottom borders (drawn by DrawCassettePanel)
			       + 6 + lh + 3                 // top pad + date + gap
			       + 1 + 3                       // divider + gap
			       + _message.Length * lh        // message lines
			       + 3 + 1 + 3                   // gap + divider + gap
			       + fh + 6;                     // hint + bottom pad

			int ox = (Width  - pw) / 2;
			int oy = (Height - ph) / 2;

			this.Clear(CassetteTheme.BG0);
			this.DrawCassettePanel(ox, oy, pw, ph, "NEWS");
			this.AddScanlines(ox + 1, oy + 1, pw - 2, ph - 2);

			int tx = ox + 12;
			int ty = oy + 6;

			this.DrawText(_date, 0, CassetteTheme.INK_LOW, tx, ty);
			ty += lh + 3;
			this.DrawCassetteDivider(tx, ty, pw - 24);
			ty += 4;

			foreach (string line in _message)
			{
				this.DrawText(line, 0, CassetteTheme.INK_HIGH, tx, ty);
				ty += lh;
			}

			ty += 2;
			this.DrawCassetteDivider(tx, ty, pw - 24);
			ty += 4;
			this.DrawText(hint, 0, CassetteTheme.INK_LOW, ox + pw / 2, ty, TextAlign.Center);
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (_update) { _update = false; return true; }
			return false;
		}

		public void Close() => Destroy();
		public override bool KeyDown(KeyboardEventArgs args) { Close(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Close(); return true; }

		public Newspaper(City city, string[] message, bool showGovernment = false)
		{
			_message = message;
			_date    = $"January 1, {Common.YearString(Game.GameTurn)}";

			using (Palette cassette = CassetteTheme.CreatePalette())
			{
				Palette = Common.DefaultPalette;
				Palette.MergePalette(cassette, 1, 17);
			}

			Render();
			OnResize += (s, a) => { Render(); _update = true; };
		}
	}
}
