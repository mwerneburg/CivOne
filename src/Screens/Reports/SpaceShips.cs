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
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	[Modal, OwnPalette]
	internal class SpaceShips : BaseScreen
	{
		private readonly Player[] _civs;
		private int _civIdx;
		private bool _update = true;

		private int CountStructural(Player p)
		{
			byte id = Game.PlayerNumber(p);
			return Game.Instance.GetCities().Where(c => c.Owner == id).Sum(c => c.Buildings.Count(b => b is SSStructural));
		}

		private int CountComponent(Player p)
		{
			byte id = Game.PlayerNumber(p);
			return Game.Instance.GetCities().Where(c => c.Owner == id).Sum(c => c.Buildings.Count(b => b is SSComponent));
		}

		private int CountModule(Player p)
		{
			byte id = Game.PlayerNumber(p);
			return Game.Instance.GetCities().Where(c => c.Owner == id).Sum(c => c.Buildings.Count(b => b is SSModule));
		}

		private void DrawPartSlots(int built, int minShow, int slotW, int slotH, int gap, int x0, int y0, int cols, byte builtColor)
		{
			int total = Math.Max(built, minShow);
			total = ((total + cols - 1) / cols) * cols; // round up to full rows
			for (int i = 0; i < total; i++)
			{
				int col = i % cols;
				int row = i / cols;
				int x = x0 + col * (slotW + gap);
				int y = y0 + row * (slotH + gap);
				byte fill = i < built ? builtColor : CassetteTheme.BG2;
				this.FillRectangle(x,     y,     slotW,     slotH,     CassetteTheme.BORDER)
				    .FillRectangle(x + 1, y + 1, slotW - 2, slotH - 2, fill);
			}
		}

		private void DrawScreen()
		{
			Player p = _civs[_civIdx];
			byte pid = Game.PlayerNumber(p);
			int s = CountStructural(p);
			int c = CountComponent(p);
			int m = CountModule(p);
			bool launched = Game.Instance.SpaceshipLaunchTurn[pid] != 0;
			bool ready    = s >= 4 && c >= 4 && m >= 2 && !launched;

			// Clear content area
			this.FillRectangle(0, 28, 320, 172, CassetteTheme.BG1);

			// Vertical divider
			this.FillRectangle(159, 28, 1, 172, CassetteTheme.BORDER);

			// ── Left panel: ship diagram ──

			// Module row (min 4 shown, each 24×12, gap 3, 4 cols)
			const int modW = 24, modH = 12, modGap = 3, modCols = 4;
			int modRowPx = modCols * modW + (modCols - 1) * modGap; // 105
			int modX0 = (158 - modRowPx) / 2;
			this.DrawText("MODULES", 0, CassetteTheme.OK, modX0, 32);
			DrawPartSlots(m, 4, modW, modH, modGap, modX0, 42, modCols, CassetteTheme.OK);

			// Component rows (min 4 shown, each 20×10, gap 2, 4 cols)
			const int compW = 20, compH = 10, compGap = 2, compCols = 4;
			int compRowPx = compCols * compW + (compCols - 1) * compGap; // 86
			int compX0 = (158 - compRowPx) / 2;
			this.DrawText("COMPONENTS", 0, CassetteTheme.PHOS, compX0, 64);
			DrawPartSlots(c, 4, compW, compH, compGap, compX0, 74, compCols, CassetteTheme.PHOS);

			// Structural rows (min 4 shown, each 20×10, gap 2, 4 cols)
			const int strW = 20, strH = 10, strGap = 2, strCols = 4;
			int strRowPx = strCols * strW + (strCols - 1) * strGap;
			int strX0 = (158 - strRowPx) / 2;
			this.DrawText("STRUCTURAL", 0, CassetteTheme.CYAN, strX0, 104);
			DrawPartSlots(s, 4, strW, strH, strGap, strX0, 114, strCols, CassetteTheme.CYAN);

			// Target label
			this.FillRectangle(4, 144, 153, 1, CassetteTheme.BORDER)
			    .DrawText("ALPHA CENTAURI", 0, CassetteTheme.PHOS_DIM, 80, 149, TextAlign.Center);

			// ── Right panel: stats ──
			const int rx = 165;
			byte nameColor = Common.ColourLight[pid];

			this.DrawText(p.TribeNamePlural, 0, nameColor, rx, 32)
			    .DrawText(p.LeaderName, 0, CassetteTheme.INK_MID, rx, 42)
			    .FillRectangle(161, 53, 157, 1, CassetteTheme.BORDER);

			int fh = Resources.GetFontHeight(0);
			int yy = 59;
			this.DrawText($"Structural: {s}", 0, CassetteTheme.CYAN,  rx, yy); yy += fh + 2;
			this.DrawText($"Component:  {c}", 0, CassetteTheme.PHOS,  rx, yy); yy += fh + 2;
			this.DrawText($"Module:     {m}", 0, CassetteTheme.OK,    rx, yy); yy += fh + 4;

			this.FillRectangle(161, yy, 157, 1, CassetteTheme.BORDER);
			yy += 5;

			if (launched)
			{
				string launchYear  = Common.YearString((ushort)Game.Instance.SpaceshipLaunchTurn[pid]);
				string arrivalYear = Common.YearString((ushort)Game.Instance.SpaceshipArrivalTurn[pid]);
				bool arrived = Game.Instance.SpaceshipArrivalTurn[pid] <= Game.Instance.GameTurn;
				this.DrawText("LAUNCHED", 0, CassetteTheme.PHOS_GLOW, rx, yy);           yy += fh + 4;
				this.DrawText($"Launch: {launchYear}",  0, CassetteTheme.INK_MID, rx, yy); yy += fh + 2;
				this.DrawText($"ETA:    {arrivalYear}", 0, arrived ? CassetteTheme.OK : CassetteTheme.WARN, rx, yy);
			}
			else if (ready)
			{
				this.DrawText("READY",              0, CassetteTheme.OK,      rx, yy); yy += fh + 4;
				this.DrawText("Awaiting launch...", 0, CassetteTheme.INK_LOW, rx, yy);
			}
			else
			{
				this.DrawText("NOT READY", 0, CassetteTheme.ALERT, rx, yy); yy += fh + 4;
				if (s < 4) { this.DrawText($"Need {4 - s} more structural", 0, CassetteTheme.INK_MID, rx, yy); yy += fh + 2; }
				if (c < 4) { this.DrawText($"Need {4 - c} more component",  0, CassetteTheme.INK_MID, rx, yy); yy += fh + 2; }
				if (m < 2) { this.DrawText($"Need {2 - m} more module",     0, CassetteTheme.INK_MID, rx, yy); }
			}

			// Navigation hints
			this.FillRectangle(161, 182, 157, 1, CassetteTheme.BORDER)
			    .DrawText($"{_civIdx + 1}/{_civs.Length}", 0, CassetteTheme.INK_LOW, 240, 186, TextAlign.Center)
			    .DrawText("LEFT/RIGHT - CYCLE   ESC - CLOSE", 0, CassetteTheme.INK_LOW, 240, 192, TextAlign.Center);
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;
			DrawScreen();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (args.Key == Key.Left)
			{
				_civIdx = (_civIdx - 1 + _civs.Length) % _civs.Length;
				_update = true;
				return true;
			}
			if (args.Key == Key.Right)
			{
				_civIdx = (_civIdx + 1) % _civs.Length;
				_update = true;
				return true;
			}
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		public SpaceShips() : base(MouseCursor.Pointer)
		{
			_civs = Game.Players
				.Where(p => p != 0 && !p.IsDestroyed())
				.ToArray();

			_civIdx = 0;
			for (int i = 0; i < _civs.Length; i++)
			{
				if (_civs[i] == Human) { _civIdx = i; break; }
			}

			Palette pal = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				pal.MergePalette(cassette, 1, 17);
			Palette = pal;

			this.Clear(CassetteTheme.BG0)
			    .FillRectangle(0, 0, 320, 27, CassetteTheme.BG3)
			    .FillRectangle(0, 27, 320, 1, CassetteTheme.BORDER)
			    .DrawText("SPACE RACE", 0, CassetteTheme.PHOS_GLOW, 160, 4, TextAlign.Center)
			    .DrawText(Game.Instance.GameYear, 0, CassetteTheme.INK_MID, 160, 15, TextAlign.Center);
		}
	}
}
