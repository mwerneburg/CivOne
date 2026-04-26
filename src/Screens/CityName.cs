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
using CivOne.Graphics;

namespace CivOne.Screens
{
	[OwnPalette]
	internal class CityName : BaseScreen
	{
		private readonly Input _input;

		public int NameId { get; private set; }

		public string Value { get; private set; }

		public event EventHandler Accept, Cancel;

		private void CityName_Accept(object sender, EventArgs args)
		{
			Value = (sender as Input).Text;
			if (Accept != null)
				Accept(this, null);
			if (sender is Input)
				((Input)sender)?.Close();
			Destroy();
		}

		private void CityName_Cancel(object sender, EventArgs args)
		{
			if (Cancel != null)
				Cancel(this, null);
			if (sender is Input)
				((Input)sender)?.Close();
			Destroy();
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!Common.HasScreenType<Input>())
			{
				Common.AddScreen(_input);
			}
			return false;
		}

		public CityName(int nameId, string cityName)
		{
			NameId = nameId;

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			// Centered 200×44 Cassette panel
			const int pw = 200, ph = 44;
			int px = (320 - pw) / 2;
			int py = (200 - ph) / 2;

			// Panel fill + border
			this.FillRectangle(px, py, pw, ph, CassetteTheme.BG1);
			this.FillRectangle(px,          py,          pw, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,          py + ph - 1, pw, 1, CassetteTheme.BORDER);
			this.FillRectangle(px,          py,          1,  ph, CassetteTheme.BORDER);
			this.FillRectangle(px + pw - 1, py,          1,  ph, CassetteTheme.BORDER);

			// Title label straddling the top border (Cassette motif)
			int fh = Resources.GetFontHeight(0);
			string label = " CITY NAME ";
			int lw = Resources.GetTextSize(0, label).Width;
			this.FillRectangle(px + 8, py, lw, 1, CassetteTheme.BG0);
			this.DrawText(label, 0, CassetteTheme.PHOS, px + 8, py - fh / 2);

			// Input field box
			int ix = px + 8;
			int iy = py + 14;
			int iw = pw - 16;
			int ih = fh + 6;
			this.FillRectangle(ix,          iy,          iw, ih, CassetteTheme.BG3);
			this.FillRectangle(ix,          iy,          iw, 1,  CassetteTheme.BORDER);
			this.FillRectangle(ix,          iy + ih - 1, iw, 1,  CassetteTheme.BORDER);
			this.FillRectangle(ix,          iy,          1,  ih, CassetteTheme.BORDER);
			this.FillRectangle(ix + iw - 1, iy,          1,  ih, CassetteTheme.BORDER);

			// Hint below
			this.DrawText("ENTER to confirm  ESC to cancel", 0, CassetteTheme.INK_LOW,
				px + pw / 2, py + ph - fh - 4, TextAlign.Center);

			_input = new Input(Palette, cityName, 0,
				CassetteTheme.INK_HIGH, CassetteTheme.PHOS_FAINT,
				ix + 3, iy + 3, iw - 6, fh, 12);
			_input.Accept += CityName_Accept;
			_input.Cancel += CityName_Cancel;
		}
	}
}
