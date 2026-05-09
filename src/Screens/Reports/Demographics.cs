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
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using UniversityBuilding = CivOne.Buildings.University;

namespace CivOne.Screens.Reports
{
	[OwnPalette]
	internal class Demographics : BaseScreen
	{
		private struct TableRow
		{
			public string Title;
			public string Value;
			public int    Place;
			public TableRow(string title, string value, int place) { Title = title; Value = value; Place = place; }
		}

		private readonly TextSettings _shadowText, _normalText;
		private bool _update = true;

		// ── ranking helper ────────────────────────────────────────────────────

		// Returns the human player's rank (1 = best) among non-Barbarian, living players.
		// higherIsBetter=true: rank 1 goes to the player with the highest value.
		private int Rank(Func<Player, int> metric, bool higherIsBetter = true)
		{
			int humanVal = metric(Human);
			return 1 + Game.Players
				.Where(p => !(p.Civilization is Barbarian) && p != Human && !p.IsDestroyed())
				.Count(p => higherIsBetter ? metric(p) > humanVal : metric(p) < humanVal);
		}

		// ── per-player metric functions ───────────────────────────────────────

		private static int ApprovalValue(Player p)
		{
			var cities = p.Cities;
			int total = cities.Sum(c => c.Size);
			if (total == 0) return 0;
			return cities.Sum(c => c.HappyCitizens) * 100 / total;
		}

		private static int GnpValue(Player p)
			=> p.Cities.Sum(c => c.TradeTaxes);

		private static int MfgValue(Player p)
			=> Math.Max(0, p.Cities.Sum(c => c.ShieldIncome));

		private static int LandAreaValue(Player p)
		{
			var cities = p.Cities;
			// Each city contributes a base footprint (50k sq.mi) plus 20k per city-size unit.
			return cities.Length * 50 + cities.Sum(c => c.Size) * 20;
		}

		private static int LiteracyValue(Player p)
		{
			int total  = Common.Advances.Count(a => !(a is FutureTech));
			int have   = p.Advances.Count(a => !(a is FutureTech));
			int cities = Math.Max(1, p.Cities.Length);
			int libPct = p.Cities.Count(c => c.HasBuilding<Library>())           * 5 / cities;
			int uniPct = p.Cities.Count(c => c.HasBuilding<UniversityBuilding>()) * 5 / cities;
			return Math.Min(99, have * 100 / Math.Max(1, total) + libPct + uniPct);
		}

		private static int DiseaseValue(Player p)
		{
			var cities = p.Cities;
			if (cities.Length == 0) return 0;
			// Cities of size > 2 without an Aqueduct are at heightened disease risk.
			int atRisk = cities.Count(c => c.Size > 2 && !c.HasBuilding<Aqueduct>());
			return atRisk * 100 / cities.Length;
		}

		private static int PollutionValue(Player p) => p.Pollution;

		private static int LifeExpValue(Player p)
		{
			int approval  = ApprovalValue(p);
			int disease   = DiseaseValue(p);
			int advCount  = p.Advances.Count(a => !(a is FutureTech));
			int lifeExp   = 35 + approval / 5 - disease / 5 + advCount / 7;
			return Math.Max(20, Math.Min(90, lifeExp));
		}

		// Returns family size × 10 (integer) to avoid floats.
		private static int FamilySizeX10(Player p)
		{
			var cities = p.Cities;
			if (cities.Length == 0) return 15;
			int avgFood = cities.Sum(c => Math.Max(0, c.FoodIncome)) / cities.Length;
			return Math.Min(35, 15 + avgFood * 2);   // 1.5 – 3.5 children
		}

		private static int MilitaryServiceValue(Player p)
		{
			int units = Game.GetUnits().Count(u => p == u.Owner);
			return units * 5 / Math.Max(1, p.Cities.Length);
		}

		private static int AnnualIncomeValue(Player p)
		{
			int goldPerTurn = p.Cities.Sum(c => c.TradeTaxes);
			int pop         = Math.Max(1, p.Population / 10000);
			return goldPerTurn * 100 / pop;
		}

		private static int ProductivityValue(Player p)
			=> p.Cities.Sum(c => Math.Max(0, c.ShieldIncome) + c.TradeTotal);

		// ── table rows ────────────────────────────────────────────────────────

