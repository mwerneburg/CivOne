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
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	internal class CivilizationScore : BaseReport
	{
		private const int GRAPH_LEFT   = 52;   // space for Y-axis labels
		private const int GRAPH_TOP    = 30;   // below the BaseReport header
		private const int GRAPH_BOTTOM_PAD = 16; // space for the X-axis year labels

		private int GraphTop    => GRAPH_TOP;
		// Year labels sit on the first row below the graph, the scroll hint on a second
		// row under them — they used to share one row and the centred hint overprinted
		// whichever year label it landed on.
		private int GraphBottom => Height - GRAPH_BOTTOM_PAD - Resources.GetFontHeight(0) - 1;
		private int GraphLeft   => GRAPH_LEFT;
		private int GraphRight  => Width - 4;
		private int GraphW      => GraphRight - GraphLeft;
		private int GraphH      => GraphBottom - GraphTop;

		private int _scrollX;
		private bool _dirty = true;

		// Three views over the same graph. Score was the only one for a long time and culture
		// rode along in brackets in the legend, which is a poor way to read a quantity that
		// moves every turn. Output is here because the economic victory is otherwise unreadable
		// from inside a game: Pax Mercatoria wants half the world's gross output for 20 turns
		// and nothing on any screen said what your share was.
		private enum Page { Score, Culture, Output }
		private Page _page = Page.Score;

		private string PageTitle => _page switch
		{
			Page.Culture => "CULTURAL WEIGHT",
			Page.Output  => "ECONOMIC OUTPUT",
			_            => "CIVILIZATION SCORE",
		};

		private System.Collections.Generic.IReadOnlyList<int[]> Series => _page switch
		{
			Page.Culture => Game.CultureHistory,
			Page.Output  => Game.OutputHistory,
			_            => Game.ScoreHistory,
		};

		private int LiveValue(Player p) => _page switch
		{
			Page.Culture => p.Culture,
			Page.Output  => Game.GrossOutputOf(p),
			_            => p.Score,
		};

		// ── draw ─────────────────────────────────────────────────────────────

		private void Draw()
		{
			var history = Series;
			var players = Game.Players
				.Where(p => !(p.Civilization is Barbarian))
				.ToArray();

			// ── layout ──────────────────────────────────────────────────────

			int n = history.Count;

			float pxPerTurn;
			int   maxScrollX;

			if (n <= 1)
			{
				pxPerTurn  = GraphW;
				maxScrollX = 0;
				_scrollX   = 0;
			}
			else if (n <= GraphW)
			{
				pxPerTurn  = (float)GraphW / (n - 1);
				maxScrollX = 0;
				// Reset, not just capped: the three pages share one scroll position and their
				// histories are different lengths, so a position valid on the 472-sample score
				// page pushes a 1-sample output page off the left edge entirely.
				_scrollX   = 0;
			}
			else
			{
				pxPerTurn  = 1f;
				maxScrollX = n - 1 - GraphW;
				_scrollX   = Math.Max(0, Math.Min(_scrollX, maxScrollX));
			}

			// ── score range ─────────────────────────────────────────────────

			int maxScore = 1;
			if (n > 0)
			{
				foreach (var snap in history)
					for (int pi = 1; pi < snap.Length; pi++)
						if (snap[pi] > maxScore) maxScore = snap[pi];
			}
			foreach (var p in players)
				if (LiveValue(p) > maxScore) maxScore = LiveValue(p);

			int tickInterval = NiceInterval(maxScore);
			int yTop         = ((maxScore / tickInterval) + 1) * tickInterval;
			float pxPerScore = (float)GraphH / yTop;

			// ── background ──────────────────────────────────────────────────

			this.FillRectangle(0, GRAPH_TOP, Width, Height - GRAPH_TOP, CassetteTheme.BG0);

			int fh = Resources.GetFontHeight(0);

			// BaseReport painted the title once in its constructor; paging changes it, so the
			// header band is repainted here rather than left saying SCORE on the culture page.
			this.FillRectangle(0, 0, Width, 10, CassetteTheme.BG3)
				.DrawText(PageTitle, 0, CassetteTheme.PHOS_GLOW, Width / 2, 2, TextAlign.Center);

			// ── Y-axis grid and labels ───────────────────────────────────────

			for (int tick = 0; tick <= yTop; tick += tickInterval)
			{
				int ty = GraphBottom - (int)(tick * pxPerScore);
				if (ty < GraphTop) break;
				this.FillRectangle(GraphLeft, ty, GraphW, 1, CassetteTheme.BG2);
				this.DrawText(tick.ToString(), 0, CassetteTheme.INK_LOW,
				              GraphLeft - 2, ty - fh / 2, TextAlign.Right);
			}

			// ── axes ─────────────────────────────────────────────────────────

			this.FillRectangle(GraphLeft - 1, GraphTop, 1, GraphH + 1, CassetteTheme.BORDER);
			this.FillRectangle(GraphLeft - 1, GraphBottom, GraphW + 2, 1, CassetteTheme.BORDER);

			// ── Cultural ascendancy threshold ────────────────────────────────

			// The bar the human must clear: the best rival's culture times the required
			// margin. Flat, because it is a live comparison rather than a historical curve —
			// the culture series records each civ, not the bar.
			if (_page == Page.Culture)
			{
				int best = Game.Players
					.Where(p => p is not null && p != Human && !p.IsDestroyed() && !(p.Civilization is Barbarian))
					.Select(p => p.Culture).DefaultIfEmpty(0).Max();
				int bar = best * Game.CultureLeadMultiple;
				int by  = GraphBottom - (int)(bar * pxPerScore);
				if (by >= GraphTop && by <= GraphBottom)
					for (int dx = 0; dx < GraphW; dx += 4)
						this.FillRectangle(GraphLeft + dx, by, 2, 1, CassetteTheme.ALERT);

				int shadow = Game.CulturalShadow(Human);
				byte scol = Game.CultureStreak > 0 ? CassetteTheme.OK : CassetteTheme.INK_LOW;
				this.DrawText($"CULTURAL ASCENDANCY STREAK {Game.CultureStreak}/20", 0, scol,
					GraphRight - 4, GraphTop + 4, TextAlign.Right);
				this.DrawText($"CITIES IN OUR SHADOW {shadow}/{Game.CulturalShadowTarget}", 0,
					shadow >= Game.CulturalShadowTarget ? CassetteTheme.OK : CassetteTheme.INK_LOW,
					GraphRight - 4, GraphTop + 4 + fh + 1, TextAlign.Right);
				this.DrawText($"- - -  {Game.CultureLeadMultiple}x BEST RIVAL ({bar})", 0, CassetteTheme.ALERT,
					GraphRight - 4, GraphTop + 4 + 2 * (fh + 1), TextAlign.Right);
			}

			// ── Pax Mercatoria threshold ─────────────────────────────────────

			// On the output page only: the finish line itself. The victory wants the human
			// above HALF the world's gross output, so the useful reference is not a fixed
			// value but a curve — half of the total of every civ at that same turn. A trace
			// above this line is a turn that counted toward the streak.
			if (_page == Page.Output)
			{
				// Fewer than two samples means no segment to draw between — and this series
				// starts empty on a save that predates it, so a player's first look at the
				// page had a red legend promising a line that was never plotted. Fall back to
				// a flat line at TODAY's threshold, which is the number they actually want.
				if (n < 2)
				{
					int liveWorld = Game.Players.Where(p => p is not null).Sum(Game.GrossOutputOf);
					int fy = GraphBottom - (int)((liveWorld / 2.0) * pxPerScore);
					fy = Math.Max(GraphTop, Math.Min(GraphBottom, fy));
					for (int dx = 0; dx < GraphW; dx += 4)
						this.FillRectangle(GraphLeft + dx, fy, 2, 1, CassetteTheme.ALERT);
				}

				int prevTx = int.MinValue, prevTy = int.MinValue;
				for (int t = 0; t < n; t++)
				{
					int screenX = GraphLeft + (int)((t - _scrollX) * pxPerTurn);
					if (screenX < GraphLeft - 1) { prevTx = int.MinValue; continue; }
					if (screenX > GraphRight + 1) break;

					var snap = history[t];
					int worldTotal = 0;
					for (int pi = 1; pi < snap.Length; pi++) worldTotal += snap[pi];
					int screenY = GraphBottom - (int)((worldTotal / 2.0) * pxPerScore);
					screenY = Math.Max(GraphTop, Math.Min(GraphBottom, screenY));

					// Dashed, so it reads as a threshold rather than another civilization.
					if (prevTx != int.MinValue && (t & 2) == 0)
						DrawLine(prevTx, prevTy, screenX, screenY, CassetteTheme.ALERT);
					prevTx = screenX;
					prevTy = screenY;
				}
			}

			// ── score traces ─────────────────────────────────────────────────

			var lineTips = new System.Collections.Generic.List<(int score, int y, byte col)>();

			for (int pi = 0; pi < players.Length; pi++)
			{
				int  pIdx = (byte)players[pi];
				byte col  = Common.ColourLight[pIdx % Common.ColourLight.Length];

				int lastX = int.MinValue, lastY = int.MinValue;
				int prevX = int.MinValue, prevY = int.MinValue;

				for (int t = 0; t < n; t++)
				{
					int screenX = GraphLeft + (int)((t - _scrollX) * pxPerTurn);
					if (screenX < GraphLeft - 1) { prevX = int.MinValue; continue; }
					if (screenX > GraphRight + 1) break;

					var snap   = history[t];
					int score  = (pIdx + 1 < snap.Length) ? snap[pIdx + 1] : 0;
					int screenY = GraphBottom - (int)(score * pxPerScore);
					screenY = Math.Max(GraphTop, Math.Min(GraphBottom, screenY));

					if (prevX != int.MinValue)
						DrawLine(prevX, prevY, screenX, screenY, col);

					prevX = screenX;
					prevY = screenY;
					lastX = screenX;
					lastY = screenY;
				}

				// If no history yet, plot current score as a single point
				if (n == 0)
				{
					int score   = LiveValue(players[pi]);
					lastX = GraphLeft + GraphW / 2;
					lastY = GraphBottom - (int)(score * pxPerScore);
					lastY = Math.Max(GraphTop, Math.Min(GraphBottom, lastY));
				}

				// End the line at NOW, not at the last snapshot.
				//
				// RecordScoreSnapshot runs once per turn, but the human's end-of-game
				// AwardMilestone calls land AFTER it — so the final sample can be well below
				// the score the player is actually shown. In a 2200 AD save the recorded value
				// for the human was 7232 against a reported 8577, while every AI matched its
				// snapshot to the point. The tip label has always printed the live score, so
				// the number floated 1345 points clear of its own line and the two leaders
				// appeared to be plotted in the wrong order.
				if (lastX != int.MinValue)
				{
					int liveY = GraphBottom - (int)(LiveValue(players[pi]) * pxPerScore);
					liveY = Math.Max(GraphTop, Math.Min(GraphBottom, liveY));
					if (liveY != lastY)
					{
						DrawLine(lastX, lastY, lastX, liveY, col);
						lastY = liveY;
					}
				}

				// Terminal dot (3×3) at the most recent visible data point
				if (lastX != int.MinValue)
				{
					this.FillRectangle(lastX - 1, lastY - 1, 3, 3, col);
					lineTips.Add((LiveValue(players[pi]), lastY, col));
				}
			}

			// ── X-axis year labels ───────────────────────────────────────────

			if (n >= 1)
			{
				int minTurns  = (int)Math.Ceiling(52.0 / Math.Max((double)pxPerTurn, 0.001));
				int labelEvery = NiceCeil(Math.Max(1, minTurns));
				for (int t = 0; t < n; t += labelEvery)
				{
					int sx = GraphLeft + (int)((t - _scrollX) * pxPerTurn);
					if (sx < GraphLeft || sx > GraphRight) continue;
					ushort turnNum = (ushort)history[t][0];
					string label = Common.YearString(turnNum);
					this.DrawText(label, 0, CassetteTheme.INK_LOW, sx, GraphBottom + 2);
				}
			}

			// ── legend ───────────────────────────────────────────────────────

			// Ranked by SCORE; the (Nc) is the culture ledger, which is independent
			// and deliberately unsorted. The rank number and the score tags at the
			// line tips (below) are the reliable way to match a line to its row —
			// the palette repeats across this many civs, so colour alone can't.
			// Left-aligned at the top-left: the traces converge on their current
			// scores at the RIGHT edge (where the line tips and their score tags
			// live), so the legend sits on the left where the early-turn traces
			// are sparse and it interferes least.
			int lx = GraphLeft + 4;
			int ly = GraphTop + 4;
			int rank = 1;
			foreach (var p in players.OrderByDescending(LiveValue))
			{
				int  pIdx = (byte)p;
				byte col  = Common.ColourLight[pIdx % Common.ColourLight.Length];
				this.DrawText($"{rank++}. {p.TribeNamePlural}: {LiveValue(p)}", 0, col, lx, ly, TextAlign.Left);
				ly += fh + 1;
			}

			// Score tags at each line's tip, greedily spaced so clustered tips
			// stay readable. Same number as the legend row: match by value.
			int prevBottom = int.MinValue;
			foreach (var (score, tipY, tipCol) in lineTips.OrderBy(t => t.y))
			{
				int ty = Math.Max(GraphTop, tipY - fh / 2);
				if (ty < prevBottom) ty = prevBottom;
				if (ty > GraphBottom - fh) ty = GraphBottom - fh;
				this.DrawText(score.ToString(), 0, tipCol, GraphRight - 4, ty, TextAlign.Right);
				prevBottom = ty + fh;
			}

			// ── Pax Mercatoria streak ────────────────────────────────────────

			// The number the player actually wants: how many consecutive turns of the twenty
			// are banked. Nothing else in the game reports it.
			if (_page == Page.Output)
			{
				string streak = $"PAX MERCATORIA STREAK {Game.EconStreak}/20";
				byte col = Game.EconStreak > 0 ? CassetteTheme.OK : CassetteTheme.INK_LOW;
				this.DrawText(streak, 0, col, GraphRight - 4, GraphTop + 4, TextAlign.Right);
				int halfNow = Game.Players.Where(p => p is not null).Sum(Game.GrossOutputOf) / 2;
				this.DrawText($"- - -  HALF OF WORLD OUTPUT ({halfNow})", 0, CassetteTheme.ALERT,
					GraphRight - 4, GraphTop + 4 + fh + 1, TextAlign.Right);
			}

			// ── scroll hint ──────────────────────────────────────────────────

			{
				string h = maxScrollX > 0
					? $"[ < > scroll  {(int)(100.0 * _scrollX / maxScrollX)}%   TAB page ]"
					: "[ TAB page ]";
				this.DrawText(h, 0, CassetteTheme.INK_LOW,
				              Width / 2, GraphBottom + 2 + fh + 1, TextAlign.Center);
			}

			_dirty = false;
		}

		// ── Bresenham line ────────────────────────────────────────────────────

		private void DrawLine(int x0, int y0, int x1, int y1, byte col)
		{
			int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;
			while (true)
			{
				if (x0 >= GraphLeft && x0 <= GraphRight && y0 >= GraphTop && y0 <= GraphBottom)
					this.FillRectangle(x0, y0, 1, 1, col);
				if (x0 == x1 && y0 == y1) break;
				int e2 = err * 2;
				if (e2 >= dy) { if (x0 == x1) break; err += dy; x0 += sx; }
				if (e2 <= dx) { if (y0 == y1) break; err += dx; y0 += sy; }
			}
		}

		// ── axis helpers ──────────────────────────────────────────────────────

		// Round up to the nearest 1/2/5/10 × power-of-10 that is >= minVal.
		private static int NiceCeil(int minVal)
		{
			if (minVal <= 1) return 1;
			double mag = Math.Pow(10, Math.Floor(Math.Log10(minVal)));
			foreach (int m in (int[])[1, 2, 5, 10])
			{
				int v = (int)(m * mag);
				if (v >= minVal) return v;
			}
			return (int)(10 * mag);
		}

		private static int NiceInterval(int range, int targetTicks = 8)
		{
			if (range <= 0) return 1;
			double step = range / (double)targetTicks;
			double mag  = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(step, 0.001))));
			double norm = step / mag;
			double nice = norm <= 1.5 ? 1 : norm <= 3.5 ? 2 : norm <= 7.5 ? 5 : 10;
			return Math.Max(1, (int)(nice * mag));
		}

		// ── update / input ────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_dirty) return false;
			Draw();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (args.Key == Key.Tab)
			{
				_page  = _page == Page.Score ? Page.Culture : _page == Page.Culture ? Page.Output : Page.Score;
				_dirty = true;
				return true;
			}

			int n          = Series.Count;
			int maxScrollX = Math.Max(0, n - 1 - GraphW);

			if (maxScrollX > 0 && (args.Key == Key.Left || args.Key == Key.NumPad4))
			{
				_scrollX = Math.Max(0, _scrollX - Math.Max(1, GraphW / 4));
				_dirty   = true;
				return true;
			}
			if (maxScrollX > 0 && (args.Key == Key.Right || args.Key == Key.NumPad6))
			{
				_scrollX = Math.Min(maxScrollX, _scrollX + Math.Max(1, GraphW / 4));
				_dirty   = true;
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

		// ── constructor ───────────────────────────────────────────────────────

		public CivilizationScore() : base("CIVILIZATION SCORE", 3)
		{
			// Start at the right edge so the most recent scores are visible
			int n = Game.ScoreHistory.Count;
			_scrollX = Math.Max(0, n - 1 - (Width - GRAPH_LEFT - 4));
		}
	}
}
