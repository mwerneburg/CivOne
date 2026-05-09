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
using System.Reflection;
using System.Text;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Concepts;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne.Screens
{
	[Modal, OwnPalette, Expand]
	internal class Civilopedia : BaseScreen
	{
		internal static ICivilopedia[] Advances = Reflect.GetCivilopediaAdvances().OrderBy(x => x.Name).ToArray();
		internal static ICivilopedia[] Improvements = Reflect.GetCivilopediaCityImprovements().OrderBy(x => x.Name).ToArray();
		internal static ICivilopedia[] Units = Reflect.GetCivilopediaUnits().OrderBy(x => x.Name).ToArray();
		internal static ICivilopedia[] TerrainType = Reflect.GetCivilopediaTerrainTypes().OrderBy(x => x.Name).ToArray();
		internal static ICivilopedia[] Misc = Reflect.GetConcepts().OrderBy(x => x.Name).ToArray();
		internal static ICivilopedia[] Complete = Reflect.GetCivilopediaAll().OrderBy(x => x.Name).ToArray();

		private readonly ICivilopedia[] _pages;
		private readonly ICivilopedia _singlePage;
		private readonly bool _discovered;

		private bool _update = true;
		private int _startIndex = 0;
		private byte _pageNumber = 1;

		private int OX => (Width - 320) / 2;

		private static Palette BuildPalette()
		{
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			return p;
		}

		private string GetCategory()
		{
			if (_singlePage is ITile) return "Terrain Type";
			if (_singlePage is IWonder) return "Wonder of the World";
			if (_singlePage is IBuilding) return "City Improvement";
			if (_singlePage is IUnit) return "Military Unit";
			if (_singlePage is IAdvance) return "Civilization Advance";
			if (_singlePage is IConcept) return "Game Concept";
			return "";
		}

		private string[] GetBlurb(byte pageNumber)
		{
			string suffix = pageNumber == 1 ? "" : "2";
			if (_singlePage is IBuilding || _singlePage is IWonder)
				return Resources.GetCivilopediaText("BLURB1/" + _singlePage.Name.ToUpper() + suffix);
			if (_singlePage is IUnit)
				return Resources.GetCivilopediaText("BLURB2/" + _singlePage.Name.ToUpper() + suffix);
			if (_singlePage is IAdvance)
				return Resources.GetCivilopediaText("BLURB0/" + _singlePage.Name.ToUpper() + suffix);
			if (_singlePage is IConcept)
				return Resources.GetCivilopediaText("BLURB4/" + _singlePage.Name.ToUpper() + suffix);
			return new string[0];
		}

		private void DrawHeader()
		{
			this.FillRectangle(0, 0, Width, 27, CassetteTheme.BG3)
				.FillRectangle(0, 27, Width, 1, CassetteTheme.BORDER);

			if (_singlePage == null)
			{
				this.DrawText("ENCYCLOPEDIA OF CIVILIZATION", 0, CassetteTheme.PHOS_GLOW, Width / 2, 9, TextAlign.Center);
				if (_pages.Length > 78)
					this.DrawText("MORE", 0, CassetteTheme.PHOS, OX + 8, 18, TextAlign.Left);
				this.DrawText("EXIT", 0, CassetteTheme.PHOS, OX + 286, 18, TextAlign.Left);
				return;
			}

			this.DrawText(GetCategory().ToUpper(), 0, CassetteTheme.PHOS_DIM, Width / 2, 4, TextAlign.Center)
				.DrawText(_singlePage.Name, 0, CassetteTheme.PHOS_GLOW, Width / 2, 14, TextAlign.Center);
			if (_pageNumber == 2 && _discovered)
				this.DrawText("(Discovered)", 0, CassetteTheme.INK_MID, Width / 2, 21, TextAlign.Center);
		}

		private void DrawSinglePage()
		{
			int yy = 34;

			string[] blurb = GetBlurb(_pageNumber);
			foreach (string line in blurb)
			{
				this.DrawText(line, 0, CassetteTheme.INK_HIGH, OX + 12, yy);
				yy += 9;
			}

			if (_singlePage is ITile)
			{
				DrawTerrainText(ref yy);
				return;
			}

			if (_pageNumber == 2)
			{
				if (yy > 34) yy += 8;
				DrawStats(ref yy);
			}
		}

		private void DrawStats(ref int yy)
		{
			if (_singlePage is IBuilding b)
			{
				string tech = b.RequiredTech?.Name ?? "(none)";
				this.DrawText($"Requires: {tech}", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Cost: {b.Price * 10} shields", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Maintenance: ${b.Maintenance}/turn", 0, CassetteTheme.INK_MID, OX + 12, yy);
				return;
			}
			if (_singlePage is BaseWonder w)
			{
				string tech = w.RequiredTech?.Name ?? "(none)";
				this.DrawText($"Requires: {tech}", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Cost: {w.Price * 10} shields", 0, CassetteTheme.INK_MID, OX + 12, yy);
				return;
			}
			if (_singlePage is IUnit u)
			{
				string tech = u.RequiredTech?.Name ?? "(none)";
				this.DrawText($"Requires: {tech}", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Cost: {u.Price * 10} resources", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Attack: {u.Attack}   Defense: {u.Defense}   Move: {u.Move}", 0, CassetteTheme.INK_MID, OX + 12, yy);
				return;
			}
			if (_singlePage is IAdvance adv)
			{
				if (adv.RequiredTechs.Length > 0)
				{
					var sb = new StringBuilder();
					foreach (IAdvance req in adv.RequiredTechs) { if (sb.Length > 0) sb.Append(" and "); sb.Append(req.Name); }
					this.DrawText($"Requires: {sb}", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				}
				yy += 4;
				this.DrawText("Allows:", 0, CassetteTheme.INK_HIGH, OX + 12, yy); yy += 9;
				foreach (IAdvance tech in Common.Advances.Where(a => a.Requires(adv.Id)))
				{
					string allows = tech.Name;
					foreach (IAdvance at in tech.RequiredTechs.Where(a => a.Id != adv.Id))
						allows += $" (with {at.Name})";
					this.DrawText(allows, 0, CassetteTheme.INK_MID, OX + 20, yy); yy += 9;
				}
				foreach (IUnit unit in Reflect.GetUnits().Where(u2 => u2.RequiredTech != null && u2.RequiredTech.Id == adv.Id))
				{
					this.DrawText($"{unit.Name} unit", 0, CassetteTheme.INK_MID, OX + 20, yy); yy += 9;
				}
				foreach (IBuilding building in Reflect.GetBuildings().Where(bld => bld.RequiredTech != null && bld.RequiredTech.Id == adv.Id))
				{
					this.DrawText($"{building.Name} improvement", 0, CassetteTheme.INK_MID, OX + 20, yy); yy += 9;
				}
				foreach (IWonder wonder in Reflect.GetWonders().Where(wndr => wndr.RequiredTech != null && wndr.RequiredTech.Id == adv.Id))
				{
					this.DrawText($"{wonder.Name} Wonder", 0, CassetteTheme.INK_MID, OX + 20, yy); yy += 9;
				}
			}
		}

		private bool NextPage()
		{
			if (_singlePage != null && _pageNumber < _singlePage.PageCount)
			{
				_pageNumber++;
				_update = true;
				return true;
			}
			return false;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			this.Clear(CassetteTheme.BG0);
			DrawHeader();

			if (_singlePage == null)
			{
				int xx = OX + 10, yy = 32;
				int columns = (int)Math.Ceiling((float)_pages.Length / 26);
				int columnWidth = (columns < 3) ? 150 : 100;
				for (int i = _startIndex; i < _pages.Length; i++)
				{
					string name = _pages[i].Name;
					if (columns >= 3 && name.Length >= 18) name = $"{name.Substring(0, 17)}.";
					this.DrawText(name, 0, CassetteTheme.INK_HIGH, xx, yy);
					yy += 7;
					if (yy > Height - 10) { xx += columnWidth; if (xx > OX + 300) break; yy = 32; }
				}
			}
			else
			{
				DrawSinglePage();
			}

			_update = false;
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (_singlePage != null && NextPage()) return true;
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_singlePage != null)
			{
				if (!NextPage()) Destroy();
				return true;
			}

			if (args.Y < 32)
			{
				if (args.X - OX < 160)
				{
					if (_pages.Length <= 78) return false;
					_startIndex += 78;
					if (_startIndex >= _pages.Length) _startIndex = 0;
					_update = true;
					return true;
				}
				Destroy();
				return true;
			}

			int lx = OX + 10, ly = 32;
			int cols = (int)Math.Ceiling((float)_pages.Length / 26);
			int colWidth = (cols < 3) ? 150 : 100;
			for (int i = _startIndex; i < _pages.Length; i++)
			{
				if (args.X > lx + colWidth) { i += 25; lx += colWidth; continue; }
				if (args.Y >= ly && args.Y <= ly + 7)
				{
					Common.AddScreen(new Civilopedia(_pages[i]));
					return true;
				}
				ly += 7;
				if (ly > Height - 10) { lx += colWidth; if (lx > OX + 300) break; ly = 32; }
			}
			return false;
		}

		private void DrawTerrainTextValues(ref int y, string name, string food = null, string production = null, string trade = null, string foodIrrigation = null, string productionMining = null, string tradeRoads = null)
		{
			string foodFormat = "Food: {0} units.";
			string productionFormat = "Production: {0} units.";
			string tradeFormat = "Trade: {0}";

			this.DrawText(name, 0, CassetteTheme.INK_HIGH, OX + 12, y);
			y += 8;
			if (food != null)
			{
				if (foodIrrigation != null)
					food = string.Format("{0} ({1} with Irrigation)", food, foodIrrigation);
				this.DrawText(string.Format(foodFormat, food), 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			if (production != null)
			{
				if (productionMining != null)
					production = string.Format("{0} ({1} with Mining)", production, productionMining);
				this.DrawText(string.Format(productionFormat, production), 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			if (trade != null)
			{
				if (tradeRoads != null)
					trade = string.Format("{0} ({1} with Roads)", trade, tradeRoads);
				this.DrawText(string.Format(tradeFormat, trade), 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			if (food == null && production == null && trade == null)
			{
				this.DrawText("nothing", 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			y += 4;
		}

		private void DrawTerrainText(ref int yy)
		{
			ITile tile = (ITile)_singlePage;
			int move = 1, defense = 0;

			switch (tile.Type)
			{
				case Terrain.Arctic:
					DrawTerrainTextValues(ref yy, "Arctic");
					DrawTerrainTextValues(ref yy, "Seals", "2");
					move = 2;
					break;
				case Terrain.Desert:
					DrawTerrainTextValues(ref yy, "Desert", "0", "1", "0", "1", "2", "1%");
					DrawTerrainTextValues(ref yy, "Oasis", "3*", "1", "0", "4*", "2", "1%");
					break;
				case Terrain.Forest:
					DrawTerrainTextValues(ref yy, "Forest", "1", "2");
					DrawTerrainTextValues(ref yy, "Game", "3*", "2");
					move = 2;
					defense = 50;
					break;
				case Terrain.Grassland1:
				case Terrain.Grassland2:
					DrawTerrainTextValues(ref yy, "Grassland", "2", "0/1", "0", "3*", null, "1%");
					break;
				case Terrain.Hills:
					DrawTerrainTextValues(ref yy, "Hills", "1", "0", null, "2", "3*");
					DrawTerrainTextValues(ref yy, "Coal", "1", "2", null, "2", "5*");
					move = 2;
					defense = 100;
					break;
				case Terrain.Jungle:
					DrawTerrainTextValues(ref yy, "Jungle", "1");
					DrawTerrainTextValues(ref yy, "Gems", "1", null, "4%*");
					move = 2;
					defense = 50;
					break;
				case Terrain.Mountains:
					DrawTerrainTextValues(ref yy, "Mountains", null, "1", null, null, "2");
					DrawTerrainTextValues(ref yy, "Gold", null, "1", "6%*", null, "2");
					move = 3;
					defense = 200;
					break;
				case Terrain.Ocean:
					DrawTerrainTextValues(ref yy, "Ocean", "1", null, "2%");
					DrawTerrainTextValues(ref yy, "Fish", "3*", null, "2%");
					break;
				case Terrain.Plains:
					DrawTerrainTextValues(ref yy, "Plains", "1", "1", "0", "2", null, "1%");
					DrawTerrainTextValues(ref yy, "Horses", "1", "3", "0", "2", null, "1%");
					break;
				case Terrain.River:
					DrawTerrainTextValues(ref yy, "River", "2", "0/1", "1%", "3*");
					defense = 50;
					break;
				case Terrain.Swamp:
					DrawTerrainTextValues(ref yy, "Swamp", "1");
					DrawTerrainTextValues(ref yy, "Oil", "1", "4");
					move = 2;
					defense = 50;
					break;
				case Terrain.Tundra:
					DrawTerrainTextValues(ref yy, "Tundra", "1");
					DrawTerrainTextValues(ref yy, "Game", "3*");
					break;
			}

			this.DrawText("*  -1 if government is Despotism/Anarchy.", 0, CassetteTheme.INK_LOW, OX + 16, yy); yy += 8;
			this.DrawText("%  +1 if government is Republic/Democracy.", 0, CassetteTheme.INK_LOW, OX + 16, yy); yy += 12;

			this.DrawText($"Movement cost: {move} MP", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 8;
			this.DrawText($"Defense bonus: +{defense}%", 0, CassetteTheme.INK_MID, OX + 12, yy);
		}

		public Civilopedia(ICivilopedia[] pages) : base(MouseCursor.Pointer)
		{
			Palette = BuildPalette();
			_pages = pages;
		}

		public Civilopedia(ICivilopedia page, bool discovered = false, bool icon = true)
		{
			Palette = BuildPalette();
			_discovered = discovered;
			_singlePage = page;
			if (!Game.CivilopediaText) _pageNumber++;
		}
	}
}
