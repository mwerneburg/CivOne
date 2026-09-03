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
		private readonly bool _allowCycle;
		private readonly CityMap _cityMap;

		private bool _update = true;
		private bool _mouseDown = false;
		private int _buildingsPage = 0;

		// ─── layout ──────────────────────────────────────────────────────────────

		private const int Margin = 2;
		private const int ColGap = 2;

		private int HeaderH    => 35;
		private int CitizenSlotH => 26;
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
		private int GarrisonH    => 46;
		private int BuildingsY   => BodyY + NowBuildingH + ColGap;
		private int GarrisonY    => BodyY + BodyH - GarrisonH;
		private int BuildingsH   => BodyH - NowBuildingH - ColGap - GarrisonH - ColGap;

		// Row height for building list entries (font 0)
		private int BuildingRowH => 9;

		// How many buildings fit on one page
		private int BuildingPageSize => Math.Max(1, (BuildingsH - 14) / BuildingRowH);

		// All wonders + buildings in display order. Infrastructure Bond floats to the top
		// of the buildings list because it's the high-traffic "sell to fund X" lever.
		private IProduction[] Improvements =>
			_city.Wonders.Cast<IProduction>()
				.Concat(_city.Buildings
					.OrderBy(b => b is Buildings.InfrastructureBond ? 0 : 1)
					.ThenBy(b => b.Id)
					.Cast<IProduction>())
				.ToArray();

		// ─── draw ────────────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			bool mapUpdated = _cityMap.Update(gameTick);
			if (!_update && !mapUpdated && !ProductionInvalid) return false;

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
			int citizenY  = hy + (HeaderH - CitizenSlotH) / 2;
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
				this.DrawCitizenToken(citizens[i], cxx, citizenY, 8, CitizenSlotH);
				cxx += 8;
			}
		}

		private void DrawResources()
		{
			int px = ColLeftX;
			int py = BodyY;
			int pw = ColLeftW;
			int fh = Resources.GetFontHeight(0);

			// Calculate panel height: 3 meters (each: label fh + 2 + bar 4 = fh+6) + divider + 5 fields (each fh+4)
			int meterH = fh + 6;
			int fieldH = fh + 4;
			int ph     = 8 + 3 * meterH + 2 + 6 * fieldH + 4;
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
			int prodCost = _city.ProductionCost(_city.CurrentProduction);
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
			byte corrColor = _city.Corruption > 0 ? CassetteTheme.PHOS : CassetteTheme.INK_MID;
			this.DrawCassetteField("CORRUPTION", corrVal, cx, cy, cw, 0, corrColor);
			cy += fieldH;

			// Upkeep field (shield costs)
			this.DrawCassetteField("UPKEEP", $"{_city.ShieldCosts} SHLD", cx, cy, cw);
			cy += fieldH;

			// Treasury field (gold). The empire's balance sheet lives on the last page of the
			// Trade Report; this is the per-city half of it, so "which city is bleeding me"
			// is answerable without paging through a report.
			int net = _city.NetGold;
			byte netColor = net < 0 ? CassetteTheme.ALERT
			              : net > 0 ? CassetteTheme.OK
			                        : CassetteTheme.INK_MID;
			this.DrawCassetteField("TREASURY", net >= 0 ? $"+{net} GOLD" : $"{net} GOLD",
				cx, cy, cw, 0, netColor);
			cy += fieldH;

			// Pollution field
			int smokeStacks = _city.SmokeStacks;
			string pollVal = smokeStacks > 0 ? $"{smokeStacks} TONS" : "NONE";
			byte pollColor = smokeStacks > 0 ? CassetteTheme.ALERT : CassetteTheme.INK_MID;
			this.DrawCassetteField("POLLUTION", pollVal, cx, cy, cw, 0, pollColor);
			cy += fieldH;

			// Governor field — click it, or press G, to cycle. Off by default and shown that
			// way: this is the only thing on the screen that will move a citizen without being
			// asked, so it says plainly whether it is armed.
			bool ownCity = !_viewCity && _city.Player == Game.HumanPlayer;
			string govVal = GovernorLabel(_city);
			byte govColor = (_city.GovernorOrder || _city.GovernorGrowth)
				? CassetteTheme.OK
				: CassetteTheme.INK_MID;
			this.DrawCassetteField("GOVERNOR", ownCity ? govVal : "OFF", cx, cy, cw, 0,
				ownCity ? govColor : CassetteTheme.INK_LOW);
		}

		internal static string GovernorLabel(City city)
			=> (city.GovernorOrder, city.GovernorGrowth) switch
			{
				(true,  true)  => "BOTH",
				(true,  false) => "ORDER",
				(false, true)  => "GROWTH",
				_              => "OFF",
			};

		// OFF -> GROWTH -> ORDER -> BOTH -> OFF. One control rather than two, because the
		// panel is a column of single-line fields and a four-step cycle reaches every
		// combination in at most three presses.
		//
		// GROWTH comes first deliberately. "This city is capped at 7, stop farming for
		// nothing" is a fact about the rules that a player can check at a glance. ORDER
		// silently changes what a city PRODUCES to quell a malcontent, which is a strategy
        // decision — so it is never what you get by tapping the control once.
		private bool CycleGovernor()
		{
			if (_viewCity || _city.Player != Game.HumanPlayer) return true;
			(bool order, bool growth) = (_city.GovernorOrder, _city.GovernorGrowth) switch
			{
				(false, false) => (false, true),
				(false, true)  => (true,  false),
				(true,  false) => (true,  true),
				_              => (false, false),
			};
			_city.GovernorOrder  = order;
			_city.GovernorGrowth = growth;
			_update = true;
			return true;
		}

		// The panel grows a line per route, and MOOD sits directly beneath it — so an uncapped
		// route list would push both off the bottom of the column and the guard above would
		// hide them entirely. The DATA is uncapped now; the display shows the most valuable
		// few and counts the rest.
		private const int TradeRoutesShown = 4;

		private int TradePanelLines() =>
			Math.Min(_city.TradeRouteCount, TradeRoutesShown)
			+ (_city.TradeRouteCount > TradeRoutesShown ? 1 : 0);

		private int TradePanelHeight(int fh)
		{
			int lines = Math.Max(1, TradePanelLines());
			return 8 + lines * fh + 8;
		}

		private void DrawTradeRoutes()
		{
			// Position below the resources panel
			int fh = Resources.GetFontHeight(0);
			int meterH = fh + 6;
			int fieldH = fh + 4;
			int resourcesPh = 8 + 3 * meterH + 2 + 6 * fieldH + 4;

			int px = ColLeftX;
			int py = BodyY + resourcesPh + ColGap;
			int pw = ColLeftW;
			int ph = TradePanelHeight(fh);

			if (py + ph > BodyY + BodyH) return;
			this.DrawCassettePanel(px, py, pw, ph, "TRADE");

			var routes = _city.TradeRoutes.OrderByDescending(r => r.Value).ToArray();
			if (routes.Length == 0)
			{
				this.DrawText("NONE", 0, CassetteTheme.INK_LOW, px + 4, py + 8);
				return;
			}
			int shown = Math.Min(routes.Length, TradeRoutesShown);
			for (int i = 0; i < shown; i++)
			{
				string name = $"{routes[i].Partner.Name.ToUpper()} ({routes[i].Value})";
				this.DrawText(name, 0, CassetteTheme.OK, px + 4, py + 8 + i * fh);
			}
			if (routes.Length > shown)
				this.DrawText($"+{routes.Length - shown} MORE ({routes.Skip(shown).Sum(r => r.Value)})",
					0, CassetteTheme.INK_LOW, px + 4, py + 8 + shown * fh);
		}

		private void DrawHappiness()
		{
			int fh = Resources.GetFontHeight(0);
			int meterH = fh + 6;
			int fieldH = fh + 4;
			int resourcesPh = 8 + 3 * meterH + 2 + 6 * fieldH + 4;
			int tradePh     = TradePanelHeight(fh);

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

			if (_city.IsInDisorder)
			{
				this.DrawText("DISORDER", 0, CassetteTheme.ALERT,
					cx + cw, py + ph - fh - 4, TextAlign.Right);
			}
			else if (_city.WasWeLoveKing)
			{
				this.DrawText("♥ THE KING", 0, CassetteTheme.PHOS,
					cx + cw, py + ph - fh - 4, TextAlign.Right);
			}
			else
			{
				this.DrawText("STABLE", 0, CassetteTheme.OK,
					cx + cw, py + ph - fh - 4, TextAlign.Right);
			}
		}

		private void DrawMapColumn()
		{
			int fh0 = Resources.GetFontHeight(0);

			int px = ColCenterX;
			int py = BodyY;
			int pw = ColCenterW;
			int mapPanelH = ColCenterW + 8;
			this.DrawCassettePanel(px, py, pw, mapPanelH, "TILES");
			this.AddLayer(_cityMap, px + 1, py + 7);

			int rateY = py + mapPanelH + ColGap;
			int rateH = BodyY + BodyH - rateY;
			if (rateH < 14) return;
			this.DrawCassettePanel(px, rateY, pw, rateH, "TRADE");

			int taxRate = _city.Player?.TaxesRate   ?? 0;
			int luxRate = _city.Player?.LuxuriesRate ?? 0;
			int sciRate = 10 - taxRate - luxRate;

			int rowX = px + 4;
			int rowW = pw - 8;
			int rowY = rateY + 8;

			// The rate is the empire slider; the output is what THIS city yields, so
			// cycling a specialist visibly moves its own row by 2.
			(string label, int rate, int output, byte color)[] rows =
			{
				("TAX",     taxRate * 10, _city.Taxes,     CassetteTheme.PHOS_DIM),
				("SCIENCE", sciRate * 10, _city.Science,   CassetteTheme.OK),
				("LUXURY",  luxRate * 10, _city.Luxuries,  CassetteTheme.CYAN),
			};
			foreach (var (label, rate, output, color) in rows)
			{
				if (rowY + fh0 > rateY + rateH - 13) break;
				this.DrawText(label,        0, CassetteTheme.INK_MID, rowX,             rowY);
				this.DrawText($"{rate}%",   0, CassetteTheme.INK_LOW, rowX + rowW - 22, rowY, TextAlign.Right);
				this.DrawText($"{output}",  0, color,                 rowX + rowW,      rowY, TextAlign.Right);
				rowY += fh0 + 1;
			}

			DrawButton("VIEW", 0, CassetteTheme.PHOS_DIM, CassetteTheme.BG3,
				px + pw - 72, rateY + rateH - 13, 34, 11);
			DrawButton("MAP", 0, CassetteTheme.PHOS_DIM, CassetteTheme.BG3,
				px + pw - 36, rateY + rateH - 13, 34, 11);
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

			// Production name, and the material the empire is short of for it — the
			// meter below is silently 50% longer without it, which looks like the item
			// simply costing more than the Civilopedia says.
			string prodName = (_city.CurrentProduction as ICivilopedia)?.Name.ToUpper() ?? "???";
			byte nameColor  = blink ? CassetteTheme.PHOS_GLOW : CassetteTheme.PHOS_DIM;
			StrategicResource missing = _city.MissingResource(_city.CurrentProduction);
			if (missing == StrategicResource.None)
				this.DrawText(prodName, 1, nameColor, px + 4, py + 7);
			else
			{
				string flag = $"NO {missing.ToString().ToUpper()}";
				// INK_MID, not ALERT: this is a notice, not a ban — the item is buildable.
				this.DrawText(flag, 0, CassetteTheme.INK_MID, px + pw - 4, py + 8, TextAlign.Right);
				// Both share one line, so the name gives way rather than run under the flag.
				int room = pw - 12 - Resources.GetTextSize(0, flag).Width;
				while (prodName.Length > 1 && Resources.GetTextSize(1, prodName).Width > room)
					prodName = $"{prodName.Substring(0, prodName.Length - 2)}.";
				this.DrawText(prodName, 1, nameColor, px + 4, py + 7);
			}

			// Progress meter
			int prodCost    = _city.ProductionCost(_city.CurrentProduction);
			int meterH      = fh0 + 6;
			this.DrawCassetteMeter($"{_city.Shields}/{prodCost} SHLD", _city.Shields, Math.Max(1, prodCost),
				px + 4, py + 7 + fh1 + 2, pw - 8);

			// Change / Buy buttons
			if (!_viewCity)
			{
				int btnY  = py + ph - 14;
				int btnW  = (pw - 10) / 2;
				byte chgColor = blink ? CassetteTheme.PHOS : CassetteTheme.PHOS_DIM;
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
			int sellW = hasSold ? 0 : Resources.GetTextSize(0, "SELL").Width + 4;

			for (int i = pageStart; i < items.Length && i < pageStart + pageSize; i++)
			{
				if (cy + BuildingRowH > py + ph - 2) break;

				IProduction item = items[i];
				bool isWonder = item is IWonder;
				byte nameCol  = isWonder ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_HIGH;

				string name = ((item as ICivilopedia)?.Name ?? "?").ToUpper();
				int maxNameW = pw - 8 - (isWonder ? 0 : sellW);
				while (Resources.GetTextSize(0, name).Width > maxNameW)
					name = name.Substring(0, name.Length - 1);

				this.DrawText(name, 0, nameCol, px + 4, cy);

				if (!isWonder && !hasSold)
				{
					this.DrawText("SELL", 0, CassetteTheme.INK_LOW, px + pw - 2, cy, TextAlign.Right);
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

			const int IconSize = 32;
			int ux = px + 2;
			foreach (IUnit unit in present)
			{
				if (ux + IconSize > px + pw - 2) break;
				DrawGarrisonUnit(unit, ux, py + 7);
				if (unit.Sentry || unit.Fortify)
					this.FillRectangle(ux, py + 7, 6, 6, CassetteTheme.INK_LOW);
				ux += IconSize + 1;
			}

			// Remote units: show after a gap with a cyan corner tick
			if (remote.Length > 0 && present.Length > 0) ux += 4;
			foreach (IUnit unit in remote)
			{
				if (ux + IconSize > px + pw - 2) break;
				DrawGarrisonUnit(unit, ux, py + 7);
				this.FillRectangle(ux + 26, py + 7, 6, 6, CassetteTheme.CYAN);
				ux += IconSize + 1;
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

		// The map sprite, doubled. The garrison panel used to have its own art stack
		// (garrison_icons/*.png, then CustomUnitIcons); the two sets disagreed on screen.
		// internal so GarrisonIconTests can compare it against the map sprite.
		internal static Bytemap GarrisonIcon(IUnit unit) => unit.ToBitmap().Scale(2);

		private void DrawGarrisonUnit(IUnit unit, int x, int y)
		{
			using (Bytemap scaled = GarrisonIcon(unit))
				this.AddLayer(scaled, x, y);
		}

		private void ForceUpdate(object sender, EventArgs args) => _update = true;

		private void AcceptBuy(object sender, EventArgs args)
		{
			_city.Buy();
			_update = true;
		}

		private void SellBuilding(object sender, EventArgs args)
		{
			_city.SellBuilding((sender as ConfirmSell)!.Building);
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
			int gold      = Game.CurrentPlayer.Gold;
			short price   = _city.BuyPrice;
			if (price <= 0)
				return true; // already complete, or will finish next turn unaided — nothing to buy
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
				Game.CityNames[_city.NameId] = (s as CityName)!.Value;
				_update = true;
			};
			Common.AddScreen(nameDialog);
			return true;
		}

		private void CloseScreen()
		{
			// A player who just quelled a riot in here has changed what the city's tile should
			// look like, and nothing else would notice until the next unit move forced a
			// repaint. See City.RefreshTileIfAppearanceChanged.
			_city.RefreshTileIfAppearanceChanged();
			Destroy();
		}

		// ─── hit testing ─────────────────────────────────────────────────────────

		private Rectangle RenameRect    => new Rectangle(Margin, Margin, BodyW / 2, HeaderH);
		private Rectangle MapRect       => new Rectangle(ColCenterX + 1, BodyY + 7, ColCenterW, ColCenterW);
		private Rectangle HeaderRect    => new Rectangle(Margin, Margin, BodyW, HeaderH);
		private Rectangle MapButtonRect
		{
			get
			{
				int rateY = BodyY + ColCenterW + 8 + ColGap;
				int rateH = BodyY + BodyH - rateY;
				return new Rectangle(ColCenterX + ColCenterW - 36, rateY + rateH - 13, 34, 11);
			}
		}
		private Rectangle ViewButtonRect
		{
			get
			{
				int rateY = BodyY + ColCenterW + 8 + ColGap;
				int rateH = BodyY + BodyH - rateY;
				return new Rectangle(ColCenterX + ColCenterW - 72, rateY + rateH - 13, 34, 11);
			}
		}
		private Rectangle GovernorRect
		{
			get
			{
				int fh = Resources.GetFontHeight(0);
				int fieldH = fh + 4;
				int meterH = fh + 6;
				int top = BodyY + 8 + 3 * meterH + 2 + 5 * fieldH;
				return new Rectangle(ColLeftX + 4, top, ColLeftW - 8, fieldH);
			}
		}
		private Rectangle ChangeRect    => new Rectangle(ColRightX + 2, BodyY + NowBuildingH - 14, (ColRightW - 10) / 2, 11);
		private Rectangle BuyRect       => new Rectangle(ColRightX + 4 + (ColRightW - 10) / 2, BodyY + NowBuildingH - 14, (ColRightW - 10) / 2, 11);
		private Rectangle BuildingsRect => new Rectangle(ColRightX, BuildingsY, ColRightW, BuildingsH);
		private Rectangle GarrisonRect  => new Rectangle(ColRightX, GarrisonY, ColRightW, GarrisonH);

		// Citizen x positions recomputed for header click tests
		private int CitizenHeaderX0 => Margin + (BodyW - _city.Size * 8) / 2;
		private int CitizenHeaderY  => Margin + (HeaderH - CitizenSlotH) / 2;

		// ─── events ──────────────────────────────────────────────────────────────

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (_allowCycle)
			{
				switch (args.Key)
				{
					case Key.Left:  return NavigateCity(-1);
					case Key.Right: return NavigateCity(+1);
				}
			}
			switch (args.KeyChar)
			{
				case 'B': if (!_viewCity) return OpenBuy();    break;
				case 'C': if (!_viewCity) return OpenChange(); break;
				case 'R': if (!_viewCity) return OpenRename(); break;
				case 'G': if (!_viewCity) return CycleGovernor(); break;
			}
			CloseScreen();
			return true;
		}

		private bool NavigateCity(int direction)
		{
			Player owner = _city.Player;
			if (owner is null) return true;
			City[] cities = owner.Cities;
			if (cities.Length < 2) return true;
			int idx = Array.IndexOf(cities, _city);
			if (idx < 0) return true;
			int next = (idx + direction + cities.Length) % cities.Length;
			Destroy();
			Common.AddScreen(new CityManager(cities[next], _viewCity, _allowCycle));
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			_mouseDown = true;

			// Governor field click → cycle
			if (!_viewCity && GovernorRect.Contains(args.Location))
				return CycleGovernor();

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
				if (args.Y >= CitizenHeaderY && args.Y < CitizenHeaderY + CitizenSlotH)
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
					.Take((ColRightW - 4) / 33)
					.ToArray();
				for (int i = 0; i < units.Length; i++)
				{
					int ux = ColRightX + 2 + i * 33;
					if (ux + 32 > ColRightX + ColRightW - 2) break;
					var unitRect = new Rectangle(ux, GarrisonY + 7, 32, 32);
					if (unitRect.Contains(args.Location))
					{
						units[i].Busy      = false;
						units[i].MovesLeft = units[i].Move;
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
						int sw = Resources.GetTextSize(0, "SELL").Width + 4;
						var sellRect = new Rectangle(ColRightX + ColRightW - sw - 2, cy - 1, sw + 2, BuildingRowH);
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

			// MAP / VIEW buttons in RATES panel
			if (MapButtonRect.Contains(args.Location))  return true;
			if (ViewButtonRect.Contains(args.Location)) return true;

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

			// MAP button → full-screen unit map
			if (MapButtonRect.Contains(args.Location))
			{
				Common.AddScreen(new CityUnitMap(_city));
				return true;
			}

			// VIEW button → cityscape panorama
			if (ViewButtonRect.Contains(args.Location))
			{
				Common.AddScreen(new CityView(_city, viewOnly: true));
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

		public CityManager(City city, bool viewCity = false, bool allowCycle = true) : base(MouseCursor.Pointer)
		{
			_viewCity   = viewCity;
			_allowCycle = allowCycle;
			_city       = city;
			_cityMap    = new CityMap(_city);

			using Palette p = Common.DefaultPalette;
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
