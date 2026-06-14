#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens
{
	[Expand]
	internal class GameOptions : BaseScreen
	{
		private const int FONT = 0;

		private readonly (string Label, Func<bool> Get, Action Toggle)[] _options;
		private int  _cursor = 0;
		private bool _update = true;

		// ── layout ───────────────────────────────────────────────────────────────
		private int Fh      => Resources.GetFontHeight(FONT);
		private int RowH    => Fh + 2;
		private int PanelW  => 160;
		private int HeaderH => Fh + 8;
		private int FooterH => Fh + 6;
		private int ListH   => _options.Length * RowH + 4;
		private int PanelH  => HeaderH + ListH + FooterH;
		private int PanelX  => (Width  - PanelW) / 2;
		private int PanelY  => (Height - PanelH) / 2;

		// ── draw ─────────────────────────────────────────────────────────────────
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			int px = PanelX, py = PanelY, pw = PanelW;

			this.DrawCassettePanel(px, py, pw, PanelH, "OPTIONS");

			int listTop = py + HeaderH + 2;
			for (int i = 0; i < _options.Length; i++)
			{
				int  ry  = listTop + i * RowH;
				bool sel = (i == _cursor);
				bool on  = _options[i].Get();

				if (sel)
					this.FillRectangle(px + 2, ry, pw - 4, RowH, CassetteTheme.PHOS_FAINT);

				byte checkCol = on  ? (sel ? CassetteTheme.PHOS_GLOW : CassetteTheme.OK)
				                    : (sel ? CassetteTheme.INK_MID   : CassetteTheme.INK_LOW);
				byte textCol  = sel ? CassetteTheme.PHOS_GLOW
				                    : (on  ? CassetteTheme.INK_HIGH  : CassetteTheme.INK_MID);

				this.DrawText(on ? "^" : " ",    FONT, checkCol, px + 5,      ry);
				this.DrawText(_options[i].Label, FONT, textCol,  px + 5 + 8,  ry);
			}

			int footerY = py + HeaderH + ListH + 2;
			this.DrawCassetteDivider(px + 2, footerY - 1, pw - 4);
			this.DrawText("\x18\x19 MOVE  ENTER/SPC TOGGLE  ESC CLOSE",
				FONT, CassetteTheme.INK_LOW, px + pw / 2, footerY + 2, TextAlign.Center);

			return true;
		}

		// ── input ────────────────────────────────────────────────────────────────
		public override bool KeyDown(KeyboardEventArgs args)
		{
			switch (args.Key)
			{
				case Key.Up:
				case Key.NumPad8:
					if (_cursor > 0) { _cursor--; _update = true; }
					return true;
				case Key.Down:
				case Key.NumPad2:
					if (_cursor < _options.Length - 1) { _cursor++; _update = true; }
					return true;
				case Key.Enter:
				case Key.Space:
					_options[_cursor].Toggle();
					_update = true;
					return true;
				case Key.Escape:
					Destroy();
					return true;
			}
			return false;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			int listTop = PanelY + HeaderH + 2;
			if (args.X >= PanelX + 2 && args.X < PanelX + PanelW - 2
			    && args.Y >= listTop && args.Y < listTop + _options.Length * RowH)
			{
				int row = (args.Y - listTop) / RowH;
				if (row < 0 || row >= _options.Length) return true;
				if (row == _cursor)
				{
					_options[_cursor].Toggle();
				}
				else
				{
					_cursor = row;
				}
				_update = true;
				return true;
			}

			Destroy();
			return true;
		}

		// ── constructor ──────────────────────────────────────────────────────────
		public GameOptions() : base(MouseCursor.Pointer)
		{
			using Palette p = Common.DefaultPalette;
			using (Palette c = CassetteTheme.CreatePalette())
				p.MergePalette(c, 1, 17);
			Palette = p;

			this.AddLayer(Common.Screens.Last(), 0, 0);

			_options = new (string, Func<bool>, Action)[]
			{
				("Instant Advice",    () => Game.InstantAdvice,   () => { Game.InstantAdvice   = !Game.InstantAdvice;   }),
				("End of Turn",       () => Game.EndOfTurn,       () => { Game.EndOfTurn       = !Game.EndOfTurn;       }),
				("Animations",        () => Game.Animations,      () => { Game.Animations      = !Game.Animations;      }),
				("Enemy Moves",       () => Game.EnemyMoves,      () => { Game.EnemyMoves      = !Game.EnemyMoves;      }),
				("Civilopedia Text",  () => Game.CivilopediaText, () => { Game.CivilopediaText = !Game.CivilopediaText; }),
				("Circuses",          () => Game.Circuses,        () => { Game.Circuses        = !Game.Circuses;        }),
				("Barricades",        () => Game.Barricades,      () => { Game.Barricades      = !Game.Barricades;      }),
				("Cursor Coords",     () => Settings.CursorCoords, () => { Settings.CursorCoords = !Settings.CursorCoords; }),
				("Power Saving",      () => Settings.PowerSaving, () => { Settings.PowerSaving  = !Settings.PowerSaving;  }),
				("Autopilot",         () => Settings.Autopilot,   () => { Settings.Autopilot   = !Settings.Autopilot;   }),
				("Debug Menu",        () => Settings.DebugMenu,   () => { Settings.DebugMenu   = !Settings.DebugMenu;   }),
			};
		}
	}
}
