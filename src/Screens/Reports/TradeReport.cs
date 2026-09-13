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

		private const int MARGIN = 8;
		private const int GAP    = 6;

		// ── layout ───────────────────────────────────────────────────────────
		//
		// MEASURED, not guessed, and laid out from the real canvas width rather than inside a
		// fixed 320-wide band. The old version put the stats at OX+86, the split at OX+130 and
		// the maintenance column at OX+160, which held for the cities it was written against
		// and fell apart on a real empire: reported from a 41-city Zulu game where three-digit
		// science overprinted the route split, and `Intombe 94/0/1097` ran straight through
		// `49 Cathedral, 270` in the next column.
		//
		// Every column is sized to the widest string that will actually go in it, so the
		// layout cannot be outgrown by a long city name or a fourth digit. What it cannot do
		// is invent room: on a narrow canvas names are truncated (Fit) rather than allowed to
		// run into their neighbour.
		private static int TextWidth(string text) => Resources.GetTextSize(0, text).Width;

		// Where the four columns sit, given the canvas and how wide the widest entry in each
		// actually is. Pulled out of the drawing and made internal so the one property that
		// matters can be ASSERTED rather than squinted at: no column may reach into the one
		// beside it, for any canvas width and any content. That is precisely what the old
		// fixed offsets could not promise, and a screenshot is how it was found out twice.
		//
		// Numeric columns are RIGHT edges — a wider number grows leftwards into its own
		// column's slack. NameRoom is a width, and is floored at zero: on a canvas too narrow
		// to hold everything the name is cut (Fit) rather than allowed to run.
		internal readonly struct Columns
		{
			public readonly int NameRoom, StatsRight, SplitRight, MaintX;
			public Columns(int nameRoom, int statsRight, int splitRight, int maintX)
			{
				NameRoom = nameRoom; StatsRight = statsRight; SplitRight = splitRight; MaintX = maintX;
			}
		}

		internal static Columns Layout(int width, int maintWidth, int statsWidth, int splitWidth)
		{
			int maintX     = Math.Max(width / 2, width - MARGIN - maintWidth);
			int splitRight = maintX - GAP * 2;
			int statsRight = splitRight - splitWidth - GAP;
			int nameRoom   = Math.Max(0, statsRight - statsWidth - GAP - MARGIN);
			return new Columns(nameRoom, statsRight, splitRight, maintX);
		}

		// The longest prefix of `text` that fits, with a trailing dot to show it was cut.
		private static string Fit(string text, int maxWidth)
		{
			if (maxWidth <= 0) return string.Empty;
			if (string.IsNullOrEmpty(text) || TextWidth(text) <= maxWidth) return text;
			while (text.Length > 1 && TextWidth(text + ".") > maxWidth)
				text = text.Substring(0, text.Length - 1);
			return text + ".";
		}

		private string Stats(City c) =>
			$"{Math.Max(0, (int)c.Luxuries)}{LUXURIES}/{Math.Max(0, (int)c.Taxes)}{GOLD}/{Math.Max(0, (int)c.Science)}{SCIENCE}";

		private string Split(City c) => $"{c.BaseTrade}+{c.TradeRouteBonus}";

		private string[] MaintenanceLines()
		{
			var lines = new System.Collections.Generic.List<string> { "Maintenance Cost" };
			foreach (Building entry in Enum.GetValues(typeof(Building)))
			{
				int count = _cities.SelectMany(c => c.Buildings).Count(b => b.Id == (int)entry);
				if (count == 0) continue;
				IBuilding building = _cities.SelectMany(c => c.Buildings).First(b => b.Id == (int)entry);
				if (building.Maintenance == 0) continue;
				lines.Add($"{count} {building.Name}, {building.Maintenance * count}$");
			}
			lines.Add($"Total Cost: {_cities.Sum(c => c.TotalMaintenance)}$");
			return lines.ToArray();
		}

		// Reserved on EVERY page, not just the one that draws it, so the city list does not
		// shift sideways when the maintenance block appears on the last page.
		private int MaintenanceX
		{
			get
			{
				int w = MaintenanceLines().Max(TextWidth);
				return Layout(Width, w, 0, 0).MaintX;
			}
		}

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

			this.DrawText("City Trade", 0, CassetteTheme.PHOS, MARGIN, 32);

			Columns col = Layout(Width,
				MaintenanceLines().Max(TextWidth),
				_cities.Max(c => TextWidth(Stats(c))),
				_cities.Max(c => TextWidth(Split(c))));
			int splitRight = col.SplitRight, statsRight = col.StatsRight, nameRoom = col.NameRoom;

			int pageSize = PageSize;
			int yy = 40;
			for (int i = (_page++ * pageSize); i < _cities.Length && i < (_page * pageSize); i++)
			{
				City city = _cities[i];
				string name = Fit(city.Name, nameRoom);

				// Home trade and route income, the split the old line could not show: a city's
				// gold said nothing about whether it came from its own ground or from its
				// caravans, and those answer to completely different decisions.
				this.DrawText(name, 0, CassetteTheme.BG0, MARGIN, yy + 1)
					.DrawText(name, 0, CassetteTheme.INK_HIGH, MARGIN, yy)
					.DrawText(Stats(city), 0, CassetteTheme.PHOS_DIM, statsRight, yy, TextAlign.Right)
					.DrawText(Split(city), 0,
						city.TradeRouteBonus > 0 ? CassetteTheme.OK : CassetteTheme.INK_LOW,
						splitRight, yy, TextAlign.Right);

				yy += Resources.GetFontHeight(0);
			}

			if (LastPage)
			{
				int fh = Resources.GetFontHeight(0);
				yy += 4;
				this.DrawText($"Total Trade: {totalTrade} ({totalRoutes} routes)", 0,
					CassetteTheme.INK_HIGH, MARGIN, yy);
				yy += fh;
				this.DrawText($"{totalTaxes}{GOLD} {totalLux}{LUXURIES} {totalScience}{SCIENCE}", 0,
					CassetteTheme.PHOS_DIM, MARGIN, yy);
				yy += fh;
				if (totalScience > 0 && yy <= Height - 8)
				{
					this.DrawText($"Discoveries: {(int)Math.Ceiling((double)Human.ScienceCost / totalScience)} turns", 0, CassetteTheme.INK_HIGH, MARGIN, yy);
				}
			}
		}
		
		private void DrawMaintenanceCost()
		{
			string[] lines = MaintenanceLines();
			int x = MaintenanceX;
			int fh = Resources.GetFontHeight(0);

			this.DrawText(lines[0], 0, CassetteTheme.PHOS, x, 32);
			int yy = 40;
			for (int i = 1; i < lines.Length - 1; i++)
			{
				this.DrawText(lines[i], 0, 14, x, yy);
				yy += fh;
			}
			yy += 4;
			this.DrawText(lines[lines.Length - 1], 0, 14, x, yy);
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
