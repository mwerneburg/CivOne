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
			string? title = null, int font = 0)
		{
			bitmap.FillRectangle(x + 1, y + 1, w - 2, h - 2, CassetteTheme.BG1);
			bitmap.FillRectangle(x,         y,         w, 1, CassetteTheme.BORDER);
			bitmap.FillRectangle(x,         y + h - 1, w, 1, CassetteTheme.BORDER);
			bitmap.FillRectangle(x,         y,         1, h, CassetteTheme.BORDER);
			bitmap.FillRectangle(x + w - 1, y,         1, h, CassetteTheme.BORDER);

			if (string.IsNullOrEmpty(title)) return bitmap;

			int fh = Resources.GetFontHeight(font);
			string label = " " + title!.ToUpper() + " ";
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

		// Overlay dark horizontal stripes on every odd row — simulates CRT scanlines.
		// Call this last, after all content has been drawn into the bitmap.
		public static IBitmap AddScanlines(this IBitmap bitmap, int x = 0, int y = 0, int w = -1, int h = -1)
		{
			int bw = (w < 0) ? bitmap.Width() : w;
			int bh = (h < 0) ? bitmap.Height() : h;
			for (int row = y + 1; row < y + bh; row += 2)
				bitmap.FillRectangle(x, row, bw, 1, CassetteTheme.BG0);
			return bitmap;
		}

		// Map a Citizen enum value to its body color.
		// Mood is the primary axis (glow/mid/alert); specialists keep a neutral body
		// so their role badge carries the role information cleanly.
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
				default:                    return CassetteTheme.INK_MID;  // specialists: neutral
			}
		}

		// Draw a person-silhouette citizen icon. Anchors a fixed 8×16 figure inside
		// any slot (centered). The figure encodes citizen type on two orthogonal axes:
		//   • Mood (happy/content/unhappy): body color + posture
		//       happy   = bright (PHOS_GLOW) with arms raised over head
		//       content = neutral (INK_MID), standard upright posture
		//       unhappy = red (ALERT), figure shifted down (slumped)
		//   • Role (specialists): a 3-row glyph badge above the head, in a role color
		//       taxman      = PHOS_DIM coin '$'
		//       scientist   = CYAN atom '+'
		//       entertainer = PHOS music note
		public static IBitmap DrawCitizenToken(this IBitmap bitmap, Citizen citizen, int x, int y,
			int slotW = 8, int slotH = 16)
		{
			// Anchor a fixed 8×16 figure within the slot (centered).
			int figX = x + Math.Max(0, (slotW - 8) / 2);
			int figY = y + Math.Max(0, (slotH - 16) / 2);
			int ox = figX + 1;  // inner left  (1px margin)
			int oy = figY + 1;  // inner top
			// inner area: 6 wide × 14 tall (cols ox..ox+5, rows oy..oy+13)

			bool isHappy      = citizen == Citizen.HappyMale   || citizen == Citizen.HappyFemale;
			bool isUnhappy    = citizen == Citizen.UnhappyMale || citizen == Citizen.UnhappyFemale;
			bool isSpecialist = citizen == Citizen.Taxman || citizen == Citizen.Scientist || citizen == Citizen.Entertainer;

			byte head = CassetteTheme.INK_HIGH;
			byte body = CitizenTokenColor(citizen);

			// ── specialist badge (rows 0..2 of inner area) ───────────────────────
			if (isSpecialist)
			{
				byte badgeCol = citizen == Citizen.Taxman    ? CassetteTheme.PHOS_DIM
				              : citizen == Citizen.Scientist ? CassetteTheme.CYAN
				              :                                CassetteTheme.PHOS;
				if (citizen == Citizen.Taxman)
				{
					// Coin / $ — hollow square
					bitmap.FillRectangle(ox + 1, oy,     3, 1, badgeCol);
					bitmap.FillRectangle(ox + 1, oy + 1, 1, 1, badgeCol);
					bitmap.FillRectangle(ox + 3, oy + 1, 1, 1, badgeCol);
					bitmap.FillRectangle(ox + 1, oy + 2, 3, 1, badgeCol);
				}
				else if (citizen == Citizen.Scientist)
				{
					// Atom — plus sign
					bitmap.FillRectangle(ox + 2, oy,     1, 3, badgeCol);
					bitmap.FillRectangle(ox + 1, oy + 1, 3, 1, badgeCol);
				}
				else
				{
					// Entertainer — flagged note
					bitmap.FillRectangle(ox + 2, oy,     2, 1, badgeCol);
					bitmap.FillRectangle(ox + 2, oy + 1, 1, 1, badgeCol);
					bitmap.FillRectangle(ox + 1, oy + 2, 2, 1, badgeCol);
				}
			}

			// ── figure ───────────────────────────────────────────────────────────
			// Unhappy citizens are drawn one row lower (slumped); everyone else is upright.
			int headY  = oy + (isUnhappy ? 5 : 4);
			int torsoY = oy + (isUnhappy ? 8 : 7);
			int torsoH = isUnhappy ? 3 : 4;
			int legsY  = oy + 11;
			int legH   = isUnhappy ? 2 : 3;

			// Head: 4 wide × 3 tall, horizontally centered (cols ox+1..ox+4)
			bitmap.FillRectangle(ox + 1, headY, 4, 3, head);

			// Arms raised (happy only) — sit at outer columns, flanking the head
			if (isHappy)
			{
				bitmap.FillRectangle(ox,     oy + 3, 1, 1, body);  // left fingertip
				bitmap.FillRectangle(ox + 5, oy + 3, 1, 1, body);  // right fingertip
				bitmap.FillRectangle(ox,     headY,  1, 3, body);  // left arm beside head
				bitmap.FillRectangle(ox + 5, headY,  1, 3, body);  // right arm beside head
			}

			// Torso (full 6-wide shoulders)
			bitmap.FillRectangle(ox, torsoY, 6, torsoH, body);

			// Legs: two 2-wide columns with a 2-col gap
			bitmap.FillRectangle(ox,     legsY, 2, legH, body);
			bitmap.FillRectangle(ox + 4, legsY, 2, legH, body);

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