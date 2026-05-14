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
using System.IO;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;

namespace CivOne.Screens
{
	[Modal, Expand]
	internal class ImprovementArtScreen : BaseScreen
	{
		private readonly string _name;
		private readonly byte[,] _indices; // [y, x] pre-mapped to Cassette palette
		private readonly int _imgW, _imgH;
		private bool _update = true;

		internal static string FindArtPath(string improvementName)
		{
			if (string.IsNullOrEmpty(improvementName)) return null;
			try
			{
				string file = improvementName.ToLower().Replace(' ', '_') + ".png";
				string path = Path.Combine(Settings.Instance.DataDirectory, "improvement_art", file);
				return File.Exists(path) ? path : null;
			}
			catch { return null; }
		}

		private static Palette BuildPalette()
		{
			Palette p = Common.DefaultPalette;
			CassetteTheme.ApplyTo(p);
			return p;
		}

		private static byte[,] ConvertToIndices(byte[] rgba, int w, int h, Palette pal)
		{
			// Cache palette entries to avoid repeated unmanaged reads.
			var pr = new byte[pal.Length];
			var pg = new byte[pal.Length];
			var pb = new byte[pal.Length];
			var pa = new byte[pal.Length];
			for (int i = 0; i < pal.Length; i++)
			{
				Colour c = pal[i];
				pa[i] = c.A; pr[i] = c.R; pg[i] = c.G; pb[i] = c.B;
			}

			// Cache nearest-palette lookups so repeated colors cost O(1).
			var memo = new Dictionary<int, byte>();

			var indices = new byte[h, w];
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				int i = (y * w + x) * 4;
				if (rgba[i + 3] < 128) { indices[y, x] = CassetteTheme.BG0; continue; }

				int key = (rgba[i] << 16) | (rgba[i + 1] << 8) | rgba[i + 2];
				if (!memo.TryGetValue(key, out byte idx))
				{
					byte r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
					idx = CassetteTheme.BG0;
					int best = int.MaxValue;
					for (int j = 1; j < pal.Length; j++)
					{
						if (pa[j] == 0) continue;
						int dr = r - pr[j], dg = g - pg[j], db = b - pb[j];
						int dist = dr * dr + dg * dg + db * db;
						if (dist < best) { best = dist; idx = (byte)j; }
					}
					memo[key] = idx;
				}
				indices[y, x] = idx;
			}
			return indices;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			this.CassetteBackground();

			if (_indices != null)
			{
				int ox = Math.Max(0, (Width  - _imgW) / 2);
				int oy = Math.Max(0, (Height - _imgH) / 2);
				int dw = Math.Min(_imgW, Width  - ox);
				int dh = Math.Min(_imgH, Height - oy);
				for (int y = 0; y < dh; y++)
				for (int x = 0; x < dw; x++)
					Bitmap[ox + x, oy + y] = _indices[y, x];
			}

			this.AddScanlines();

			// Subtle dismiss hint at bottom
			string hint = "[ ANY KEY OR CLICK TO CONTINUE ]";
			this.DrawText(hint, 0, CassetteTheme.BORDER, Width / 2, Height - 9, TextAlign.Center);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)  { Destroy(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		internal ImprovementArtScreen(string artPath, string improvementName)
		{
			_name = improvementName;
			OnResize += (s, e) => _update = true;

			using (Palette pal = BuildPalette())
			{
				Palette = pal;

				byte[] rgba = PngFile.ReadRgba(artPath, out _imgW, out _imgH);
				if (rgba != null)
					_indices = ConvertToIndices(rgba, _imgW, _imgH, pal);
			}
		}
	}
}