		private IEnumerable<TableRow> GetTable()
		{
			// Approval Rating
			int approval = ApprovalValue(Human);
			yield return new TableRow("Approval Rating", $"{approval}%",
			                          Rank(ApprovalValue));

			// Population (value already has separators; keep existing rank logic)
			{
				string popVal = Human.Population > 0 ? Common.NumberSeperator(Human.Population) : "0";
				yield return new TableRow("Population", popVal,
				                          Rank(p => p.Population));
			}

			// GNP — sum of per-city tax income, shown as "million $"
			int gnp = GnpValue(Human);
			yield return new TableRow("GNP", $"{gnp} million $",
			                          Rank(GnpValue));

			// Manufacturing Goods — total shield production
			int mfg = MfgValue(Human);
			yield return new TableRow("Mfg. Goods", $"{mfg} Mtons",
			                          Rank(MfgValue));

			// Land Area — scaled to thousands of sq. miles
			int area = LandAreaValue(Human) * 1000;
			yield return new TableRow("Land Area", Common.NumberSeperator(area) + " sq.miles",
			                          Rank(LandAreaValue));

			// Literacy — advance count + library/university bonus
			int lit = LiteracyValue(Human);
			yield return new TableRow("Literacy", $"{lit}%",
			                          Rank(LiteracyValue));

			// Disease — fraction of large cities without an Aqueduct (lower is better)
			int disease = DiseaseValue(Human);
			yield return new TableRow("Disease", $"{disease}%",
			                          Rank(DiseaseValue, higherIsBetter: false));

			// Pollution — real value, lower is better
			yield return new TableRow("Pollution", $"{Human.Pollution:D2} tons/year",
			                          Rank(PollutionValue, higherIsBetter: false));

			// Life Expectancy
			int lifeExp = LifeExpValue(Human);
			yield return new TableRow("Life expectancy", $"{lifeExp} years",
			                          Rank(LifeExpValue));

			// Family Size
			int fsX10 = FamilySizeX10(Human);
			yield return new TableRow("Family Size", $"{fsX10 / 10}.{fsX10 % 10} children",
			                          Rank(FamilySizeX10));

			// Military Service — units per city, in notional years of service
			int service = MilitaryServiceValue(Human);
			yield return new TableRow("Military Service", $"{service} years",
			                          Rank(MilitaryServiceValue, higherIsBetter: false));

			// Annual Income — gold per turn per 100 citizens
			int income = AnnualIncomeValue(Human);
			yield return new TableRow("Annual Income", $"{income}$ per capita",
			                          Rank(AnnualIncomeValue));

			// Productivity — shields + trade, all cities
			int prod = ProductivityValue(Human);
			yield return new TableRow("Productivity", $"{prod}",
			                          Rank(ProductivityValue));
		}

		// ── rendering ─────────────────────────────────────────────────────────

		private string Ordinal(int number)
		{
			switch (number)
			{
				case 1:  return $"{number}st";
				case 2:  return $"{number}nd";
				case 3:  return $"{number}rd";
				default: return $"{number}th";
			}
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			int yy = 21;
			foreach (TableRow row in GetTable())
			{
				this.DrawRectangle(4, yy, 312, 1, 9)
					.DrawText($"{row.Title}:", 8, yy + 3, _shadowText)
					.DrawText(row.Value,       104, yy + 3, _normalText)
					.DrawText(Ordinal(row.Place), 192, yy + 3, _shadowText);
				yy += 12;
			}

			_update = false;
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)  { Destroy(); return true; }
		public override bool MouseDown(ScreenEventArgs args)  { Destroy(); return true; }

		public Demographics()
		{
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			_normalText = new TextSettings() { Colour = CassetteTheme.INK_HIGH };
			_shadowText = TextSettings.ShadowText(CassetteTheme.INK_HIGH, CassetteTheme.BG0);

			this.Clear(CassetteTheme.BG0)
				.FillRectangle(0, 0, 320, 18, CassetteTheme.BG3)
				.FillRectangle(0, 18, 320, 1, CassetteTheme.BORDER)
				.DrawText($"{Human.TribeName} Demographics", 0, CassetteTheme.PHOS_GLOW, 160, 4, TextAlign.Center);
		}
	}
}
