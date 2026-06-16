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
using System.Collections.Generic;
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
		private readonly IProduction[] _items;
		private int _filter = 0;   // 0=All 1=Units 2=Buildings 3=Wonders
		private int _selection;
		private int _scrollTop;
		private bool _update = true;

		private static readonly string[] FilterNames = { "ALL", "UNITS", "BUILDINGS", "WONDERS" };

		private IProduction[] Filtered
		{
			get
			{
				var queued = new HashSet<Type>(_city.ProductionQueue.Select(q => q.GetType()));
				IEnumerable<IProduction> src = _filter switch
				{
					1 => _items.Where(x => x is IUnit),
					2 => _items.Where(x => x is IBuilding),
					3 => _items.Where(x => x is IWonder),
					_ => _items
				};
				// Buildings and wonders are one-per-city — once queued, hide them from
				// the picker so the player can't queue Barracks twice. Units can be
				// queued any number of times (Militia → Barracks → Phalanx → Temple →
				// Settler → MarketPlace → Aqueduct → Settler is a valid backlog), so
				// they stay visible after being queued.
				return src.Where(x => x is IUnit || !queued.Contains(x.GetType())).ToArray();
			}
		}

		// ─── layout ──────────────────────────────────────────────────────────────

		private int RowH       => Resources.GetFontHeight(0) + 1;
		private int PanelW     => Math.Min(Width - 20, 300);
		private int HeaderH    => Resources.GetFontHeight(1) + 10;
		private int FooterH    => Resources.GetFontHeight(0) + 8;
		private int MaxVisible => Math.Max(4, (Height - 80 - HeaderH - FooterH - QueueSectionH) / RowH);
		private int ListH      => MaxVisible * RowH + 4;

		// Queue section: header line + one row per queued item, capped at 5 visible
		private int QueueRowH     => Resources.GetFontHeight(0) + 2;
		private int QueueVisible  => Math.Min(_city.ProductionQueue.Count, 5);
		private int QueueSectionH => _city.ProductionQueue.Count == 0 ? 0 :
		                             FooterH + QueueVisible * QueueRowH + 4;

		private int PanelH => HeaderH + ListH + FooterH + QueueSectionH;
		private int PanelX => (Width  - PanelW) / 2;
		private int PanelY => (Height - PanelH) / 2;

		// ─── actions ─────────────────────────────────────────────────────────────

		private void Confirm()
		{
			IProduction choice = Filtered[_selection];
			_city.SetProduction(choice);
			DecisionLogger.LogCityProduction(_city, choice, "Human", isHuman: true);
			Destroy();
		}

		private void EnqueueSelected()
		{
			var filtered = Filtered;
			if (filtered.Length == 0) return;
			_city.EnqueueProduction(filtered[_selection]);
			// List just shrank; keep cursor in bounds and visible
			int newLen = Filtered.Length;
			if (_selection >= newLen) _selection = Math.Max(0, newLen - 1);
			EnsureVisible();
			_update = true;
		}

		private void RemoveFromQueue(int index)
		{
			var queue = _city.ProductionQueue.ToList();
			if (index < 0 || index >= queue.Count) return;
			_city.ClearProductionQueue();
			for (int i = 0; i < queue.Count; i++)
				if (i != index) _city.EnqueueProduction(queue[i]);
			_update = true;
		}

		private void EnsureVisible()
		{
			if (_selection < _scrollTop)
				_scrollTop = _selection;
			if (_selection >= _scrollTop + MaxVisible)
				_scrollTop = _selection - MaxVisible + 1;
		}

		private void CycleFilter()
		{
			_filter    = (_filter + 1) % 4;
			_selection = 0;
			_scrollTop = 0;
			_update    = true;
		}

		// ─── draw ────────────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			int fh0 = Resources.GetFontHeight(0);
			int fh1 = Resources.GetFontHeight(1);
			int px = PanelX, py = PanelY, pw = PanelW;
			int mvr = MaxVisible;
			var filtered = Filtered;
			var queue = _city.ProductionQueue;

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawCassettePanel(px, py, pw, PanelH);

			// Header
			this.DrawText($"BUILD IN {_city.Name.ToUpper()}", 1, CassetteTheme.PHOS, px + 5, py + 4);
			this.DrawText($"[{FilterNames[_filter]}]", 1, CassetteTheme.INK_MID, px + pw - 5, py + 4, TextAlign.Right);
			this.DrawCassetteDivider(px + 2, py + HeaderH - 1, pw - 4);

			// Item list
			int listTop = py + HeaderH + 2;
			IProduction? prev = null;
			for (int i = _scrollTop; i < filtered.Length && i < _scrollTop + mvr; i++)
			{
				IProduction item = filtered[i];
				int ry = listTop + (i - _scrollTop) * RowH;
				bool sel = (i == _selection);

				if (prev is not null && ItemCategory(item) != ItemCategory(prev))
					this.DrawCassetteDivider(px + 2, ry, pw - 4);

				if (sel)
					this.FillRectangle(px + 2, ry, pw - 4, RowH, CassetteTheme.PHOS_FAINT);

				string name = (item as ICivilopedia)?.Name ?? "?";
				byte nameCol = sel ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;
				this.DrawText(name, 0, nameCol, px + 4, ry);

				int turns = TurnsFor(item);
				string right = $"{turns}t";
				if (item is IUnit u)
					right += $"  {u.Attack}/{u.Defense}/{u.Move}";
				byte rightCol = sel ? CassetteTheme.PHOS_DIM : CassetteTheme.INK_LOW;
				this.DrawText(right, 0, rightCol, px + pw - 4, ry, TextAlign.Right);

				prev = item;
			}

			// Scroll indicators
			if (_scrollTop > 0)
				this.DrawText("^", 0, CassetteTheme.INK_MID, px + pw - 10, listTop);
			if (_scrollTop + mvr < filtered.Length)
				this.DrawText("v", 0, CassetteTheme.INK_MID, px + pw - 10, listTop + (mvr - 1) * RowH);

			// Footer / key hints
			int footerY = py + HeaderH + ListH + 2;
			this.DrawCassetteDivider(px + 2, footerY - 1, pw - 4);
			this.DrawText("TAB FILTER  ENTER=NOW  Q=QUEUE",
				0, CassetteTheme.INK_LOW, px + pw / 2, footerY + 2, TextAlign.Center);

			// Queue section
			if (queue.Count > 0)
			{
				int qy = footerY + FooterH;
				this.DrawCassetteDivider(px + 2, qy, pw - 4);
				this.DrawText("QUEUE:", 0, CassetteTheme.PHOS_DIM, px + 4, qy + 2);
				qy += FooterH - 2;
				int xBtn = px + pw - 16;
				for (int i = 0; i < QueueVisible; i++)
				{
					IProduction item = queue[i];
					string name = (item as ICivilopedia)?.Name ?? "?";
					int turns = TurnsFor(item);
					this.DrawText($"{i + 1}. {name}", 0, CassetteTheme.INK_HIGH, px + 4, qy);
					this.DrawText($"{turns}t", 0, CassetteTheme.INK_LOW, xBtn - 4, qy, TextAlign.Right);
					this.DrawText("[x]", 0, CassetteTheme.ALERT, xBtn, qy);
					qy += QueueRowH;
				}
				if (queue.Count > QueueVisible)
					this.DrawText($"  … {queue.Count - QueueVisible} more", 0, CassetteTheme.INK_LOW, px + 4, qy);
			}

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
			var filtered = Filtered;
			switch (args.Key)
			{
				case Key.Up:
				case Key.NumPad8:
					if (_selection > 0) { _selection--; EnsureVisible(); _update = true; }
					return true;
				case Key.Down:
				case Key.NumPad2:
					if (_selection < filtered.Length - 1) { _selection++; EnsureVisible(); _update = true; }
					return true;
				case Key.Tab:
					CycleFilter();
					return true;
				case Key.Enter:
					if (filtered.Length > 0) Confirm();
					return true;
				case Key.Escape:
					Destroy();
					return true;
			}

			char key = char.ToUpperInvariant(args.KeyChar);
			if (key == 'Q')
			{
				if (filtered.Length > 0) EnqueueSelected();
				return true;
			}
			if (key >= 'A' && key <= 'Z')
			{
				for (int i = 1; i <= filtered.Length; i++)
				{
					int idx = (_selection + i) % filtered.Length;
					string name = (filtered[idx] as ICivilopedia)?.Name ?? "";
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
			int px = PanelX, pw = PanelW;
			int listTop = PanelY + HeaderH + 2;
			int mvr     = MaxVisible;
			var filtered = Filtered;
			var queue    = _city.ProductionQueue;

			// Click in item list
			if (args.X >= px + 2 && args.X < px + pw - 2
				&& args.Y >= listTop && args.Y < listTop + mvr * RowH)
			{
				int row = (args.Y - listTop) / RowH;
				int idx = _scrollTop + row;
				if (idx >= 0 && idx < filtered.Length)
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

			// Click on queue × buttons
			if (queue.Count > 0)
			{
				int footerY = PanelY + HeaderH + ListH + 2;
				int qy = footerY + FooterH + FooterH - 2;   // past divider + "QUEUE:" header row
				int xBtn = px + pw - 16;
				for (int i = 0; i < QueueVisible; i++)
				{
					if (args.X >= xBtn && args.X <= px + pw - 4
						&& args.Y >= qy && args.Y < qy + QueueRowH)
					{
						RemoveFromQueue(i);
						return true;
					}
					qy += QueueRowH;
				}
			}

			// Click outside panel: cancel
			Destroy();
			return true;
		}

		// ─── lifecycle ────────────────────────────────────────────────────────────

		public CityChooseProduction(City city) : base(MouseCursor.Pointer)
		{
			_city = city;

			using Palette p = Common.DefaultPalette;
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
