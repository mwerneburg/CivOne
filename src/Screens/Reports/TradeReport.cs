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

		// Rows the summary block needs under the last page. Held back from the page size so
		// the totals cannot be pushed off the bottom of a full screen.
		private const int SUMMARY_ROWS = 3;

		// One definition. It was computed in three places against the same expression, which
		// is the shape that drifts: pagination, the "is this the last page" test and the list
		// loop all have to agree or the totals draw over a city or never draw at all.
		private int PageSize => Math.Max(1,
			((Height - 40) / Resources.GetFontHeight(0)) - SUMMARY_ROWS);

		private bool LastPage => (_page * PageSize) >= _cities.Length;

		private void DrawCityTrade()
		{
			// TradeTotal = BaseTrade + TradeRouteBonus, and Taxes is levied on the whole of
			// it — so route income is INSIDE the tax figure, never additional to it. The old
			// summary read "Total Income: {sum of Taxes}" and nothing else, which at a 0% tax
			// rate printed a flat 0 beside city lines showing hundreds of trade. True, and
			// useless: the trade was all going to science and luxuries and the screen would
			// not say so. The block below reports the gross and then where it went.
			int totalTaxes   = _cities.Sum(c => Math.Max(0, (int)c.Taxes));
			int totalLux     = _cities.Sum(c => Math.Max(0, (int)c.Luxuries));
			int totalScience = _cities.Sum(c => Math.Max(0, (int)c.Science));
			int totalTrade   = _cities.Sum(c => c.TradeTotal);
			int totalRoutes  = _cities.Sum(c => c.TradeRouteBonus);

			this.DrawText("City Trade", 0, CassetteTheme.PHOS, OX + 8, 32);

			int pageSize = PageSize;
			int yy = 40;
			for (int i = (_page++ * pageSize); i < _cities.Length && i < (_page * pageSize); i++)
			{
				City city = _cities[i];

				int lux = Math.Max(0, (int)city.Luxuries);
				int tax = Math.Max(0, (int)city.Taxes);
				int sci = Math.Max(0, (int)city.Science);
				// Home trade and route income, the split the old line could not show: a city's
				// gold said nothing about whether it came from its own ground or from its
				// caravans, and those answer to completely different decisions.
				this.DrawText(city.Name, 0, CassetteTheme.BG0, OX + 16, yy + 1)
					.DrawText(city.Name, 0, CassetteTheme.INK_HIGH, OX + 16, yy)
					.DrawText($"{lux}{LUXURIES}/{tax}{GOLD}/{sci}{SCIENCE}", 0, CassetteTheme.PHOS_DIM, OX + 86, yy)
					.DrawText($"{city.BaseTrade}+{city.TradeRouteBonus}", 0,
						city.TradeRouteBonus > 0 ? CassetteTheme.OK : CassetteTheme.INK_LOW,
						OX + 130, yy);

				yy += Resources.GetFontHeight(0);
			}

			if (LastPage)
			{
				int fh = Resources.GetFontHeight(0);
				yy += 4;
				this.DrawText($"Total Trade: {totalTrade} ({totalRoutes} routes)", 0,
					CassetteTheme.INK_HIGH, OX + 8, yy);
				yy += fh;
				this.DrawText($"{totalTaxes}{GOLD} {totalLux}{LUXURIES} {totalScience}{SCIENCE}", 0,
					CassetteTheme.PHOS_DIM, OX + 8, yy);
				yy += fh;
				if (totalScience > 0 && yy <= Height - 8)
				{
					this.DrawText($"Discoveries: {(int)Math.Ceiling((double)Human.ScienceCost / totalScience)} turns", 0, CassetteTheme.INK_HIGH, OX + 8, yy);
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

			this.FillRectangle(0, 32, Width, Height - 32, 2);
			DrawCityTrade();
			if (LastPage)
			{
				DrawMaintenanceCost();
			}

		//	this.AddLayer(Portrait[(int)Advisor.Domestic], 278, 2);

			_update = false;
			return true;
		}

		private bool NextPage()
		{
			if (!LastPage)
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
