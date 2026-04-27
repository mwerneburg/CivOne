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

namespace CivOne.Screens
{
	[Expand, OwnPalette]
	internal class CityName : BaseScreen
	{
		private Input _input;

		public int NameId { get; private set; }
		public string Value { get; private set; }

		public event EventHandler Accept, Cancel;

		private readonly string _initialName;

		private void CityName_Accept(object sender, EventArgs args)
		{
			Value = (sender as Input).Text;
			Accept?.Invoke(this, null);
			((Input)sender)?.Close();
			Destroy();
		}

		private void CityName_Cancel(object sender, EventArgs args)
		{
			Cancel?.Invoke(this, null);
			((Input)sender)?.Close();
			Destroy();
		}

		protected override bool HasUpdate(uint gameTick)
		{
			this.FillRectangle(0, 0, Width, Height, 0);

			const int pw = 200, ph = 44;
			int px = (Width  - pw) / 2;
			int py = (Height - ph) / 2;

			// Panel fill + border
			this.FillRectangle(px, py, pw, ph, CassetteTheme.BG1);
			this.FillRectangle(px,          py,          pw, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,          py + ph - 1, pw, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,          py,          1,  ph, CassetteTheme.BORDER);
			this.FillRectangle(px + pw - 1, py,          1,  ph, CassetteTheme.BORDER);

			// Title label straddling the top border
			int fh = Resources.GetFontHeight(0);
			string label = " CITY NAME ";
			int lw = Resources.GetTextSize(0, label).Width;
			this.FillRectangle(px + 8, py, lw, 1, CassetteTheme.BG0);
			this.DrawText(label, 0, CassetteTheme.PHOS, px + 8, py - fh / 2);

			// Input well
			int ix = px + 8;
			int iy = py + 14;
			int iw = pw - 16;
			int ih = fh + 6;
			this.FillRectangle(ix,          iy,          iw, ih, CassetteTheme.BG3);
			this.FillRectangle(ix,          iy,          iw, 1,  CassetteTheme.BORDER);
			this.FillRectangle(ix,          iy + ih - 1, iw, 1,  CassetteTheme.BORDER);
			this.FillRectangle(ix,          iy,          1,  ih, CassetteTheme.BORDER);
			this.FillRectangle(ix + iw - 1, iy,          1,  ih, CassetteTheme.BORDER);

			// Hint
			this.DrawText("ENTER to confirm  ESC to cancel", 0, CassetteTheme.INK_LOW,
				px + pw / 2, py + ph - fh - 4, TextAlign.Center);

			// Create or reposition the Input
			if (_input == null)
			{
				_input = new Input(Palette, _initialName, 0,
					CassetteTheme.INK_HIGH, CassetteTheme.PHOS_FAINT,
					ix + 3, iy + 3, iw - 6, fh, 12);
				_input.Accept += CityName_Accept;
				_input.Cancel += CityName_Cancel;
			}
			else
			{
				_input.X = ix + 3;
				_input.Y = iy + 3;
			}

			if (!Common.HasScreenType<Input>())
				Common.AddScreen(_input);

			return true;
		}

		public CityName(int nameId, string cityName)
		{
			NameId = nameId;
			_initialName = cityName;

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
