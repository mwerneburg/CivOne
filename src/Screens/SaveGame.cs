// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.IO;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Persistence;

namespace CivOne.Screens
{
	[Modal, Expand]
	internal class SaveGame : BaseScreen
	{
		private const int SLOT_COUNT = 8;

		// ── layout ───────────────────────────────────────────────────────────────
		private int RowH    => Resources.GetFontHeight(0) + 2;
		private int PanelW  => Math.Min(Width - 40, 300);
		private int HeaderH => Resources.GetFontHeight(1) + 10;
		private int FooterH => Resources.GetFontHeight(0) + 8;
		private int ListH   => SLOT_COUNT * RowH + 4;
		private int PanelH  => HeaderH + ListH + FooterH;
		private int PanelX  => (Width  - PanelW) / 2;
		private int PanelY  => (Height - PanelH) / 2;

		// ── state ────────────────────────────────────────────────────────────────
		internal static int SelectedGame = 0;

		private int  _selection = SelectedGame;
		private bool _update    = true;
		private bool _saved     = false;

		// ── slot metadata ────────────────────────────────────────────────────────
		private struct SlotInfo
		{
			public string CosFile;
			public string Label;
			public string Year;
			public bool   Exists;
		}

		private SlotInfo[] _slots;

		private SlotInfo[] LoadSlots()
		{
			string dir = Path.Combine(Settings.SavesDirectory, "c");
			var slots  = new SlotInfo[SLOT_COUNT];
			for (int i = 0; i < SLOT_COUNT; i++)
			{
				string path = Path.Combine(dir, $"CIVIL{i}.cos");
				slots[i].CosFile = path;
				slots[i].Exists  = false;
				slots[i].Label   = "(empty)";
				slots[i].Year    = "";
				if (!File.Exists(path)) continue;
				try
				{
					var meta = CosSerializer.DeserializeMeta(File.ReadAllText(path));
					if (meta is null) continue;
					slots[i].Exists = true;
					slots[i].Label  = meta.Name ?? "(unknown)";
					slots[i].Year   = Common.YearString((ushort)meta.Turn);
				}
				catch { slots[i].Label = "(unreadable)"; }
			}
			return slots;
		}

		// ── draw ─────────────────────────────────────────────────────────────────
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			int px = PanelX, py = PanelY, pw = PanelW;
			int fh0 = Resources.GetFontHeight(0);
			int fh1 = Resources.GetFontHeight(1);

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawCassettePanel(px, py, pw, PanelH);

			if (_saved)
			{
				this.DrawText("GAME SAVED", 1, CassetteTheme.PHOS_GLOW, px + pw / 2, py + PanelH / 2 - fh1, TextAlign.Center);
				this.DrawText("Press any key", 0, CassetteTheme.INK_MID, px + pw / 2, py + PanelH / 2 + 2, TextAlign.Center);
				return true;
			}

			// Header
			this.DrawText("SAVE GAME", 1, CassetteTheme.PHOS, px + 6, py + 4);
			this.DrawCassetteDivider(px + 2, py + HeaderH - 1, pw - 4);

			// Slot list
			int listTop = py + HeaderH + 2;
			for (int i = 0; i < SLOT_COUNT; i++)
			{
				SlotInfo s  = _slots[i];
				int ry      = listTop + i * RowH;
				bool sel    = (i == _selection);

				if (sel)
					this.FillRectangle(px + 2, ry, pw - 4, RowH, CassetteTheme.PHOS_FAINT);

				byte numCol  = sel ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_MID;
				byte namCol  = sel ? CassetteTheme.PHOS_GLOW : (s.Exists ? CassetteTheme.INK_HIGH : CassetteTheme.INK_LOW);
				byte yearCol = sel ? CassetteTheme.PHOS_DIM  : CassetteTheme.INK_LOW;

				this.DrawText($"{i + 1}.", 0, numCol, px + 5, ry);
				this.DrawText(s.Label,     0, namCol,  px + 20, ry);
				if (s.Year.Length > 0)
					this.DrawText(s.Year, 0, yearCol, px + pw - 5, ry, TextAlign.Right);
			}

			// Footer
			int footerY = py + HeaderH + ListH + 2;
			this.DrawCassetteDivider(px + 2, footerY - 1, pw - 4);
			this.DrawText("↑↓ MOVE  ENTER SAVE  ESC CANCEL",
				0, CassetteTheme.INK_LOW, px + pw / 2, footerY + 2, TextAlign.Center);

			return true;
		}

		// ── input ────────────────────────────────────────────────────────────────
		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (_saved) { Destroy(); return true; }

			switch (args.Key)
			{
				case Key.Up:
				case Key.NumPad8:
					if (_selection > 0) { _selection--; _update = true; }
					return true;
				case Key.Down:
				case Key.NumPad2:
					if (_selection < SLOT_COUNT - 1) { _selection++; _update = true; }
					return true;
				case Key.Enter:
					DoSave();
					return true;
				case Key.Escape:
					Destroy();
					return true;
			}
			return false;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_saved) { Destroy(); return true; }

			int listTop = PanelY + HeaderH + 2;
			if (args.X >= PanelX + 2 && args.X < PanelX + PanelW - 2
				&& args.Y >= listTop && args.Y < listTop + SLOT_COUNT * RowH)
			{
				int row = (args.Y - listTop) / RowH;
				if (row < 0 || row >= SLOT_COUNT) return true;
				if (row == _selection) { DoSave(); return true; }
				_selection = row;
				_update    = true;
				return true;
			}

			// click outside panel — cancel
			Destroy();
			return true;
		}

		private void DoSave()
		{
			SelectedGame = _selection;
			Game.SaveCos(_slots[_selection].CosFile);
			_saved  = true;
			_update = true;
		}

		// ── constructor ──────────────────────────────────────────────────────────
		public SaveGame() : base(MouseCursor.Pointer)
		{
			using Palette p = Common.DefaultPalette;
			using (Palette c = CassetteTheme.CreatePalette())
				p.MergePalette(c, 1, 17);
			Palette = p;

			_slots = LoadSlots();
			_selection = Math.Max(0, Math.Min(SelectedGame, SLOT_COUNT - 1));
		}
	}
}
