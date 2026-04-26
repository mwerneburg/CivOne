// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Drawing;
using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;
using CivOne.Screens.CityManagerPanels;
using CivOne.Screens.Dialogs;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne.Screens
{
	[Expand]
	internal class CityManager : BaseScreen
	{
		private readonly City _city;
		private readonly bool _viewCity;
		private readonly CityMap _cityMap;

		private bool _update = true;
		private bool _mouseDown = false;
		private int _buildingsPage = 0;

		// ─── layout ──────────────────────────────────────────────────────────────

		private const int Margin = 2;
		private const int ColGap = 2;

		private int HeaderH => 22;
		private int BodyX   => Margin;
		private int BodyY   => Margin + HeaderH + 2;
		private int BodyW   => Width  - 2 * Margin;
		private int BodyH   => Height - BodyY - Margin;

		// Center column width snapped to 80k+2 so _tileSize is always a multiple of 16
		// (prevents misalignment between tile bitmap and resource icon positions)
		private int ColCenterW
		{
			get
			{
				int raw = Math.Max(82, (BodyW - 2 * ColGap) * 26 / 100);
				int k   = Math.Max(1, (raw - 2) / 80);
				return k * 80 + 2;
			}
		}
		private int ColLeftW    => Math.Max(88, (BodyW - ColCenterW - 2 * ColGap) * 45 / 100);
		private int ColRightW   => BodyW - ColLeftW - ColCenterW - 2 * ColGap;
		private int ColLeftX    => BodyX;
		private int ColCenterX  => ColLeftX + ColLeftW + ColGap;
		private int ColRightX   => ColCenterX + ColCenterW + ColGap;

		// Panel heights in the right column
		private int NowBuildingH => 58;
		private int GarrisonH    => 28;
		private int BuildingsY   => BodyY + NowBuildingH + ColGap;
		private int GarrisonY    => BodyY + BodyH - GarrisonH;
		private int BuildingsH   => BodyH - NowBuildingH - ColGap - GarrisonH - ColGap;

		// Row height for building list entries (font 0)
		private int BuildingRowH => 9;

		// How many buildings fit on one page
		private int BuildingPageSize => Math.Max(1, (BuildingsH - 14) / BuildingRowH);

		// All wonders + buildings in display order
		private IProduction[] Improvements =>
			_city.Wonders.Cast<IProduction>().Concat(_city.Buildings.Cast<IProduction>()).ToArray();

		// ─── draw ────────────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			bool mapUpdated = _cityMap.Update(gameTick);
			if (!_update && !mapUpdated) return false;

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);

			DrawHeader();
			DrawResources();
			DrawTradeRoutes();
			DrawHappiness();
			DrawMapColumn();
			DrawNowBuilding(gameTick);
			DrawBuildingsList();
			DrawGarrison();

			_update = false;
			return true;
		}

		private void DrawHeader()
		{
			int hx = Margin;
			int hy = Margin;
			int hw = BodyW;
			this.DrawCassettePanel(hx, hy, hw, HeaderH);

			int fh0 = Resources.GetFontHeight(0);
			int fh1 = Resources.GetFontHeight(1);

			// Left: city name (clickable → rename) + empire/pop subtitle
			string empire = _city.Player?.Civilization?.NamePlural ?? "UNKNOWN";
			string pop    = Common.NumberSeperator(_city.Population);
			this.DrawText(_city.Name.ToUpper(), 1, CassetteTheme.PHOS,       hx + 6, hy + 3);
			this.DrawText($"{empire} · POP {pop}", 0, CassetteTheme.INK_MID, hx + 6, hy + 3 + fh1 + 2);

			// Right: rename hint + ESC
			if (!_viewCity)
			{
				string renameHint = "R-RENAME";
				int rnW = Resources.GetTextSize(0, renameHint).Width + 4;
				this.DrawText(renameHint, 0, CassetteTheme.INK_LOW, hx + hw - rnW - 36, hy + (HeaderH - fh0) / 2);
			}
			string escLabel = "ESC";
			int escW = Resources.GetTextSize(0, escLabel).Width + 8;
			this.DrawText(escLabel, 0, CassetteTheme.INK_MID, hx + hw - escW, hy + (HeaderH - fh0) / 2);

			// Center: citizen icon strip, aligned to center of the header
			int citizenW  = _city.Size * 8;
			int citizenX0 = hx + (hw - citizenW) / 2;
			int citizenY  = hy + (HeaderH - 14) / 2;
			Citizen[] citizens = _city.Citizens.ToArray();
			int cxx = citizenX0;
			int group = -1;
			for (int i = 0; i < _city.Size; i++)
			{
				if (group != (group = Common.CitizenGroup(citizens[i])) && group > 0 && i > 0)
				{
					cxx += 2;
					if (group == 3) cxx += 4;
				}
				this.AddLayer(Icons.Citizen(citizens[i]), cxx, citizenY);
				cxx += 8;
			}
		}

		private void DrawResources()
		{
			int px = ColLeftX;
			int py = BodyY;
			int pw = ColLeftW;
			int fh = Resources.GetFontHeight(0);

			// Calculate panel height: 3 meters (each: label fh + 2 + bar 4 = fh+6) + divider + 3 fields (each fh+4)
			int meterH = fh + 6;
			int fieldH = fh + 4;
			int ph     = 8 + 3 * meterH + 2 + 3 * fieldH + 4;
			this.DrawCassettePanel(px, py, pw, ph, "RESOURCES");

			int cx = px + 4;
			int cw = pw - 8;
			int cy = py + 8;

			// Food meter: storage bar
			int foodIncome = _city.FoodIncome;
			string foodLabel = foodIncome >= 0 ? $"+{foodIncome} FOOD" : $"{foodIncome} FOOD";
			this.DrawCassetteMeter(foodLabel, _city.Food, Math.Max(1, _city.FoodRequired), cx, cy, cw);
			cy += meterH;

			// Shields meter: production progress
			int shieldIncome = _city.ShieldIncome;
			string shldLabel = shieldIncome >= 0 ? $"+{shieldIncome} PROD" : $"{shieldIncome} PROD";
			int prodCost = (int)_city.CurrentProduction.Price * 10;
			this.DrawCassetteMeter(shldLabel, _city.Shields, Math.Max(1, prodCost), cx, cy, cw);
			cy += meterH;

			// Trade meter
			this.DrawCassetteMeter($"+{_city.TradeTotal} TRADE", _city.TradeTotal, Math.Max(1, _city.TradeTotal + 4), cx, cy, cw);
			cy += meterH;

			this.DrawCassetteDivider(cx, cy + 1, cw);
			cy += 4;

			// Growth field
			int growthTurns = (foodIncome > 0)
				? (_city.FoodRequired - _city.Food + foodIncome - 1) / foodIncome
				: 0;
			string growthVal = (foodIncome > 0) ? $"{growthTurns} TURNS" : "NONE";
			byte growthColor = (foodIncome > 0) ? CassetteTheme.OK : CassetteTheme.INK_MID;
			this.DrawCassetteField("GROWTH", growthVal, cx, cy, cw, 0, growthColor);
			cy += fieldH;

			// Corruption field
			string corrVal = _city.Corruption > 0 ? $"{_city.Corruption}" : "NONE";
			byte corrColor = _city.Corruption > 0 ? CassetteTheme.WARN : CassetteTheme.INK_MID;
			this.DrawCassetteField("CORRUPT", corrVal, cx, cy, cw, 0, corrColor);
			cy += fieldH;

			// Upkeep field (shield costs)
			this.DrawCassetteField("UPKEEP", $"{_city.ShieldCosts} SHLD", cx, cy, cw);
		}

		private void DrawTradeRoutes()
		{
			// Position below the resources panel
			int fh = Resources.GetFontHeight(0);
			int meterH = fh + 6;
			int fieldH = fh + 4;
			int resourcesPh = 8 + 3 * meterH + 2 + 3 * fieldH + 4;

			int px = ColLeftX;
			int py = BodyY + resourcesPh + ColGap;
			int pw = ColLeftW;
			int ph = 8 + fh + 8;   // single line panel

			if (py + ph > BodyY + BodyH) return;
			this.DrawCassettePanel(px, py, pw, ph, "TRADE");

			int routeCount = _city.TradeRoutes.Count();
			string routeText = routeCount == 0 ? "NONE" : $"{routeCount}/3";
			byte routeColor  = routeCount == 0 ? CassetteTheme.INK_LOW : CassetteTheme.OK;
			this.DrawText(routeText, 0, routeColor, px + 4, py + 8);
		}

		private void DrawHappiness()
		{
			int fh = Resources.GetFontHeight(0);
			int meterH = fh + 6;
			int fieldH = fh + 4;
			int resourcesPh = 8 + 3 * meterH + 2 + 3 * fieldH + 4;
			int tradePh     = 8 + fh + 8;

			int px = ColLeftX;
			int py = BodyY + resourcesPh + ColGap + tradePh + ColGap;
			int pw = ColLeftW;
			int ph = BodyY + BodyH - py;  // fill remaining space in left column

			if (ph < 14) return;
			this.DrawCassettePanel(px, py, pw, ph, "MOOD");

			int happy    = _city.HappyCitizens;
			int content  = _city.ContentCitizens;
			int unhappy  = _city.UnhappyCitizens;

			int cx = px + 4;
			int cy = py + 8;
			int cw = pw - 8;

			this.DrawCassetteField("HAPPY",   $"{happy}",   cx, cy,        cw, 0, CassetteTheme.PHOS);
			this.DrawCassetteField("CONTENT", $"{content}", cx, cy + fieldH, cw);
			if (cy + 2 * fieldH < py + ph - fh - 2)
			{
				byte alertCol = unhappy > 0 ? CassetteTheme.ALERT : CassetteTheme.INK_MID;
				this.DrawCassetteField("UNHAPPY", $"{unhappy}", cx, cy + 2 * fieldH, cw, 0, alertCol);
			}

			if (!_city.IsInDisorder)
			{
				this.DrawText("STABLE", 0, CassetteTheme.OK,
					cx + cw, py + ph - fh - 4, TextAlign.Right);
			}
			else
			{
				this.DrawText("DISORDER", 0, CassetteTheme.ALERT,
					cx + cw, py + ph - fh - 4, TextAlign.Right);
			}
		}

		private void DrawMapColumn()
		{
			int fh0 = Resources.GetFontHeight(0);

			// Panel wrapping the map
			int px = ColCenterX;
			int py = BodyY;
			int pw = ColCenterW;
			int mapPanelH = ColCenterW + 8;  // square map + label gap above
			this.DrawCassettePanel(px, py, pw, mapPanelH, "TILES");

			// Map at (px+1, py+7)
			this.AddLayer(_cityMap, px + 1, py + 7);

			// Rate bar below map panel
			int rateY = py + mapPanelH + ColGap;
			int rateH = BodyY + BodyH - rateY;
			if (rateH < 14) return;
			this.DrawCassettePanel(px, rateY, pw, rateH, "RATES");

			// Tax / Lux / Sci stacked rows
			int taxRate = _city.Player?.TaxesRate   ?? 0;
			int luxRate = _city.Player?.LuxuriesRate ?? 0;
			int sciRate = 10 - taxRate - luxRate;

			int rowX = px + 4;
			int rowW = pw - 8;
			int rowY = rateY + 8;

			(string label, int rate, byte color)[] rows =
			{
				("PRODUCTION", taxRate * 10, CassetteTheme.PHOS_DIM),
				("SCIENCE",    sciRate * 10, CassetteTheme.OK),
				("LUXURY",     luxRate * 10, CassetteTheme.CYAN),
			};
			foreach (var (label, rate, color) in rows)
			{
				if (rowY + fh0 > rateY + rateH) break;
				this.DrawText(label,          0, CassetteTheme.INK_MID, rowX,          rowY);
				this.DrawText($"{rate}%", 0, color,              rowX + rowW,   rowY, TextAlign.Right);
				rowY += fh0 + 1;
			}
		}

		private void DrawNowBuilding(uint gameTick)
		{
			int px = ColRightX;
			int py = BodyY;
			int pw = ColRightW;
			int ph = NowBuildingH;

			bool blink = ProductionInvalid && (gameTick % 4 > 1);
			this.DrawCassettePanel(px, py, pw, ph, "BUILDING");

			int fh0 = Resources.GetFontHeight(0);
			int fh1 = Resources.GetFontHeight(1);

			// Production name
			string prodName = (_city.CurrentProduction as ICivilopedia)?.Name.ToUpper() ?? "???";
			byte nameColor  = blink ? CassetteTheme.WARN : CassetteTheme.PHOS;
			this.DrawText(prodName, 1, nameColor, px + 4, py + 7);

			// Progress meter
			int prodCost    = (int)_city.CurrentProduction.Price * 10;
			int meterH      = fh0 + 6;
			this.DrawCassetteMeter($"{_city.Shields}/{prodCost} SHLD", _city.Shields, Math.Max(1, prodCost),
				px + 4, py + 7 + fh1 + 2, pw - 8);

			// Change / Buy buttons
			if (!_viewCity)
			{
				int btnY  = py + ph - 14;
				int btnW  = (pw - 10) / 2;
				byte chgColor = blink ? CassetteTheme.WARN : CassetteTheme.PHOS_DIM;
				DrawButton("CHANGE", 0, chgColor, CassetteTheme.BG3, px + 2, btnY, btnW, 11);
				DrawButton("BUY",    0, CassetteTheme.PHOS_DIM, CassetteTheme.BG3, px + 4 + btnW, btnY, btnW, 11);
			}
		}

		private void DrawBuildingsList()
		{
			int px = ColRightX;
			int py = BuildingsY;
			int pw = ColRightW;
			int ph = BuildingsH;
			if (ph < 16) return;

			IProduction[] items = Improvements;
			int pageSize = BuildingPageSize;
			int pageStart = _buildingsPage * pageSize;

			this.DrawCassettePanel(px, py, pw, ph, "BUILDINGS");

			int fh = Resources.GetFontHeight(0);
			int cy = py + 8;
			bool hasSold = _city.BuildingSold;

			for (int i = pageStart; i < items.Length && i < pageStart + pageSize; i++)
			{
				if (cy + BuildingRowH > py + ph - 2) break;

				IProduction item = items[i];
				bool isWonder = item is IWonder;
				byte nameCol  = isWonder ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;

				string name = ((item as ICivilopedia)?.Name ?? "?").ToUpper();
				while (Resources.GetTextSize(0, name).Width > pw - (isWonder ? 8 : 22))
					name = name.Substring(0, name.Length - 1);

				this.DrawText(name, 0, nameCol, px + 4, cy);

				if (!isWonder && !hasSold)
				{
					this.DrawText("SL", 0, CassetteTheme.INK_LOW, px + pw - 18, cy);
				}
				cy += BuildingRowH;
			}

			// "MORE" button if more than one page
			if (items.Length > pageSize)
			{
				int moreBtnY = py + ph - 13;
				DrawButton("MORE", 0, CassetteTheme.PHOS_DIM, CassetteTheme.BG3, px + pw - 36, moreBtnY, 34, 11);
			}
		}

		private void DrawGarrison()
		{
			int px = ColRightX;
			int py = GarrisonY;
			int pw = ColRightW;
			int ph = GarrisonH;
			this.DrawCassettePanel(px, py, pw, ph, "GARRISON");

			IUnit[] present = Game.GetUnits()
				.Where(u => u.X == _city.X && u.Y == _city.Y)
				.ToArray();
			// Units homed here but currently away (supported remotely)
			IUnit[] remote = _city.Units
				.Where(u => u.X != _city.X || u.Y != _city.Y)
				.ToArray();

			if (present.Length == 0 && remote.Length == 0)
			{
				this.DrawText("NONE", 0, CassetteTheme.INK_LOW, px + 4, py + 10);
				return;
			}

			int ux = px + 2;
			foreach (IUnit unit in present)
			{
				if (ux + 16 > px + pw - 2) break;
				this.AddLayer(unit.ToBitmap(), ux, py + 7);
				if (unit.Sentry || unit.Fortify)
					this.FillRectangle(ux, py + 7, 4, 4, CassetteTheme.INK_LOW);
				ux += 16;
			}

			// Remote units: show after a gap with a cyan corner tick
			if (remote.Length > 0 && present.Length > 0) ux += 2;
			foreach (IUnit unit in remote)
			{
				if (ux + 16 > px + pw - 2) break;
				this.AddLayer(unit.ToBitmap(), ux, py + 7);
				this.FillRectangle(ux + 12, py + 7, 4, 4, CassetteTheme.CYAN);
				ux += 16;
			}
		}

		// ─── helpers ──────────────────────────────────────────────────────────────

		private bool ProductionInvalid
		{
			get
			{
				if (_city.CurrentProduction is IBuilding b) return _city.HasBuilding(b);
				if (_city.CurrentProduction is IWonder   w) return Game.WonderBuilt(w);
				return false;
			}
		}

		private void ForceUpdate(object sender, EventArgs args) => _update = true;

		private void AcceptBuy(object sender, EventArgs args)
		{
			_city.Buy();
			_update = true;
		}

		private void SellBuilding(object sender, EventArgs args)
		{
			_city.SellBuilding((sender as ConfirmSell).Building);
			_buildingsPage = 0;
			_update = true;
		}

		private bool OpenChange()
		{
			var menu = new CityChooseProduction(_city);
			menu.Closed += ForceUpdate;
			Common.AddScreen(menu);
			return true;
		}

		private bool OpenBuy()
		{
			string name   = (_city.CurrentProduction as ICivilopedia)?.Name ?? "???";
			short gold    = Game.CurrentPlayer.Gold;
			short price   = _city.BuyPrice;
			if (gold < price)
			{
				Common.AddScreen(new MessageBox("Cost to complete", $"{name}: ${price}", $"Treasury: ${gold}"));
				return true;
			}
			var confirm = new ConfirmBuy(name, price, gold);
			confirm.Buy += AcceptBuy;
			Common.AddScreen(confirm);
			return true;
		}

		private bool OpenRename()
		{
			var nameDialog = new CityName(_city.NameId, _city.Name);
			nameDialog.Accept += (s, _) =>
			{
				Game.CityNames[_city.NameId] = (s as CityName).Value;
				_update = true;
			};
			Common.AddScreen(nameDialog);
			return true;
		}

		private void CloseScreen() => Destroy();

		// ─── hit testing ─────────────────────────────────────────────────────────

		private Rectangle RenameRect    => new Rectangle(Margin, Margin, BodyW / 2, HeaderH);
		private Rectangle MapRect       => new Rectangle(ColCenterX + 1, BodyY + 7, ColCenterW, ColCenterW);
		private Rectangle HeaderRect    => new Rectangle(Margin, Margin, BodyW, HeaderH);
		private Rectangle ChangeRect    => new Rectangle(ColRightX + 2, BodyY + NowBuildingH - 14, (ColRightW - 10) / 2, 11);
		private Rectangle BuyRect       => new Rectangle(ColRightX + 4 + (ColRightW - 10) / 2, BodyY + NowBuildingH - 14, (ColRightW - 10) / 2, 11);
		private Rectangle BuildingsRect => new Rectangle(ColRightX, BuildingsY, ColRightW, BuildingsH);
		private Rectangle GarrisonRect  => new Rectangle(ColRightX, GarrisonY, ColRightW, GarrisonH);

		// Citizen x positions recomputed for header click tests
		private int CitizenHeaderX0 => Margin + (BodyW - _city.Size * 8) / 2;
		private int CitizenHeaderY  => Margin + (HeaderH - 14) / 2;

		// ─── events ──────────────────────────────────────────────────────────────

		public override bool KeyDown(KeyboardEventArgs args)
		{
			switch (args.KeyChar)
			{
				case 'B': if (!_viewCity) return OpenBuy();    break;
				case 'C': if (!_viewCity) return OpenChange(); break;
				case 'R': if (!_viewCity) return OpenRename(); break;
			}
			CloseScreen();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			_mouseDown = true;

			// City name click → rename
			if (!_viewCity && RenameRect.Contains(args.Location)
				&& args.Y < CitizenHeaderY)
			{
				OpenRename();
				return true;
			}

			// Citizen click in header
			if (!_viewCity && HeaderRect.Contains(args.Location))
			{
				if (args.Y >= CitizenHeaderY && args.Y < CitizenHeaderY + 14)
				{
					Citizen[] citizens = _city.Citizens.ToArray();
					int cxx = CitizenHeaderX0;
					int group = -1;
					int specIndex = -1;
					for (int i = 0; i < _city.Size; i++)
					{
						if (group != (group = Common.CitizenGroup(citizens[i])) && group > 0 && i > 0)
						{
							cxx += 2;
							if (group == 3) cxx += 4;
						}
						bool isSpec = (int)citizens[i] >= 6;
						if (isSpec) specIndex++;
						if (args.X >= cxx && args.X < cxx + 8)
						{
							if (specIndex >= 0)
							{
								// Cycle this specialist's role
								_city.ChangeSpecialist(specIndex);
							}
							else
							{
								// Worker clicked: de-assign a resource tile to create a specialist
								var extra = _city.ResourceTiles
									.Where(t => t.X != _city.X || t.Y != _city.Y)
									.ToArray();
								if (extra.Length > 0)
									_city.SetResourceTile(extra[extra.Length - 1]);
							}
							_update = true;
							return true;
						}
						cxx += 8;
					}
				}
				return true;  // consume click anywhere in header (don't close screen)
			}

			// Map click: handled on MouseUp via sub-panel delegation
			if (MapRect.Contains(args.Location)) return true;

			// Change / Buy buttons
			if (!_viewCity)
			{
				if (ChangeRect.Contains(args.Location)) return true;
				if (BuyRect.Contains(args.Location))    return true;
			}

			// Garrison: click wakes the unit; close screen so the player can give orders
			if (GarrisonRect.Contains(args.Location))
			{
				IUnit[] units = Game.GetUnits()
					.Where(u => u.X == _city.X && u.Y == _city.Y)
					.Take((ColRightW - 4) / 16)
					.ToArray();
				for (int i = 0; i < units.Length; i++)
				{
					int ux = ColRightX + 2 + i * 16;
					if (ux + 16 > ColRightX + ColRightW - 2) break;
					var unitRect = new Rectangle(ux, GarrisonY + 7, 16, 14);
					if (unitRect.Contains(args.Location))
					{
						if (units[i].Sentry || units[i].Fortify)
						{
							units[i].Busy      = false;
							units[i].MovesLeft = units[i].Move;
						}
						Game.ActiveUnit = units[i];
						CloseScreen();
						return true;
					}
				}
				return true;  // consume click in garrison panel
			}

			// Buildings list: sell button
			if (!_viewCity && !_city.BuildingSold && BuildingsRect.Contains(args.Location))
			{
				int pageStart = _buildingsPage * BuildingPageSize;
				IProduction[] items = Improvements;
				int cy = BuildingsY + 8;
				for (int i = pageStart; i < items.Length && i < pageStart + BuildingPageSize; i++)
				{
					if (cy + BuildingRowH > BuildingsY + BuildingsH - 2) break;
					if (items[i] is IBuilding bldg)
					{
						// Sell button is at ColRightX + ColRightW - 18 to end, same row
						var sellRect = new Rectangle(ColRightX + ColRightW - 20, cy - 1, 18, BuildingRowH);
						if (sellRect.Contains(args.Location))
						{
							var confirm = new ConfirmSell(bldg);
							confirm.Sell += SellBuilding;
							Common.AddScreen(confirm);
							return true;
						}
					}
					cy += BuildingRowH;
				}

				// "MORE" button
				if (items.Length > BuildingPageSize)
				{
					int moreBtnY = BuildingsY + BuildingsH - 13;
					var moreRect = new Rectangle(ColRightX + ColRightW - 36, moreBtnY, 34, 11);
					if (moreRect.Contains(args.Location))
					{
						_buildingsPage = ((_buildingsPage + 1) * BuildingPageSize >= items.Length) ? 0 : _buildingsPage + 1;
						_update = true;
						return true;
					}
				}
				return true;  // consume click in buildings panel
			}

			// Consume clicks in the left and center columns (no close action there)
			if (new Rectangle(BodyX, BodyY, BodyW, BodyH).Contains(args.Location))
				return true;

			CloseScreen();
			return true;
		}

		public override bool MouseUp(ScreenEventArgs args)
		{
			if (!_mouseDown) return false;

			// Map tile click
			if (MapRect.Contains(args.Location))
			{
				ScreenEventArgs local = new ScreenEventArgs(args.X - (ColCenterX + 1), args.Y - (BodyY + 7), args.Buttons);
				_cityMap.MouseDown(local);
				_update = true;
				return true;
			}

			if (!_viewCity)
			{
				if (ChangeRect.Contains(args.Location)) return OpenChange();
				if (BuyRect.Contains(args.Location))    return OpenBuy();
			}
			return false;
		}

		// ─── resize ──────────────────────────────────────────────────────────────

		private void Resize(object sender, ResizeEventArgs args)
		{
			_cityMap.Resize(ColCenterW);
			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			_update = true;
		}

		// ─── lifecycle ───────────────────────────────────────────────────────────

		public CityManager(City city, bool viewCity = false) : base(MouseCursor.Pointer)
		{
			_viewCity = viewCity;
			_city     = city;
			_cityMap  = new CityMap(_city);

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			_cityMap.Resize(ColCenterW);
			OnResize += Resize;
		}

		public override void Dispose()
		{
			_cityMap.Dispose();
			base.Dispose();
		}
	}
}
