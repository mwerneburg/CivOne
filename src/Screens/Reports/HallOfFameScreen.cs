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
using CivOne.Persistence;

namespace CivOne.Screens.Reports
{
	[Modal, OwnPalette, Expand]
	internal class HallOfFameScreen : BaseScreen
	{
		private readonly List<HofEntry> _entries;
		private readonly int            _currentIndex;
		private bool _drawn;

		protected override bool HasUpdate(uint gameTick)
		{
			if (_drawn) return false;
			_drawn = true;

			int fh   = Resources.GetFontHeight(0);
			int fh1  = Resources.GetFontHeight(1);
			int rowH = fh + 3;
			int cx   = Width / 2;

			// Background + header bar
			this.Clear(CassetteTheme.BG0)
				.FillRectangle(0, 0, Width, 27, CassetteTheme.BG3)
				.FillRectangle(0, 27, Width, 1, CassetteTheme.BORDER);

			this.DrawText("HALL OF FAME", 0, CassetteTheme.PHOS_GLOW, cx, 2, TextAlign.Center);
			this.DrawText("All-time leaders", 0, CassetteTheme.INK_MID, cx, 10, TextAlign.Center);
			this.DrawText("Sorted by score", 0, CassetteTheme.INK_LOW, cx, 18, TextAlign.Center);

			int top = 32;
			int maxVisible = (Height - top - 20) / rowH;

			int OX = (Width - 320) / 2;
			int xRank  = OX + 4;
			int xName  = OX + 24;
			int xVic   = OX + 134;
			int xYear  = OX + 230;
			int xScore = OX + 316;

			if (_entries.Count == 0)
			{
				this.DrawText("No entries yet.", 0, CassetteTheme.INK_MID, cx, top + 20, TextAlign.Center);
			}
			else
			{
				// Column headers
				this.DrawText("#",       0, CassetteTheme.INK_LOW, xRank,  top);
				this.DrawText("Leader",  0, CassetteTheme.INK_LOW, xName,  top);
				this.DrawText("Outcome", 0, CassetteTheme.INK_LOW, xVic,   top);
				this.DrawText("Year",    0, CassetteTheme.INK_LOW, xYear,  top);
				this.DrawText("Score",   0, CassetteTheme.INK_LOW, xScore, top, TextAlign.Right);
				top += rowH;
				this.DrawCassetteDivider(OX + 2, top, 316);
				top += 3;

				int shown = _entries.Count < maxVisible ? _entries.Count : maxVisible;
				for (int i = 0; i < shown; i++)
				{
					var e = _entries[i];
					bool isCurrent = (i == _currentIndex);
					if (isCurrent)
						this.FillRectangle(OX + 2, top - 1, 316, rowH, CassetteTheme.PHOS_FAINT);

					byte nameCol  = isCurrent ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;
					byte infoCol  = isCurrent ? CassetteTheme.PHOS      : CassetteTheme.INK_MID;
					byte scoreCol = isCurrent ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;

					this.DrawText($"{i + 1}.",              0, infoCol,  xRank,  top);
					this.DrawText(e.LeaderName,              0, nameCol,  xName,  top);
					this.DrawText(Truncate(e.Victory, 14),  0, infoCol,  xVic,   top);
					this.DrawText(e.Year,                   0, infoCol,  xYear,  top);
					this.DrawText(e.Score.ToString(),       0, scoreCol, xScore, top, TextAlign.Right);
					top += rowH;
				}
				if (_entries.Count > maxVisible)
					this.DrawText($"… and {_entries.Count - maxVisible} more",
						0, CassetteTheme.INK_LOW, cx, top + 2, TextAlign.Center);
			}

			this.DrawCassetteDivider(4, Height - 18, Width - 8);
			this.DrawText("Press any key to continue", 0, CassetteTheme.INK_LOW, cx, Height - 14, TextAlign.Center);

			return true;
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

		private static string Truncate(string s, int maxChars)
		{
			if (s == null || s.Length <= maxChars) return s ?? "";
			return s.Substring(0, maxChars - 1) + "…";
		}

		internal HallOfFameScreen(int currentIndex = -1) : base(MouseCursor.Pointer)
		{
			_currentIndex = currentIndex;
			_entries      = HallOfFame.Load();

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
