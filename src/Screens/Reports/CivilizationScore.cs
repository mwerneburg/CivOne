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
		private const int GRAPH_BOTTOM_PAD = 16; // space for X-axis labels + scroll hint

		private int GraphTop    => GRAPH_TOP;
		private int GraphBottom => Height - GRAPH_BOTTOM_PAD;
		private int GraphLeft   => GRAPH_LEFT;
		private int GraphRight  => Width - 4;
		private int GraphW      => GraphRight - GraphLeft;
		private int GraphH      => GraphBottom - GraphTop;

		private int _scrollX;
		private bool _dirty = true;

		// ── draw ─────────────────────────────────────────────────────────────

		private void Draw()
		{
			var history = Game.ScoreHistory;
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
			}
			else if (n <= GraphW)
			{
				pxPerTurn  = (float)GraphW / (n - 1);
				maxScrollX = 0;
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
				if (p.Score > maxScore) maxScore = p.Score;

			int tickInterval = NiceInterval(maxScore);
			int yTop         = ((maxScore / tickInterval) + 1) * tickInterval;
			float pxPerScore = (float)GraphH / yTop;

			// ── background ──────────────────────────────────────────────────

			this.FillRectangle(0, GRAPH_TOP, Width, Height - GRAPH_TOP, CassetteTheme.BG0);

			int fh = Resources.GetFontHeight(0);

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

			// ── score traces ─────────────────────────────────────────────────

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
					int score   = players[pi].Score;
					lastX = GraphLeft + GraphW / 2;
					lastY = GraphBottom - (int)(score * pxPerScore);
					lastY = Math.Max(GraphTop, Math.Min(GraphBottom, lastY));
				}

				// Terminal dot (3×3) at the most recent visible data point
				if (lastX != int.MinValue)
					this.FillRectangle(lastX - 1, lastY - 1, 3, 3, col);
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

			int lx = GraphRight - 2;
			int ly = GraphTop + 4;
			foreach (var p in players.OrderByDescending(p => p.Score))
			{
				int  pIdx = (byte)p;
				byte col  = Common.ColourLight[pIdx % Common.ColourLight.Length];
				this.DrawText($"{p.TribeNamePlural}: {p.Score}", 0, col, lx, ly, TextAlign.Right);
				ly += fh + 1;
			}

			// ── scroll hint ──────────────────────────────────────────────────

			if (maxScrollX > 0)
			{
				int pct  = (int)(100.0 * _scrollX / maxScrollX);
				string h = $"[ < > scroll  {pct}% ]";
				this.DrawText(h, 0, CassetteTheme.INK_LOW,
				              Width / 2, GraphBottom + 2, TextAlign.Center);
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
			foreach (int m in new[] { 1, 2, 5, 10 })
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
			int n          = Game.ScoreHistory.Count;
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
