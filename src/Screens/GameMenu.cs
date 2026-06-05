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
using System.Drawing;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using static CivOne.Graphics.CassetteTheme;
using CivOne.IO;
using CivOne.Graphics.Sprites;
using CivOne.UserInterface;

namespace CivOne.Screens
{
	public class GameMenu : BaseScreen
	{
		public readonly MenuItemCollection<int> Items;
		
		private int _activeItem = -1;
		private bool _update = true;

		private bool _keepOpen = false;
		public bool KeepOpen
		{
			get
			{
				return _keepOpen;
			}
			set
			{
				_keepOpen = true;
				_activeItem = 0;
			}
		}

		private int ItemWidth(MenuItem<int> menuItem)
		{
			int width = 0;
			if (menuItem is not null)
			{
				if (menuItem.Text is not null) width += Resources.GetTextSize(0, menuItem.Text).Width;
				if (menuItem.Shortcut is not null) width += Resources.GetTextSize(0, menuItem.Shortcut).Width + 8;
			}
			return width;
		}

		private int MaxItemWidth => Items.Select(x => ItemWidth(x)).Max();

		private void MenuItemDraw(MenuItem<int> menuItem, int x, int y, bool active = false)
		{
			if (menuItem is null || menuItem.Text is null) return;
			byte colour = !menuItem.Enabled
				? CassetteTheme.INK_MID
				: active ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;
			this.DrawText(menuItem.Text, 0, colour, x, y, TextAlign.Left);
			if (menuItem.Shortcut is null) return;
			int textWidth = Resources.GetTextSize(0, menuItem.Text).Width;
			byte shortcutColour = active ? CassetteTheme.PHOS_GLOW : CassetteTheme.PHOS_DIM;
			this.DrawText(menuItem.Shortcut, 0, shortcutColour, x + textWidth + 8, y, TextAlign.Left);
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return true;
			
			int ww = MaxItemWidth + 17;
			int hh = (Resources.GetFontHeight(0) * Items.Count) + 9;
			
			Bitmap = new Bytemap(ww, hh);
			this.Tile(Pattern.PanelGrey, 1, 1)
				.DrawRectangle()
				.DrawRectangle3D(1, 1, ww - 2, hh - 2)
				.As<Picture>();
			
			int i = 0;
			int yy = 5;
			foreach (MenuItem<int> menuItem in Items)
			{
				bool active = i == _activeItem;
				if (active)
					this.FillRectangle(3, yy - 1, MaxItemWidth + 11, Resources.GetFontHeight(0), CassetteTheme.PHOS_FAINT);
				MenuItemDraw(menuItem, 11, yy, active);
				yy += Resources.GetFontHeight(0);
				i++;
			}
			
			_update = false;
			return true;
		}
		
		public override bool KeyDown(KeyboardEventArgs args)
		{
			switch (args.Key)
			{
				case Key.NumPad8:
				case Key.Up:
					if (_activeItem > 0)
					{
						_activeItem--;
						_update = true;
					}
					return true;
				case Key.NumPad2:
				case Key.Down:
					if (_activeItem <= (Items.Count - 1))
					{
						_activeItem++;
						_update = true;
					}
					return true;
				case Key.Escape:
					KeepOpen = false;
					return false;
				case Key.Enter:
					if (_activeItem >= 0)
						Items[_activeItem].Select();
					return false;
			}
			return true;
		}
		
		private int MouseOverItem(ScreenEventArgs args)
		{
			int fontHeight = Resources.GetFontHeight(0);
			int yy = 5;
			
			for (int i = 0; i < Items.Count; i++)
			{
				if (new Rectangle(3, yy, MaxItemWidth + 8, fontHeight).Contains(args.Location)) return i;
				yy += fontHeight;
			}
			
			return -1;
		}
		
		public override bool MouseDrag(ScreenEventArgs args)
		{
			if (KeepOpen) return false;
			int index = MouseOverItem(args);
			if (index == _activeItem) return false;
						
			_activeItem = index;
			
			_update = true;
			return true;
		}
		
		public override bool MouseDown(ScreenEventArgs args)
		{
			if (!KeepOpen) return false;
			int index = MouseOverItem(args);
			if (index == _activeItem) return false;
						
			_activeItem = index;
			
			_update = true;
			return true;
		}
		
		public override bool MouseUp(ScreenEventArgs args)
		{
			if (_activeItem < 0 && !KeepOpen) return false;
			if (_activeItem < 0 && KeepOpen)
			{
				KeepOpen = false;
				return false;
			}
			Items[_activeItem]?.Select();
			
			return true;
		}
		
		public GameMenu(string menuId, Palette palette) : base(8, 8)
		{
			Items = new MenuItemCollection<int>(menuId);
			
			Palette = palette;
		}
	}
}