// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens
{
	[Expand, Modal]
	internal class SouthPoleIntelReport : BaseScreen
	{
		private const int FONT_ID = 0;
		private const int PAD     = 10;

		private readonly string[] _lines;
		private bool _dirty = true;

		private void Redraw()
		{
			int fh = Resources.GetFontHeight(FONT_ID);

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawRectangle(2, 2, Width - 4, Height - 4, CassetteTheme.BORDER);

			int y = PAD;
			for (int i = 0; i < _lines.Length; i++)
			{
				if (y + fh >= Height - PAD) break;

				string text = _lines[i];
				byte color;
				if (i == 0)
					color = CassetteTheme.ALERT;
				else if (i == 1)
					color = CassetteTheme.PHOS_DIM;
				else if (string.IsNullOrEmpty(text))
					color = CassetteTheme.BG0;
				else
					color = CassetteTheme.INK_MID;

				this.DrawText(text, FONT_ID, color, PAD + 4, y);
				y += fh;
			}

			this.DrawText("[ ANY KEY OR CLICK TO DISMISS ]", FONT_ID, CassetteTheme.INK_LOW,
			              Width / 2, Height - PAD + 1, TextAlign.Center);

			_dirty = false;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_dirty) return false;
			Redraw();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args) { Destroy(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		public SouthPoleIntelReport(string gameYear)
		{
			string[] intelLines = SouthPoleExpeditionLog.LoadIntelLines()
				?? new[]
				{
					"Satellite imagery has revealed an anomalous formation at the South Pole.",
					"Norwegian scientists confirm the structure is of non-terrestrial origin.",
					"A classified expedition has been dispatched. Details: EYES ONLY."
				};

			var lines = new List<string>();
			lines.Add("CLASSIFIED INTELLIGENCE REPORT");
			lines.Add($"TRANSMISSION TIMESTAMP: {gameYear}");
			lines.Add("");
			foreach (string line in intelLines)
				lines.Add(line.Replace("{game year}", gameYear));
			_lines = lines.ToArray();

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
