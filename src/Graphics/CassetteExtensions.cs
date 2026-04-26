// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Drawing;
using CivOne.Enums;
using CivOne.IO;
using static CivOne.Enums.TextAlign;

namespace CivOne.Graphics
{
	internal static class CassetteExtensions
	{
		private static Resources Resources => Resources.Instance;

		// Fill entire bitmap with the deep background color.
		public static IBitmap CassetteBackground(this IBitmap bitmap)
			=> bitmap.FillRectangle(0, 0, bitmap.Width(), bitmap.Height(), CassetteTheme.BG0);

		// Panel with BG1 fill, 1px BORDER outline.
		// If title is provided, it is rendered in PHOS straddling the top border —
		// the classic "label in the frame" motif from the prototype.
		public static IBitmap DrawCassettePanel(this IBitmap bitmap, int x, int y, int w, int h,
			string title = null, int font = 0)
		{
			bitmap.FillRectangle(x + 1, y + 1, w - 2, h - 2, CassetteTheme.BG1);
			bitmap.FillRectangle(x,         y,         w, 1, CassetteTheme.BORDER);
			bitmap.FillRectangle(x,         y + h - 1, w, 1, CassetteTheme.BORDER);
			bitmap.FillRectangle(x,         y,         1, h, CassetteTheme.BORDER);
			bitmap.FillRectangle(x + w - 1, y,         1, h, CassetteTheme.BORDER);

			if (string.IsNullOrEmpty(title)) return bitmap;

			int fh = Resources.GetFontHeight(font);
			string label = " " + title.ToUpper() + " ";
			Size ts = Resources.GetTextSize(font, label);

			// Punch a gap in the top border behind the title, then draw it centered on the line.
			bitmap.FillRectangle(x + 8, y, ts.Width, 1, CassetteTheme.BG0);
			bitmap.DrawText(label, font, CassetteTheme.PHOS, x + 8, y - fh / 2);

			return bitmap;
		}

		// 1px horizontal rule in BORDER color.
		public static IBitmap DrawCassetteDivider(this IBitmap bitmap, int x, int y, int w)
			=> bitmap.FillRectangle(x, y, w, 1, CassetteTheme.BORDER);

		// Label (INK_MID, left) + value (valueColor, right) on one row, with a 1px
		// divider below. Use this for the "FIELD · VALUE" data rows.
		public static IBitmap DrawCassetteField(this IBitmap bitmap, string label, string value,
			int x, int y, int w, int font = 0, byte valueColor = CassetteTheme.INK_HIGH)
		{
			int fh = Resources.GetFontHeight(font);
			bitmap.DrawText(label.ToUpper(), font, CassetteTheme.INK_MID, x, y);
			bitmap.DrawText(value.ToUpper(), font, valueColor, x + w, y, Right);
			bitmap.DrawCassetteDivider(x, y + fh + 1, w);
			return bitmap;
		}

		// Map a Citizen enum value to a Cassette palette color.
		public static byte CitizenTokenColor(Citizen citizen)
		{
			switch (citizen)
			{
				case Citizen.HappyMale:
				case Citizen.HappyFemale:   return CassetteTheme.PHOS_GLOW;
				case Citizen.ContentMale:
				case Citizen.ContentFemale: return CassetteTheme.INK_MID;
				case Citizen.UnhappyMale:
				case Citizen.UnhappyFemale: return CassetteTheme.ALERT;
				case Citizen.Entertainer:   return CassetteTheme.PHOS;
				case Citizen.Taxman:        return CassetteTheme.PHOS_DIM;
				case Citizen.Scientist:     return CassetteTheme.CYAN;
				default:                    return CassetteTheme.INK_LOW;
			}
		}

		// Draw a single citizen token in a slotW×slotH slot (default 8×16).
		// Scales the filled rect proportionally within the slot.
		// Uses only Cassette palette indices so all citizen types look
		// consistent regardless of where the original SP257 sprite pixels landed.
		public static IBitmap DrawCitizenToken(this IBitmap bitmap, Citizen citizen, int x, int y,
			int slotW = 8, int slotH = 16)
		{
			byte fill  = CitizenTokenColor(citizen);
			int tokenW = slotW - 2;   // 1px margin each side
			int tokenH = slotH - 4;   // 2px margin top + bottom
			int tx     = x + 1;
			int ty     = y + 2;
			bitmap.FillRectangle(tx, ty, tokenW, tokenH, fill);
			// thin dark frame (overwrites outer pixels of the fill)
			bitmap.FillRectangle(tx,            ty,                tokenW, 1, CassetteTheme.BG0);
			bitmap.FillRectangle(tx,            ty + tokenH - 1,  tokenW, 1, CassetteTheme.BG0);
			bitmap.FillRectangle(tx,            ty,            1, tokenH,    CassetteTheme.BG0);
			bitmap.FillRectangle(tx + tokenW - 1, ty,          1, tokenH,    CassetteTheme.BG0);
			return bitmap;
		}

		// Segmented horizontal progress bar with a label above.
		// Each filled segment gets a bright PHOS top stripe over a PHOS_DIM body.
		public static IBitmap DrawCassetteMeter(this IBitmap bitmap, string label, int value, int max,
			int x, int y, int w, int font = 0)
		{
			int fh = Resources.GetFontHeight(font);
			bitmap.DrawText(label.ToUpper(), font, CassetteTheme.INK_MID, x, y);

			int segments = Math.Min(max, w / 6);
			if (segments < 1) return bitmap;
			int filled  = (max > 0) ? (value * segments / max) : 0;
			int segW    = Math.Max(1, (w - segments + 1) / segments);
			int barY    = y + fh + 2;

			for (int i = 0; i < segments; i++)
			{
				int sx  = x + i * (segW + 1);
				byte bg = (i < filled) ? CassetteTheme.PHOS_DIM : CassetteTheme.BG2;
				bitmap.FillRectangle(sx, barY,     segW, 3, bg);
				if (i < filled)
					bitmap.FillRectangle(sx, barY, segW, 1, CassetteTheme.PHOS);
			}
			return bitmap;
		}
	}
}
