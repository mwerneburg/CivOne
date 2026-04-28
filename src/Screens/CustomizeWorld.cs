// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Drawing;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.UserInterface;

namespace CivOne.Screens
{
	internal class CustomizeWorld : BaseScreen
	{
		private static readonly (string Label, Size Size)[] MapSizes = new[]
		{
			("Tiny (40x25)",   new Size(40,  25)),
			("Small (60x40)",  new Size(60,  40)),
			("Normal (80x50)", new Size(80,  50)),
			("Large (120x75)", new Size(120, 75)),
			("Huge (160x100)", new Size(160, 100)),
			("Epic (320x200)", new Size(320, 200)),
		};

		private int _mapSize = -1, _landMass = -1, _temperature = -1, _climate = -1, _age = -1;
		private bool _hasUpdate = true;

		private bool _closing = false;
		
		private int GetMenuWidth(string title, string[] items)
		{
			int i = 0;
			Picture[] texts = new Picture[items.Length + 1];
			texts[i++] = Resources.GetText(" " + title, 0, 15);
			foreach (string item in items)
				texts[i++] = Resources.GetText(" " + item, 0, 5);
			return (texts.Select(t => t.Width).Max()) + 6;
		}
		
		private Menu CreateMenu(int y, string title, MenuItemEventHandler<int> setChoice, params string[] menuTexts)
		{
			Menu menu = new Menu(Palette)
			{
				Title = title,
				X = 203,
				Y = y,
				MenuWidth = GetMenuWidth(title, menuTexts),
				TitleColour = 15,
				ActiveColour = 11,
				TextColour = 15,
				DisabledColour = 8,
				FontId = 0
			};
			
			for (int i = 0; i < menuTexts.Length; i++)
			{
				menu.Items.Add(menuTexts[i], i).OnSelect(setChoice);
			}
			menu.ActiveItem = 1;
			return menu;
		}
		
		private void SetMapSize(object sender, MenuItemEventArgs<int> args)
		{
			_mapSize = args.Value;
			_hasUpdate = true;
		}

		private void SetLandMass(object sender, MenuItemEventArgs<int> args)
		{
			Log("Customize World - Land Mass: {0}", _landMass);
			_landMass = args.Value;
			_hasUpdate = true;
		}
		
		private void SetTemperature(object sender, MenuItemEventArgs<int> args)
		{
			Log("Customize World - Temperature: {0}", _temperature);
			_temperature = args.Value;
			_hasUpdate = true;
		}
		
		private void SetClimate(object sender, MenuItemEventArgs<int> args)
		{
			Log("Customize World - Climate: {0}", _climate);
			_climate = args.Value;
			_hasUpdate = true;
		}
		
		private void SetAge(object sender, MenuItemEventArgs<int> args)
		{
			Log("Customize World - Age: {0}", _age);
			_age = args.Value;
			_hasUpdate = true;
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (_closing)
			{
				if (!HandleScreenFadeOut())
				{
					Destroy();
					Size sz = MapSizes[_mapSize].Size;
					Map.Generate(_landMass, _temperature, _climate, _age, sz.Width, sz.Height);
					if (!Runtime.Settings.ShowIntro)
					{
						Common.AddScreen(new NewGame());
					}
					else
					{
						Common.AddScreen(new Intro());
					}
				}
				return true;
			}
			
			if (!_hasUpdate) return false;

			if (_mapSize < 0) AddMenu(CreateMenu(6, "MAP SIZE:", SetMapSize, "Tiny (40x25)", "Small (60x40)", "Normal (80x50)", "Large (120x75)", "Huge (160x100)", "Epic (320x200)"));
			else if (_landMass < 0) AddMenu(CreateMenu(6, "LAND MASS:", SetLandMass, "Small", "Normal", "Large"));
			else if (_temperature < 0) AddMenu(CreateMenu(56, "TEMPERATURE:", SetTemperature, "Cool", "Temperate", "Warm"));
			else if (_climate < 0) AddMenu(CreateMenu(106, "CLIMATE:", SetClimate, "Arid", "Normal", "Wet"));
			else if (_age < 0) AddMenu(CreateMenu(156, "AGE:", SetAge, "3 billion years", "4 billion years", "5 billion years"));
			else
			{
				_closing = true;
				foreach (IScreen menu in _menus)
					this.AddLayer(menu);
				CloseMenus();
				return true;
			}
			
			_hasUpdate = false;
			return true;
		}
		
		public CustomizeWorld()
		{
			Picture background = Resources["CUSTOM"];
			
			Palette = background.Palette;
			this.AddLayer(background, 0, 0);
		}
	}
}