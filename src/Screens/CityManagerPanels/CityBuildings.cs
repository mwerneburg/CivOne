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
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Screens.Dialogs;
using CivOne.Graphics.Sprites;
using CivOne.Wonders;
using CivOne.IO;

namespace CivOne.Screens.CityManagerPanels
{
	internal class CityBuildings : BaseScreen
	{
		private readonly City _city;
		private IProduction[] _improvements;
		
		private bool _update = true;
		
		public event EventHandler BuildingUpdate;

		private int _page = 0;

		private void DrawWonder(IWonder wonder, int offset)
		{
			string name = "★ " + wonder.Name;
			while (Resources.GetTextSize(1, name).Width > 96)
			{
				name = $"{name.Substring(0, name.Length - 2)}.";
			}
			this.DrawText(name, 1, 15, 4, 3 + (6 * offset));
		}

		private void DrawBuilding(IBuilding building, int offset)
		{
			string name = building.Name;
			while (Resources.GetTextSize(1, name).Width > 80)
			{
				name = $"{name.Substring(0, name.Length - 1)}";
			}
			this.DrawText(name, 1, 15, 4, 3 + (6 * offset))
				.AddLayer(Icons.SellButton, Width - 10, 2 + (6 * offset));
		}

		private IEnumerable<IProduction> GetImprovements
		{
			get
			{
				foreach (IWonder wonder in _city.Wonders)
					yield return wonder;
				foreach (IBuilding building in _city.Buildings)
					yield return building;
			}
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (_update)
			{
				this.Tile(Pattern.PanelBlue);

				for (int i = (_page * 14); i < _improvements.Length && i < ((_page + 1) * 14); i++)
				{
					if (_improvements[i] is IWonder)
					{
						DrawWonder((_improvements[i] as IWonder), i % 14);
						continue;
					}
					DrawBuilding((_improvements[i] as IBuilding), i % 14);
					continue;
				}

				if (_improvements.Length > 14)
				{
					DrawButton("More", 9, 1, 76, 87, 29);
				}

				this.DrawRectangle(colour: 1);
				
				_update = false;
			}
			return true;
		}

		private void SellBuilding(object sender, EventArgs args)
		{
			_city.SellBuilding((sender as ConfirmSell).Building);
			_page = 0;
			_improvements = GetImprovements.ToArray();
			_update = true;
			if (BuildingUpdate is not null)
				BuildingUpdate(this, null);
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (!_city.BuildingSold && args.X > Width - 11 && args.X < Width - 3)
			{
				int yy = 2;
				for (int i = (_page * 14); i < _improvements.Length && i < ((_page + 1) * 14); i++)
				{
					if (args.Y >= yy && args.Y < yy + 8 && _improvements[i] is IBuilding)
					{
						ConfirmSell confirmSell = new ConfirmSell(_improvements[i] as IBuilding);
						confirmSell.Sell += SellBuilding;
						Common.AddScreen(confirmSell);
						return true;
					}
					yy += 6;
				}
			}

			if (args.X > 75 && args.X < 105 && args.Y > 86 && args.Y < 96)
			{
				_page++;
				if ((_page * 14) > _improvements.Length) _page = 0;
				_update = true;
				return true;
			}
			return false;
		}

		public void Resize(int width)
		{
			Bitmap = new Bytemap(width, 97);
			_update = true;
		}

		public CityBuildings(City city) : base(108, 97)
		{
			_city = city;
			_improvements = GetImprovements.ToArray();
		}
	}
}