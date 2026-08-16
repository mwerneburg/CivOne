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
using CivOne.IO;
using CivOne.Tasks;

namespace CivOne.Screens.Reports
{
	[Expand, OwnPalette]
	internal class SpaceShips : BaseScreen
	{
		private readonly Player[] _civs;
		private int _civIdx;
		private bool _update = true;

		// ── part counts ──────────────────────────────────────────────────────────

		private int Count<T>(Player p) where T : IBuilding
		{
			int id = Game.PlayerNumber(p);
			if (typeof(T) == typeof(SSStructural)) return Game.SpaceshipStructural[id];
			if (typeof(T) == typeof(SSComponent))  return Game.SpaceshipComponent[id];
			if (typeof(T) == typeof(SSModule))     return Game.SpaceshipModule[id];
			return 0;
		}

		// Nearest-neighbor scale of a Bytemap to the given target dimensions.
		private static Bytemap ScaleBytemap(Bytemap src, int targetW, int targetH)
		{
			int sw = src.Width, sh = src.Height;
			var dst = new Bytemap(targetW, targetH);
			for (int dy = 0; dy < targetH; dy++)
			{
				int sy = dy * sh / targetH;
				for (int dx = 0; dx < targetW; dx++)
					dst[dx, dy] = src[dx * sw / targetW, sy];
			}
			return dst;
		}

		// ── drawing helpers ──────────────────────────────────────────────────────

		// One "set bar": a row of `total` slot blocks with `built` filled in.
		// Returns the y-coordinate just past the bar.
		private int DrawSetBar(int built, int total, int x, int y, int barW, int barH, byte fillCol)
		{
			int gap = 2;
			int slotW = Math.Max(4, (barW - gap * (total - 1)) / total);
			for (int i = 0; i < total; i++)
			{
				int sx = x + i * (slotW + gap);
				byte col = (i < built) ? fillCol : CassetteTheme.BG2;
				this.FillRectangle(sx, y, slotW, barH, CassetteTheme.BORDER)
				    .FillRectangle(sx + 1, y + 1, slotW - 2, barH - 2, col);
			}
			return y + barH + 2;
		}

		// How many of ONE part type are built, given `total` parts of `perSet` types that
		// arrive strictly in rotation (hab, life, solar / pod, thruster). Clamped to the
		// number of columns drawn.
		internal static int BuiltOfType(int total, int typeIndex, int perSet, int cols)
			=> Math.Max(0, Math.Min(cols, (total - typeIndex + perSet - 1) / perSet));

		// Corner bracket for the viewport HUD overlay
		private void DrawCornerBracket(int x, int y, int rot)
		{
			// 8×8 L-bracket, rotated via mirror flags
			int bx = rot == 1 || rot == 2 ? x - 7 : x;
			int by = rot == 2 || rot == 3 ? y - 7 : y;
			int sx = (rot == 1 || rot == 2) ? -1 : 1;
			int sy = (rot == 2 || rot == 3) ? -1 : 1;
			for (int i = 0; i < 8; i++)
			{
				if (bx + i * sx >= 0 && bx + i * sx < Width && by >= 0 && by < Height)
					Bitmap[bx + i * sx, by] = CassetteTheme.PHOS_DIM;
				if (bx >= 0 && bx < Width && by + i * sy >= 0 && by + i * sy < Height)
					Bitmap[bx, by + i * sy] = CassetteTheme.PHOS_DIM;
			}
		}

