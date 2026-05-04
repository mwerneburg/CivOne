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
	internal class TradeReport : BaseReport
	{
		private const char LUXURIES = '\\';
		private const char GOLD = '$';
		private const char SCIENCE = '~';

		private readonly City[] _cities;

		private bool _update = true;
		private int _page = 0;

		private void DrawCityTrade()
		{
			int totalIncome = _cities.Sum(c => c.Taxes);
			int totalScience = _cities.Sum(c => c.Science);

			this.DrawText("City Trade", 0, CassetteTheme.PHOS, OX + 8, 32);

			int pageSize = (Height - 40) / Resources.GetFontHeight(0);
			int yy = 40;
			for (int i = (_page++ * pageSize); i < _cities.Length && i < (_page * pageSize); i++)
			{
				City city = _cities[i];

				int lux = Math.Max(0, (int)city.Luxuries);
				int tax = Math.Max(0, (int)city.Taxes);
				int sci = Math.Max(0, (int)city.Science);
				this.DrawText(city.Name, 0, CassetteTheme.BG0, OX + 16, yy + 1)
					.DrawText(city.Name, 0, CassetteTheme.INK_HIGH, OX + 16, yy)
					.DrawText($"{lux}{LUXURIES}/{tax}{GOLD}/{sci}{SCIENCE}", 0, CassetteTheme.PHOS_DIM, OX + 86, yy);

				yy += Resources.GetFontHeight(0);
			}
			
			if ((_page * pageSize) >= _cities.Length)
			{
				yy += 4;
				this.DrawText($"Total Income: {totalIncome}$", 0, 10, OX + 8, yy);
				yy += Resources.GetFontHeight(0);
				if (totalScience > 0 && yy <= Height - 20)
				{
					this.DrawText($"Discoveries: {(int)Math.Ceiling((double)Human.ScienceCost / totalScience)} turns", 0, 10, OX + 8, yy);
				}
			}
		}
		
		private void DrawMaintenanceCost()
		{
			int totalCost = _cities.Sum(c => c.TotalMaintenance);

			this.DrawText("Maintenance Cost", 0, CassetteTheme.PHOS, OX + 160, 32);

			int yy = 40;
			foreach (Building entry in Enum.GetValues(typeof(Building)))
			{
				int count = _cities.SelectMany(c => c.Buildings).Count(b => b.Id == (int)entry);
				if (count == 0) continue;

				IBuilding building = _cities.SelectMany(c => c.Buildings).First(b => b.Id == (int)entry);
				if (building.Maintenance == 0) continue;

				this.DrawText($"{count} {building.Name}, {building.Maintenance * count}$", 0, 14, OX + 160, yy);
				yy += Resources.GetFontHeight(0);
			}

			yy += 4;
			this.DrawText($"Total Cost: {totalCost}$", 0, 14, OX + 160, yy);
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			int pageSize = (Height - 40) / Resources.GetFontHeight(0);
			this.FillRectangle(0, 32, Width, Height - 32, 2);
			DrawCityTrade();
			if ((_page * pageSize) >= _cities.Length)
			{
				DrawMaintenanceCost();
			}

		//	this.AddLayer(Portrait[(int)Advisor.Domestic], 278, 2);

			_update = false;
			return true;
		}

		private bool NextPage()
		{
			int pageSize = (Height - 40) / Resources.GetFontHeight(0);
			if ((_page * pageSize) < _cities.Length)
			{
				_update = true;
			}
			else
			{
				Destroy();
			}
			return true;
		}
		
		public override bool KeyDown(KeyboardEventArgs args)
		{
			return NextPage();
		}
		
		public override bool MouseDown(ScreenEventArgs args)
		{
			return NextPage();
		}

		public TradeReport() : base("TRADE REPORT", 2)
		{
			_cities = Game.GetCities().Where(c => Human == c.Owner && c.Size > 0).ToArray();
		}
	}
}
