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
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	internal class ScienceReport : BaseReport
	{
		// ── node data ────────────────────────────────────────────────────────────────

		private struct TechNode
		{
			public IAdvance Advance;
			public int X, Y, W, H;
		}

		private TechNode[]              _nodes;
		private Dictionary<byte, int>   _nodeIdx;   // advance ID → index into _nodes
		private int _treeW, _treeTop, _scrollX;
		private bool _dirty;

		// layout constants (computed from font)
		private int _fontH, _nodeH, _rowH, _colW, _nodeW;
		private const int PAD_X = 4, PAD_Y = 2, CONNECTOR = 18;

		// ── state helpers ─────────────────────────────────────────────────────────

		private bool PlayerHas(IAdvance a)    => Human.Advances.Any(h => h.Id == a.Id);
		private bool IsResearching(IAdvance a) => Human.CurrentResearch?.Id == a.Id;
		private bool CanResearch(IAdvance a)   => !PlayerHas(a) && a.RequiredTechs.All(r => PlayerHas(r));

		// ── layout ───────────────────────────────────────────────────────────────────

		private void BuildLayout()
		{
			_fontH = Resources.GetFontHeight(0);
			_nodeH = _fontH + PAD_Y * 2;
			_rowH  = _nodeH + 3;

			var all = Common.Advances.Where(a => !(a is FutureTech)).ToArray();

			// Topological depth: longest prerequisite chain to this node.
			var depth = all.ToDictionary(a => a.Id, _ => 0);
			for (bool changed = true; changed;)
			{
				changed = false;
				foreach (var a in all)
				{
					if (a.RequiredTechs.Length == 0) continue;
					int d = a.RequiredTechs
					         .Where(r => depth.ContainsKey(r.Id))
					         .Select(r => depth[r.Id] + 1)
					         .DefaultIfEmpty(1).Max();
					if (d == depth[a.Id]) continue;
					depth[a.Id] = d;
					changed = true;
				}
			}

			// Uniform node width: fit the widest tech name.
			_nodeW = all.Max(a => Resources.GetTextSize(0, a.Name).Width) + PAD_X * 2;
			_colW  = _nodeW + CONNECTOR;

			// Group by depth, sort within each column by tech ID for stability.
			var cols = all.GroupBy(a => depth[a.Id])
			              .OrderBy(g => g.Key)
			              .Select(g => g.OrderBy(a => a.Id).ToArray())
			              .ToArray();

			int maxRows = cols.Max(g => g.Length);
			int treeH   = maxRows * _rowH;

			var nodes   = new List<TechNode>(all.Length);
			_nodeIdx    = new Dictionary<byte, int>(all.Length);

			foreach (var col in cols)
			{
				int d      = depth[col[0].Id];
				int colTop = (treeH - col.Length * _rowH) / 2;

				for (int r = 0; r < col.Length; r++)
				{
					_nodeIdx[col[r].Id] = nodes.Count;
					nodes.Add(new TechNode
					{
						Advance = col[r],
						X = d * _colW,
						Y = colTop + r * _rowH,
						W = _nodeW,
						H = _nodeH
					});
				}
			}

			_nodes = nodes.ToArray();
			int maxDepth = cols.Length - 1;
			_treeW = maxDepth * _colW + _nodeW;
		}

		// ── line helpers (clipped to tree area) ───────────────────────────────────

		private void HLine(int x1, int y, int x2, byte col)
		{
			if (x1 > x2) { int t = x1; x1 = x2; x2 = t; }
			x1 = Math.Max(0, x1);
			x2 = Math.Min(Width - 1, x2);
			if (y < _treeTop || y >= Height || x1 > x2) return;
			this.FillRectangle(x1, y, x2 - x1 + 1, 1, col);
		}

		private void VLine(int x, int y1, int y2, byte col)
		{
			if (y1 > y2) { int t = y1; y1 = y2; y2 = t; }
			y1 = Math.Max(_treeTop, y1);
			y2 = Math.Min(Height - 1, y2);
			if (x < 0 || x >= Width || y1 > y2) return;
			this.FillRectangle(x, y1, 1, y2 - y1 + 1, col);
		}

		// ── edge drawing ──────────────────────────────────────────────────────────

		private void DrawEdge(TechNode from, TechNode to)
		{
			int x0 = from.X + from.W - _scrollX;
			int xm = to.X - CONNECTOR / 2 - _scrollX;   // elbow x
			int x1 = to.X - _scrollX;
			int y0 = from.Y + from.H / 2 + _treeTop;
			int y1 = to.Y   + to.H   / 2 + _treeTop;

			// Cull edges fully off-screen horizontally.
			if (x1 < 0 || x0 > Width) return;

			byte col = PlayerHas(from.Advance) && PlayerHas(to.Advance) ? CassetteTheme.PHOS_DIM :
			           PlayerHas(from.Advance)                           ? CassetteTheme.INK_MID  :
			                                                               CassetteTheme.BG3;
			HLine(x0, y0, xm, col);
			VLine(xm, y0, y1, col);
			HLine(xm, y1, x1, col);
		}

		// ── node drawing ──────────────────────────────────────────────────────────

		private void DrawNode(TechNode node)
		{
			int sx = node.X - _scrollX;
			int sy = node.Y + _treeTop;
			if (sx + node.W < 0 || sx >= Width)            return;
			if (sy + node.H < _treeTop || sy >= Height)    return;

			byte bg, fg;
			if (IsResearching(node.Advance))
			{
				bg = CassetteTheme.PHOS_DIM;
				fg = CassetteTheme.PHOS_GLOW;
			}
			else if (PlayerHas(node.Advance))
			{
				bg = CassetteTheme.BG2;
				fg = Game.GetAdvanceOrigin(node.Advance, Human)
				   ? CassetteTheme.PHOS : CassetteTheme.INK_HIGH;
			}
			else if (CanResearch(node.Advance))
			{
				bg = CassetteTheme.BG1;
				fg = CassetteTheme.INK_MID;
			}
			else
			{
				bg = CassetteTheme.BG0;
				fg = CassetteTheme.INK_LOW;
			}

			this.FillRectangle(sx, sy, node.W, node.H, bg);
			this.DrawRectangle(sx, sy, node.W, node.H, CassetteTheme.BORDER);
			this.DrawText(node.Advance.Name, 0, fg, sx + PAD_X, sy + PAD_Y);
		}

		// ── scroll hint ───────────────────────────────────────────────────────────

		private void DrawScrollHint()
		{
			if (_treeW <= Width) return;
			int maxScroll = _treeW - Width;
			int pct = maxScroll > 0 ? (int)(100.0 * _scrollX / maxScroll) : 0;
			string hint = $"[ < > to scroll  {pct}% ]";
			this.DrawText(hint, 0, CassetteTheme.INK_LOW, Width / 2, _treeTop + 1, TextAlign.Center);
		}

		// ── full redraw ───────────────────────────────────────────────────────────

		private void Redraw()
		{
			this.FillRectangle(0, _treeTop, Width, Height - _treeTop, CassetteTheme.BG0);

			DrawScrollHint();

			// Draw legend at bottom-right
			int ly = Height - _fontH - 3;
			int lx = Width - 4;
			this.DrawText("known (yours)", 0, CassetteTheme.PHOS,     lx, ly, TextAlign.Right); ly -= _fontH + 1;
			this.DrawText("known (traded)", 0, CassetteTheme.INK_HIGH, lx, ly, TextAlign.Right); ly -= _fontH + 1;
			this.DrawText("available",      0, CassetteTheme.INK_MID,  lx, ly, TextAlign.Right); ly -= _fontH + 1;
			this.DrawText("locked",         0, CassetteTheme.INK_LOW,  lx, ly, TextAlign.Right);

			// Edges behind nodes
			foreach (var node in _nodes)
				foreach (var req in node.Advance.RequiredTechs)
					if (_nodeIdx.TryGetValue(req.Id, out int ri))
						DrawEdge(_nodes[ri], node);

			// Nodes on top
			foreach (var node in _nodes)
				DrawNode(node);

			_dirty = false;
		}

		// ── update gate ───────────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_dirty) return false;
			Redraw();
			return true;
		}

		// ── input ─────────────────────────────────────────────────────────────────

		public override bool KeyDown(KeyboardEventArgs args)
		{
			int maxScroll = Math.Max(0, _treeW - Width);
			if (args.Key == Key.Left || args.Key == Key.NumPad4)
			{
				_scrollX = Math.Max(0, _scrollX - _colW);
				_dirty = true;
				return true;
			}
			if (args.Key == Key.Right || args.Key == Key.NumPad6)
			{
				_scrollX = Math.Min(maxScroll, _scrollX + _colW);
				_dirty = true;
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

		// ── constructor ───────────────────────────────────────────────────────────

		public ScienceReport() : base("SCIENCE REPORT", 1)
		{
			// Research progress bar (positioned just below the header)
			if (Human.CurrentResearch != null)
			{
				double w = 8.0;
				while (w * Human.ScienceCost > Width - 40 && w > 0.1) w -= 0.1;
				int barW = (int)Math.Ceiling(w * Human.ScienceCost);
				int barX = (Width - barW) / 2;

				this.FillRectangle(barX, 28, barW, 10, 9);
				this.DrawText($"Researching: {Human.CurrentResearch.Name}", 0, CassetteTheme.PHOS_GHOST, Width / 2, 29, TextAlign.Center)
				    .DrawText($"Researching: {Human.CurrentResearch.Name}", 0, CassetteTheme.PHOS_GLOW,  Width / 2 - 1, 29, TextAlign.Center);

				int plotX = -1;
				for (int i = 0; i < Human.Science; i++)
				{
					int nx = (int)Math.Floor(w * i) + barX;
					if (nx == plotX) continue;
					plotX = nx;
					this.AddLayer(Icons.Science, nx, 30);
				}
				_treeTop = 42;
			}
			else
			{
				_treeTop = 30;
			}

			BuildLayout();

			// Start scroll so the current research (or earliest locked tech) is in view.
			if (Human.CurrentResearch != null &&
			    _nodeIdx.TryGetValue(Human.CurrentResearch.Id, out int idx))
			{
				_scrollX = Math.Max(0, _nodes[idx].X - Width / 3);
			}

			_dirty = true;
		}
	}
}