		private void DrawScreen()
		{
			Player p = _civs[_civIdx];
			byte pid = Game.PlayerNumber(p);
			int str = Count<SSStructural>(p);
			int cmp = Count<SSComponent>(p);
			int mod = Count<SSModule>(p);

			int strNeeded = Game.SpaceshipStructuresNeeded(cmp, mod);
			bool launched = Game.Instance.SpaceshipLaunchTurn[pid] != 0;
			bool canLaunch = cmp >= 2 && mod >= 3 && str >= strNeeded && !launched;
			int successPct = Game.SpaceshipSuccessPct(cmp, mod);
			float flightYrs = Game.SpaceshipFlightYears(str, cmp, mod);
			int score = Game.SpaceshipScore(mod, cmp);

			// ── layout constants ─────────────────────────────────────────────────
			int W = Width, H = Height;
			const int headerH = 20;
			const int footerH = 11;
			int contentH = H - headerH - footerH;
			// Vista: left ~62% of screen.  Roster: rest.
			int vistaW = (W * 100) / 162;   // ≈ 62%
			int rosterX = vistaW + 2;
			int rosterW = W - rosterX - 2;

			// ── backdrop: spacedock vista ─────────────────────────────────────────
			IBitmap bg = Resources.SpacedockImage;
			if (bg is not null)
			{
				Bytemap scaled = ScaleBytemap(bg.Bitmap, vistaW, contentH);
				this.AddLayer(scaled, 0, headerH);
			}

			// Darken right side behind roster with a semi-opaque panel
			this.FillRectangle(vistaW, headerH, W - vistaW, contentH, CassetteTheme.BG0);
			// Subtle left-edge gradient line on the roster panel
			this.FillRectangle(vistaW, headerH, 1, contentH, CassetteTheme.BORDER);

			// ── header ────────────────────────────────────────────────────────────
			this.FillRectangle(0, 0, W, headerH, CassetteTheme.BG3)
			    .FillRectangle(0, headerH - 1, W, 1, CassetteTheme.BORDER);
			this.DrawText("SPACE RACE", 0, CassetteTheme.PHOS_GLOW, 4, 3);
			this.DrawText($"· {Game.Instance.GameYear} ·", 0, CassetteTheme.INK_MID, W / 2, 3, TextAlign.Center);
			// Civ navigation
			string navHint = $"◄ {p.TribeNamePlural} ►";
			this.DrawText(navHint, 0, CassetteTheme.INK_LOW, W - 4, 3, TextAlign.Right);

			// ── viewport HUD overlays (drawn on top of image) ────────────────────
			int vx1 = 4, vy1 = headerH + 4;
			int vx2 = vistaW - 4, vy2 = H - footerH - 4;

			// Corner brackets
			DrawCornerBracket(vx1,     vy1,     0);
			DrawCornerBracket(vx2 - 1, vy1,     1);
			DrawCornerBracket(vx2 - 1, vy2 - 1, 2);
			DrawCornerBracket(vx1,     vy2 - 1, 3);

			// Title only. ENGINES, MOD·SETS and STRUCTURE used to sit here too, and all three
			// were saying what the roster panel now shows better: engines are the component
			// columns, mod sets the module columns, structure the box grid. They were also the
			// least legible text on the screen — PHOS_DIM over the bright hull trusses, which
			// is why the mod-set count could not be read at all.
			int tx0 = vx2 - 12, tly = vy1 + 4;
			this.DrawText("ORBITAL SHIPYARD · L4", 0, CassetteTheme.PHOS_DIM, tx0, tly, TextAlign.Right);

			// Telemetry lines bottom-right — anchored above the progress bar (barY = vy2-18)
			int trx = vx2 - 4;
			int bly = vy2 - 18 - 6 - 9 * 3;
			this.DrawText($"TRAVEL ·· {flightYrs:F1} YRS", 0, CassetteTheme.PHOS_DIM, trx, bly, TextAlign.Right); bly += 9;
			this.DrawText($"SUCCESS · {successPct}%",  0, successPct >= 90 ? CassetteTheme.OK : successPct >= 75 ? CassetteTheme.PHOS : CassetteTheme.ALERT, trx, bly, TextAlign.Right); bly += 9;
			this.DrawText($"SCORE ··· +{score}",        0, CassetteTheme.PHOS_DIM, trx, bly, TextAlign.Right);

			// Progress bar across the bottom of the vista
			int totalParts = str + cmp + mod;
			int maxParts = strNeeded + Math.Max(4, cmp) + Math.Max(3, mod);
			float completion = maxParts > 0 ? Math.Min(1f, (float)totalParts / maxParts) : 0f;
			int barY = vy2 - 18;
			int barX = vx1 + 1;
			int barTotalW = vistaW - 8;
			int filledW = (int)(barTotalW * completion);
			this.FillRectangle(barX, barY, barTotalW, 5, CassetteTheme.BG2)
			    .FillRectangle(barX, barY, filledW, 5, CassetteTheme.PHOS);
			this.DrawText($"{Math.Round(completion * 100):F0}%  {(launched ? "LAUNCHED" : canLaunch ? "READY TO LAUNCH" : "UNDER CONSTRUCTION")}",
			              0, launched ? CassetteTheme.PHOS_GLOW : canLaunch ? CassetteTheme.OK : CassetteTheme.INK_MID,
			              barX, barY + 7);

			// ── roster panel ──────────────────────────────────────────────────────
			//
			// Modules and components are GRIDS, and a COLUMN IS A SET.
			//
			// They used to be lists: one labelled progress bar per part, max(1, modSets + 1)
			// sets of three rows. A maxed 12-module ship therefore wanted 15 rows plus set
			// dividers — around 195px of a 169px panel for MODULES alone, which pushed
			// COMPONENTS against the bottom edge and STRUCTURAL clean off it. The list grew
			// with the ship and the panel did not, so the fuller the ship the less you could
			// see of it.
			//
			// Three filled boxes down a module column is one complete set; two down a
			// component column is one engine. Parts arrive in type order (hab, life, solar /
			// pod, thruster), so the ragged right-hand edge shows exactly how far into the
			// current set the yard has reached — the thing the divider lines were for.
			//
			// Rows are icon-only, which is what buys the width for eight engine columns; the
			// key at the foot of the panel decodes the glyphs once.
			int ry = headerH + 6;

			const int boxW = 8, boxH = 7, boxGap = 1;
			int gridX = rosterX + 11;

			int DrawPartRow(char glyph, int built, int cols, int y, byte colour)
			{
				this.DrawText(glyph.ToString(), 0, colour, rosterX + 1, y + 1);
				for (int i = 0; i < cols; i++)
				{
					int sx = gridX + i * (boxW + boxGap);
					this.FillRectangle(sx, y, boxW, boxH, CassetteTheme.BORDER)
					    .FillRectangle(sx + 1, y + 1, boxW - 2, boxH - 2,
					                   i < built ? colour : CassetteTheme.BG2);
				}
				return y + boxH + 2;
			}

			char[] modSyms  = { '\xE9', '\x2A', '#' };   // fallback ascii glyphs
			char[] compSyms = { 'O', '>' };

			// --- Modules: three types across the maximum number of sets ---
			this.DrawText("MODULES", 0, CassetteTheme.OK, rosterX, ry); ry += 9;
			this.FillRectangle(rosterX, ry, rosterW, 1, CassetteTheme.BORDER); ry += 3;
			int modCols = Game.MAX_SS_MODULE / 3;
			for (int mi = 0; mi < 3; mi++)
				ry = DrawPartRow(modSyms[mi], BuiltOfType(mod, mi, 3, modCols), modCols, ry, CassetteTheme.OK);
			ry += 4;

			// --- Components: two types across the maximum number of engines ---
			this.DrawText("COMPONENTS", 0, CassetteTheme.PHOS, rosterX, ry); ry += 9;
			this.FillRectangle(rosterX, ry, rosterW, 1, CassetteTheme.BORDER); ry += 3;
			int cmpCols = Game.MAX_SS_COMPONENT / 2;
			for (int ci = 0; ci < 2; ci++)
				ry = DrawPartRow(compSyms[ci], BuiltOfType(cmp, ci, 2, cmpCols), cmpCols, ry, CassetteTheme.PHOS);
			ry += 4;

			// --- Structural ---
			this.DrawText("STRUCTURAL", 0, CassetteTheme.CYAN, rosterX, ry); ry += 9;
			this.FillRectangle(rosterX, ry, rosterW, 1, CassetteTheme.BORDER); ry += 3;

			int strShow = Math.Max(strNeeded, str);
			// Render as a single continuous bar broken into groups of 5
			int barRW = rosterW - 4;
			const int segW = 8, segH = 7, segGap = 1;
			int cols = Math.Max(1, (barRW + segGap) / (segW + segGap));
			int rows = (strShow + cols - 1) / cols;
			for (int i = 0; i < rows * cols; i++)
			{
				int col = i % cols;
				int row = i / cols;
				int sx = rosterX + 2 + col * (segW + segGap);
				int sy = ry + row * (segH + segGap);
				byte col2 = i < str ? CassetteTheme.CYAN : i < strNeeded ? CassetteTheme.BG2 : (byte)0;
				if (col2 == 0) continue;
				this.FillRectangle(sx, sy, segW, segH, CassetteTheme.BORDER)
				    .FillRectangle(sx + 1, sy + 1, segW - 2, segH - 2, col2);
			}
			ry += rows * (segH + segGap) + 3;
			this.DrawText($"{str} / {strNeeded} REQUIRED", 0, str >= strNeeded ? CassetteTheme.OK : CassetteTheme.PHOS, rosterX + 2, ry);
			ry += 11;

			// The key, and the reason the rows above can be icon-only. Modules on one line,
			// components on the next, so the two vocabularies stay separate.
			this.DrawText($"{modSyms[0]} HAB  {modSyms[1]} LIFE  {modSyms[2]} SOLAR",
			              0, CassetteTheme.INK_MID, rosterX + 2, ry); ry += 8;
			this.DrawText($"{compSyms[0]} FUEL POD  {compSyms[1]} THRUSTER",
			              0, CassetteTheme.INK_MID, rosterX + 2, ry);

			// ── footer ────────────────────────────────────────────────────────────
			int fy = H - footerH;
			this.FillRectangle(0, fy, W, 1, CassetteTheme.BORDER)
			    .FillRectangle(0, fy + 1, W, footerH - 1, CassetteTheme.BG3);

			if (launched)
			{
				string launchYr = Common.YearString((ushort)Game.Instance.SpaceshipLaunchTurn[pid]);
				// Arrival ZEROES the ETA (Game.cs, both the arrival and the interception paths)
				// while the launch turn stays set forever — so a ship that has already got there
				// reads as launched with an arrival turn of 0, and TurnToYear(0) is 4000 BC.
				// That is where "ETA 4000 BC" came from: not a bad calculation, a landed ship.
				int arrival = Game.Instance.SpaceshipArrivalTurn[pid];
				string tail = arrival > 0 ? $"ETA {Common.YearString((ushort)arrival)}" : "ARRIVED";
				this.DrawText($"LAUNCHED {launchYr} · {tail}", 0, CassetteTheme.PHOS_GLOW, W / 2, fy + 2, TextAlign.Center);
			}
			else if (canLaunch)
			{
				this.DrawText("ENTER · LAUNCH    ◄► · CYCLE    ESC · CLOSE", 0, CassetteTheme.OK, W / 2, fy + 2, TextAlign.Center);
			}
			else
			{
				this.DrawText($"◄► · CYCLE CIV    ESC · CLOSE    {_civIdx + 1}/{_civs.Length}", 0, CassetteTheme.INK_LOW, W / 2, fy + 2, TextAlign.Center);
			}
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;
			DrawScreen();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (args.Key == Key.Left || args.Key == Key.NumPad4)
			{
				_civIdx = (_civIdx - 1 + _civs.Length) % _civs.Length;
				_update = true;
				return true;
			}
			if (args.Key == Key.Right || args.Key == Key.NumPad6)
			{
				_civIdx = (_civIdx + 1) % _civs.Length;
				_update = true;
				return true;
			}
			if (args.Key == Key.Enter)
			{
				// Launch if ready
				Player p = _civs[_civIdx];
				if (p == Human)
				{
					byte pid = Game.PlayerNumber(p);
					int str = Count<SSStructural>(p);
					int cmp = Count<SSComponent>(p);
					int mod = Count<SSModule>(p);
					int needed = Game.SpaceshipStructuresNeeded(cmp, mod);
					if (cmp >= 2 && mod >= 3 && str >= needed
					    && Game.Instance.SpaceshipLaunchTurn[pid] == 0)
					{
						// Trigger launch now by nudging conditions already met —
						// EndTurn fires the actual launch logic; for immediate effect
						// call it directly.
						Game.Instance.SpaceshipLaunchTurn[pid] = Game.Instance.GameTurn;
						Game.Instance.SpaceshipArrivalTurn[pid] = Game.Instance.GameTurn
							+ (int)Math.Ceiling(Game.SpaceshipFlightYears(str, cmp, mod));
						Game.Instance.ClearSpaceShipProduction(pid);
						Game.Instance.PerformAutoSave();
						string eta = Common.YearString((ushort)Game.Instance.SpaceshipArrivalTurn[pid]);
						bool intercepted = Game.Instance.VisitorType == VisitorArchetype.Conquerors;
						string artKey = intercepted ? "spaceshipintercepted" : "spaceshiplaunched";
						string launchCaption = intercepted
							? "Our spaceship has been intercepted!"
							: $"Our spaceship has launched! Arrival: {eta}";
						GameTask.Enqueue(Show.EventArt(artKey, launchCaption));
						_update = true;
					}
				}
				return true;
			}
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		public SpaceShips() : base(MouseCursor.Pointer)
		{
			_civs = Game.Players
				.Where(p => p != 0 && !p.IsDestroyed())
				.ToArray();

			_civIdx = 0;
			for (int i = 0; i < _civs.Length; i++)
				if (_civs[i] == Human) { _civIdx = i; break; }

			// Build palette: start from spacedock image palette if available,
			// otherwise default. Then stamp Cassette colours at indices 1-17.
			IBitmap bg = Resources.SpacedockImage;
			using Palette pal = (bg is not null) ? bg.Palette.Copy() : Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				pal.MergePalette(cassette, 1, 17);
			Palette = pal;

			this.Clear(0);
		}
	}
}
