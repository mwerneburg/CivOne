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
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne.Screens
{
	[Expand]
	internal class CityChooseProduction : BaseScreen
	{
		private readonly City _city;
		private readonly IProduction[] _items;  // units, then buildings, then wonders
		private int _selection;
		private int _scrollTop;
		private bool _update = true;

		// ─── layout ──────────────────────────────────────────────────────────────

		private int RowH         => Resources.GetFontHeight(0) + 1;
		private int PanelW       => Math.Min(Width - 20, 300);
		private int HeaderH      => Resources.GetFontHeight(1) + 10;
		private int FooterH      => Resources.GetFontHeight(0) + 8;
		private int MaxVisible   => Math.Max(4, (Height - 60 - HeaderH - FooterH) / RowH);
		private int ListH        => MaxVisible * RowH + 4;
		private int PanelH       => HeaderH + ListH + FooterH;
		private int PanelX       => (Width  - PanelW) / 2;
		private int PanelY       => (Height - PanelH) / 2;

		// ─── actions ──────────────────────────────────────────────────────────────

		private void Confirm()
		{
			_city.SetProduction(_items[_selection]);
			Destroy();
		}

		private void EnsureVisible()
		{
			if (_selection < _scrollTop)
				_scrollTop = _selection;
			if (_selection >= _scrollTop + MaxVisible)
				_scrollTop = _selection - MaxVisible + 1;
		}

		// ─── draw ────────────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			int fh0 = Resources.GetFontHeight(0);
			int fh1 = Resources.GetFontHeight(1);
			int px = PanelX, py = PanelY, pw = PanelW;
			int mvr = MaxVisible;

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawCassettePanel(px, py, pw, PanelH);

			// Header
			this.DrawText($"BUILD IN {_city.Name.ToUpper()}", 1, CassetteTheme.PHOS, px + 5, py + 4);
			this.DrawCassetteDivider(px + 2, py + HeaderH - 1, pw - 4);

			// Item list
			int listTop = py + HeaderH + 2;
			IProduction prev = null;
			for (int i = _scrollTop; i < _items.Length && i < _scrollTop + mvr; i++)
			{
				IProduction item = _items[i];
				int ry = listTop + (i - _scrollTop) * RowH;
				bool sel = (i == _selection);

				// Category divider when type changes
				if (prev != null && ItemCategory(item) != ItemCategory(prev))
					this.DrawCassetteDivider(px + 2, ry, pw - 4);

				if (sel)
					this.FillRectangle(px + 2, ry, pw - 4, RowH, CassetteTheme.PHOS_FAINT);

				// Name
				string name = (item as ICivilopedia)?.Name ?? "?";
				byte nameCol = sel ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;
				this.DrawText(name, 0, nameCol, px + 4, ry);

				// Right: turns + combat stats for units
				int turns = TurnsFor(item);
				string right = $"{turns}t";
				if (item is IUnit u)
					right += $"  {u.Attack}/{u.Defense}/{u.Move}";
				byte rightCol = sel ? CassetteTheme.PHOS_DIM : CassetteTheme.INK_LOW;
				this.DrawText(right, 0, rightCol, px + pw - 4, ry, TextAlign.Right);

				prev = item;
			}

			// Scroll indicators (up/down arrows substituted by ASCII)
			if (_scrollTop > 0)
				this.DrawText("^", 0, CassetteTheme.INK_MID, px + pw - 10, listTop);
			if (_scrollTop + mvr < _items.Length)
				this.DrawText("v", 0, CassetteTheme.INK_MID, px + pw - 10, listTop + (mvr - 1) * RowH);

			// Footer
			int footerY = py + HeaderH + ListH + 2;
			this.DrawCassetteDivider(px + 2, footerY - 1, pw - 4);
			this.DrawText("UP/DN MOVE  LETTER JUMP  ENTER SELECT  ESC CANCEL",
				0, CassetteTheme.INK_LOW, px + pw / 2, footerY + 2, TextAlign.Center);

			_update = false;
			return true;
		}

		// ─── helpers ──────────────────────────────────────────────────────────────

		private static int ItemCategory(IProduction item)
		{
			if (item is IUnit)     return 0;
			if (item is IBuilding) return 1;
			return 2;
		}

		private int TurnsFor(IProduction item)
		{
			int remaining = (int)item.Price * 10 - _city.Shields;
			if (_city.ShieldIncome > 1)
				remaining = (int)Math.Ceiling((double)remaining / _city.ShieldIncome);
			return Math.Max(1, remaining);
		}

		// ─── input ────────────────────────────────────────────────────────────────

		public override bool KeyDown(KeyboardEventArgs args)
		{
			switch (args.Key)
			{
				case Key.Up:
				case Key.NumPad8:
					if (_selection > 0) { _selection--; EnsureVisible(); _update = true; }
					return true;
				case Key.Down:
				case Key.NumPad2:
					if (_selection < _items.Length - 1) { _selection++; EnsureVisible(); _update = true; }
					return true;
				case Key.Enter:
					Confirm();
					return true;
				case Key.Escape:
					Destroy();
					return true;
			}

			// Letter cycling: find the next item whose name starts with this letter
			char key = char.ToUpperInvariant(args.KeyChar);
			if (key >= 'A' && key <= 'Z')
			{
				for (int i = 1; i <= _items.Length; i++)
				{
					int idx = (_selection + i) % _items.Length;
					string name = (_items[idx] as ICivilopedia)?.Name ?? "";
					if (name.Length > 0 && char.ToUpperInvariant(name[0]) == key)
					{
						_selection = idx;
						EnsureVisible();
						_update = true;
						break;
					}
				}
				return true;
			}
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			int listTop = PanelY + HeaderH + 2;
			int mvr     = MaxVisible;

			// Click in list area: single-click selects, double-click (same row) confirms
			if (args.X >= PanelX + 2 && args.X < PanelX + PanelW - 2
				&& args.Y >= listTop && args.Y < listTop + mvr * RowH)
			{
				int row = (args.Y - listTop) / RowH;
				int idx = _scrollTop + row;
				if (idx >= 0 && idx < _items.Length)
				{
					if (idx == _selection)
						Confirm();
					else
					{
						_selection = idx;
						_update = true;
					}
				}
				return true;
			}

			// Click outside panel: cancel
			Destroy();
			return true;
		}

		// ─── lifecycle ────────────────────────────────────────────────────────────

		public CityChooseProduction(City city) : base(MouseCursor.Pointer)
		{
			_city = city;

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			var available = _city.AvailableProduction.ToArray();
			_items = available.Where(p2 => p2 is IUnit)
				.Concat(available.Where(p2 => p2 is IBuilding))
				.Concat(available.Where(p2 => p2 is IWonder))
				.ToArray();
		}
	}
}
