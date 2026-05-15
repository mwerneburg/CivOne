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
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Persistence;

namespace CivOne.Screens
{
	[Modal, Expand]
	internal class LoadGame : BaseScreen
	{
		private const int MANUAL_SLOTS = 8;
		private const int SLOT_COUNT   = 9; // 8 manual + 1 autosave

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
		public bool Cancel { get; private set; }

		private int  _selection = 0;
		private bool _update    = true;

		// ── slot metadata ────────────────────────────────────────────────────────
		private struct SlotInfo
		{
			public string CosFile;
			public string SveFile;
			public string MapFile;
			public string Label;
			public string Year;
			public bool   Exists;
			public bool   IsCos;
			public bool   IsAuto;
		}

		private SlotInfo[] _slots;

		private SlotInfo[] LoadSlots()
		{
			string dir   = Path.Combine(Settings.SavesDirectory, "c");
			var    slots = new SlotInfo[SLOT_COUNT];

			for (int i = 0; i < MANUAL_SLOTS; i++)
			{
				string basePath = Path.Combine(dir, $"CIVIL{i}");
				slots[i].CosFile = basePath + ".cos";
				slots[i].SveFile = basePath + ".SVE";
				slots[i].MapFile = basePath + ".MAP";
				slots[i].Label   = "(empty)";
				slots[i].Year    = "";

				if (File.Exists(slots[i].CosFile))
				{
					try
					{
						var meta = CosSerializer.DeserializeMeta(File.ReadAllText(slots[i].CosFile));
						if (meta is not null)
						{
							slots[i].Exists = true;
							slots[i].IsCos  = true;
							slots[i].Label  = meta.Name ?? "(unknown)";
							slots[i].Year   = Common.YearString((ushort)meta.Turn);
						}
					}
					catch { slots[i].Label = "(unreadable)"; }
				}
				else if (File.Exists(slots[i].SveFile) && File.Exists(slots[i].MapFile))
				{
					try
					{
						using (var fs = new FileStream(slots[i].SveFile, FileMode.Open))
						using (var br = new BinaryReader(fs))
						{
							if (fs.Length == 37856)
							{
								ushort humanPlayer = Common.BinaryReadUShort(br, 2);
								slots[i].Exists = true;
								slots[i].Label  = Common.BinaryReadStrings(br, 16, 112, 14)[humanPlayer];
								slots[i].Year   = Common.YearString(Common.BinaryReadUShort(br, 0));
							}
						}
					}
					catch { slots[i].Label = "(unreadable)"; }
				}
			}

			// AutoSave slot
			string autoPath = Settings.Instance.AutoSavePath;
			slots[8].CosFile = autoPath;
			slots[8].IsAuto  = true;
			slots[8].Label   = "AutoSave";
			slots[8].Year    = "";
			if (File.Exists(autoPath))
			{
				try
				{
					var meta = CosSerializer.DeserializeMeta(File.ReadAllText(autoPath));
					if (meta is not null)
					{
						slots[8].Exists = true;
						slots[8].IsCos  = true;
						slots[8].Label  = $"AUTO: {meta.Name ?? "autosave"}";
						slots[8].Year   = Common.YearString((ushort)meta.Turn);
					}
				}
				catch { slots[8].Label = "AUTO: (unreadable)"; }
			}

			return slots;
		}

		// ── draw ─────────────────────────────────────────────────────────────────
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			int px = PanelX, py = PanelY, pw = PanelW;

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawCassettePanel(px, py, pw, PanelH);

			// Header
			this.DrawText("LOAD GAME", 1, CassetteTheme.PHOS, px + 6, py + 4);
			this.DrawCassetteDivider(px + 2, py + HeaderH - 1, pw - 4);

			// Slot list
			int listTop = py + HeaderH + 2;
			for (int i = 0; i < SLOT_COUNT; i++)
			{
				SlotInfo s   = _slots[i];
				int      ry  = listTop + i * RowH;
				bool     sel = (i == _selection);

				if (sel)
					this.FillRectangle(px + 2, ry, pw - 4, RowH, CassetteTheme.PHOS_FAINT);

				byte numCol  = sel ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_MID;
				byte namCol  = sel ? CassetteTheme.PHOS_GLOW : (s.Exists ? CassetteTheme.INK_HIGH : CassetteTheme.INK_LOW);
				byte yearCol = sel ? CassetteTheme.PHOS_DIM  : CassetteTheme.INK_LOW;

				string prefix = s.IsAuto ? " A." : $"{i + 1}.";
				this.DrawText(prefix,  0, numCol,  px + 5,      ry);
				this.DrawText(s.Label, 0, namCol,  px + 20,     ry);
				if (s.Year.Length > 0)
					this.DrawText(s.Year, 0, yearCol, px + pw - 5, ry, TextAlign.Right);
			}

			// Footer
			int footerY = py + HeaderH + ListH + 2;
			this.DrawCassetteDivider(px + 2, footerY - 1, pw - 4);
			this.DrawText("↑↓ MOVE  ENTER LOAD  ESC CANCEL",
				0, CassetteTheme.INK_LOW, px + pw / 2, footerY + 2, TextAlign.Center);

			return true;
		}

		// ── input ────────────────────────────────────────────────────────────────
		public override bool KeyDown(KeyboardEventArgs args)
		{
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
					DoLoad();
					return true;
				case Key.Escape:
					Cancel = true;
					Destroy();
					return true;
			}
			return false;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			int listTop = PanelY + HeaderH + 2;
			if (args.X >= PanelX + 2 && args.X < PanelX + PanelW - 2
				&& args.Y >= listTop && args.Y < listTop + SLOT_COUNT * RowH)
			{
				int row = (args.Y - listTop) / RowH;
				if (row < 0 || row >= SLOT_COUNT) return true;
				if (row == _selection) { DoLoad(); return true; }
				_selection = row;
				_update    = true;
				return true;
			}

			// click outside panel — cancel
			Cancel = true;
			Destroy();
			return true;
		}

		private void DoLoad()
		{
			SlotInfo s = _slots[_selection];
			if (!s.Exists) return;

			SaveGame.SelectedGame = Math.Min(_selection, MANUAL_SLOTS - 1);
			Log($"Load game: {s.Label}");
			Destroy();
			if (s.IsCos)
				Game.LoadCos(s.CosFile);
			else
				Game.LoadGame(s.SveFile, s.MapFile);
			if (Game.Started)
				Common.AddScreen(new GamePlay());
		}

		// ── constructor ──────────────────────────────────────────────────────────
		public LoadGame(Palette palette)
		{
			Palette p = Common.DefaultPalette;
			using (Palette c = CassetteTheme.CreatePalette())
				p.MergePalette(c, 1, 17);
			Palette = p;

			_slots = LoadSlots();
		}
	}
}
