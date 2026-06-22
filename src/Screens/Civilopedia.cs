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
using System.Reflection;
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

		private readonly ICivilopedia[] _pages = null!;
		private readonly ICivilopedia _singlePage = null!;
		private readonly bool _discovered;

		// True only when this Civilopedia was opened as a tech-discovery notification
		// (ProcessScience does this). Autopilot uses this to distinguish auto-dismissable
		// discovery popups from the player-initiated reference reader.
		internal bool IsDiscoveryNotification => _discovered;

		private bool _update = true;
		private int _startIndex = 0;
		private byte _pageNumber = 1;

		private int OX => (Width - 320) / 2;

		private struct PageLink
		{
			public int X, Y, W;
			public ICivilopedia Target;
		}
		private readonly List<PageLink> _links = new();

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
			if (_singlePage is BaseConcept bc)
				return bc.GetPageText(pageNumber);
			if (_singlePage is IBuilding || _singlePage is IWonder)
			{
				if (_singlePage is BaseBuilding bb)
				{
					string[] custom = bb.GetPageText(pageNumber);
					if (custom.Length > 0) return custom;
				}
				if (_singlePage is BaseWonder bw)
				{
					string[] custom = bw.GetPageText(pageNumber);
					if (custom.Length > 0) return custom;
				}
				return Resources.GetCivilopediaText("BLURB1/" + _singlePage.Name.ToUpper() + suffix);
			}
			if (_singlePage is IUnit)
			{
				if (_singlePage is Units.BaseUnit bu)
				{
					string[] custom = bu.GetPageText(pageNumber);
					if (custom.Length > 0) return custom;
				}
				return Resources.GetCivilopediaText("BLURB2/" + _singlePage.Name.ToUpper() + suffix);
			}
			if (_singlePage is IAdvance)
			{
				if (_singlePage is BasePostContactAdvance bpca)
				{
					string[] custom = bpca.GetPageText(pageNumber);
					if (custom.Length > 0) return custom;
				}
				return Resources.GetCivilopediaText("BLURB0/" + _singlePage.Name.ToUpper() + suffix);
			}
			if (_singlePage is IConcept)
				return Resources.GetCivilopediaText("BLURB4/" + _singlePage.Name.ToUpper() + suffix);
			return new string[0];
		}

		// Draw a linked text item in CYAN and record its hit region.
		private void DrawLink(int x, int y, string text, ICivilopedia target)
		{
			this.DrawText(text, 0, CassetteTheme.CYAN, x, y);
			_links.Add(new PageLink { X = x, Y = y, W = Resources.GetTextSize(0, text).Width, Target = target });
		}

		// Draw a "Label: " prefix in INK_MID, then the linked value in CYAN on the same line.
		private void DrawLabelLink(int x, int y, string label, string value, ICivilopedia target)
		{
			this.DrawText(label, 0, CassetteTheme.INK_MID, x, y);
			int offset = Resources.GetTextSize(0, label).Width;
			DrawLink(x + offset, y, value, target);
		}

		private void DrawHeader()
		{
			this.FillRectangle(0, 0, Width, 27, CassetteTheme.BG3)
				.FillRectangle(0, 27, Width, 1, CassetteTheme.BORDER);

			if (_singlePage is null)
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

			// With "Civilopedia Text" off we open straight on the stats page (page 2, skipping
			// the verbose page 1 — see the constructor). But every wonder/building/unit keeps
			// "what it does" on page 1 and its historical flavour on page 2, so that skip would
			// show only backstory + stats and never the gameplay effect. Pull the page-1 (effect)
			// blurb onto the stats page instead, so a quick look always tells you what it does.
			byte blurbPage = (!Game.CivilopediaText && _pageNumber == 2) ? (byte)1 : _pageNumber;
			string[] blurb = GetBlurb(blurbPage);
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
				if (b.RequiredTech is not null)
					DrawLabelLink(OX + 12, yy, "Requires: ", b.RequiredTech.Name, b.RequiredTech as ICivilopedia);
				else
					this.DrawText("Requires: (none)", 0, CassetteTheme.INK_MID, OX + 12, yy);
				yy += 9;
				this.DrawText($"Cost: {b.Price * 10} shields", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Maintenance: ${b.Maintenance}/turn", 0, CassetteTheme.INK_MID, OX + 12, yy);
				return;
			}
			if (_singlePage is BaseWonder w)
			{
				if (w.RequiredTech is not null)
					DrawLabelLink(OX + 12, yy, "Requires: ", w.RequiredTech.Name, w.RequiredTech as ICivilopedia);
				else
					this.DrawText("Requires: (none)", 0, CassetteTheme.INK_MID, OX + 12, yy);
				yy += 9;
				this.DrawText($"Cost: {w.Price * 10} shields", 0, CassetteTheme.INK_MID, OX + 12, yy);
				if (w.ObsoleteTech is not null)
				{
					yy += 9;
					DrawLabelLink(OX + 12, yy, "Obsolete with: ", w.ObsoleteTech.Name, w.ObsoleteTech as ICivilopedia);
				}
				return;
			}
			if (_singlePage is IUnit u)
			{
				if (u.RequiredTech is not null)
					DrawLabelLink(OX + 12, yy, "Requires: ", u.RequiredTech.Name, u.RequiredTech as ICivilopedia);
				else
					this.DrawText("Requires: (none)", 0, CassetteTheme.INK_MID, OX + 12, yy);
				yy += 9;
				this.DrawText($"Cost: {u.Price * 10} resources", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
				this.DrawText($"Attack: {u.Attack}   Defense: {u.Defense}   Move: {u.Move}", 0, CassetteTheme.INK_MID, OX + 12, yy);
				if (u.ObsoleteTech is not null)
				{
					yy += 9;
					DrawLabelLink(OX + 12, yy, "Obsolete with: ", u.ObsoleteTech.Name, u.ObsoleteTech as ICivilopedia);
				}
				return;
			}
			if (_singlePage is IAdvance adv)
			{
				if (adv.RequiredTechs.Length > 0)
				{
					this.DrawText("Requires:", 0, CassetteTheme.INK_MID, OX + 12, yy); yy += 9;
					foreach (IAdvance req in adv.RequiredTechs)
					{
						DrawLink(OX + 20, yy, req.Name, req as ICivilopedia);
						yy += 9;
					}
				}
				yy += 4;
				this.DrawText("Allows:", 0, CassetteTheme.INK_HIGH, OX + 12, yy); yy += 9;
				foreach (IAdvance tech in Common.Advances.Where(a => a.Requires(adv.Id)))
				{
					DrawLink(OX + 20, yy, tech.Name, tech as ICivilopedia);
					// append non-clickable "(with X)" qualifiers
					foreach (IAdvance at in tech.RequiredTechs.Where(a => a.Id != adv.Id))
					{
						int offset = Resources.GetTextSize(0, tech.Name).Width;
						this.DrawText($" (with {at.Name})", 0, CassetteTheme.INK_LOW, OX + 20 + offset, yy);
					}
					yy += 9;
				}
				foreach (IUnit unit in Reflect.GetUnits().Where(u2 => u2.RequiredTech is not null && u2.RequiredTech.Id == adv.Id))
				{
					DrawLink(OX + 20, yy, $"{unit.Name} unit", unit as ICivilopedia);
					yy += 9;
				}
				foreach (IBuilding building in Reflect.GetBuildings().Where(bld => bld.RequiredTech is not null && bld.RequiredTech.Id == adv.Id))
				{
					DrawLink(OX + 20, yy, $"{building.Name} improvement", building as ICivilopedia);
					yy += 9;
				}
				foreach (IWonder wonder in Reflect.GetWonders().Where(wndr => wndr.RequiredTech is not null && wndr.RequiredTech.Id == adv.Id))
				{
					DrawLink(OX + 20, yy, $"{wonder.Name} Wonder", wonder as ICivilopedia);
					yy += 9;
				}
			}
		}

		private bool NextPage()
		{
			if (_singlePage is not null && _pageNumber < _singlePage.PageCount)
			{
				_pageNumber++;
				_update = true;
				return true;
			}
			return false;
		}

		private const int LIST_Y     = 32;
		private const int LIST_LINE  = 9;
		private const int LIST_PAD   = 10;

		private void GetListLayout(out int perCol, out int numCols, out int colWidth)
		{
			perCol   = Math.Max(1, (Height - LIST_Y - LIST_PAD) / LIST_LINE);
			numCols  = Math.Min(3, Math.Max(1, (int)Math.Ceiling((double)_pages.Length / perCol)));
			colWidth = (Width - 2 * LIST_PAD) / numCols;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			_links.Clear();
			this.Clear(CassetteTheme.BG0);
			DrawHeader();

			if (_singlePage is null)
			{
				GetListLayout(out int perCol, out int numCols, out int colWidth);
				int maxChars = Math.Max(8, colWidth / 6);

				int xx = LIST_PAD, yy = LIST_Y;
				for (int i = _startIndex; i < _pages.Length; i++)
				{
					string name = _pages[i].Name;
					if (name.Length > maxChars) name = name.Substring(0, maxChars - 1) + ".";
					this.DrawText(name, 0, CassetteTheme.INK_HIGH, xx, yy);
					_links.Add(new PageLink { X = xx, Y = yy, W = colWidth, Target = _pages[i] });
					yy += LIST_LINE;
					if (yy > Height - LIST_LINE - LIST_PAD)
					{
						xx += colWidth;
						if (xx >= LIST_PAD + numCols * colWidth) break;
						yy = LIST_Y;
					}
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
			if (_singlePage is not null && NextPage()) return true;
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_singlePage is not null)
			{
				// Check links before paging or closing
				foreach (PageLink link in _links)
				{
					if (args.X >= link.X && args.X <= link.X + link.W &&
					    args.Y >= link.Y && args.Y < link.Y + 9)
					{
						Common.AddScreen(new Civilopedia(link.Target));
						return true;
					}
				}
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

			foreach (PageLink link in _links)
			{
				if (args.X >= link.X && args.X < link.X + link.W &&
				    args.Y >= link.Y && args.Y < link.Y + LIST_LINE)
				{
					Common.AddScreen(new Civilopedia(link.Target));
					return true;
				}
			}
			return false;
		}

		private void DrawTerrainTextValues(ref int y, string name, string? food = null, string? production = null, string? trade = null, string? foodIrrigation = null, string? productionMining = null, string? tradeRoads = null)
		{

			this.DrawText(name, 0, CassetteTheme.INK_HIGH, OX + 12, y);
			y += 8;
			if (food is not null)
			{
				if (foodIrrigation is not null)
					food = $"{food} ({foodIrrigation} with Irrigation)";
				this.DrawText($"Food: {food} units.", 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			if (production is not null)
			{
				if (productionMining is not null)
					production = $"{production} ({productionMining} with Mining)";
				this.DrawText($"Production: {production} units.", 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			if (trade is not null)
			{
				if (tradeRoads is not null)
					trade = $"{trade} ({tradeRoads} with Roads)";
				this.DrawText($"Trade: {trade}", 0, CassetteTheme.INK_MID, OX + 16, y);
				y += 8;
			}
			if (food is null && production is null && trade is null)
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
					DrawTerrainTextValues(ref yy, "Wetland", "1");
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
			using Palette pal = BuildPalette(); Palette = pal;
			_pages = pages;
		}

		public Civilopedia(ICivilopedia page, bool discovered = false, bool icon = true) : base(MouseCursor.Pointer)
		{
			using Palette pal = BuildPalette(); Palette = pal;
			_discovered = discovered;
			_singlePage = page;
			if (!Game.CivilopediaText) _pageNumber++;
		}
	}
}
