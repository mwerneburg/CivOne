// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
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
		private readonly string _cityName;
		private readonly byte[] _rawRgba = null!;
		private readonly int _rawW, _rawH;
		private byte[,] _indices = null!;
		private int _builtForW = -1, _builtForH = -1;
		private int _scaledW, _scaledH;
		private bool _update = true;

		internal static string? FindArtPath(string improvementName)
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

		// Box-filter scale from srcW×srcH to dstW×dstH RGBA.
		private static byte[] ScaleRgba(byte[] src, int srcW, int srcH, int dstW, int dstH)
		{
			byte[] dst = new byte[dstW * dstH * 4];
			for (int dy = 0; dy < dstH; dy++)
			for (int dx = 0; dx < dstW; dx++)
			{
				int x0 = dx * srcW / dstW, x1 = (dx + 1) * srcW / dstW;
				int y0 = dy * srcH / dstH, y1 = (dy + 1) * srcH / dstH;
				if (x1 == x0) x1++;
				if (y1 == y0) y1++;
				long r = 0, g = 0, b = 0, a = 0, n = 0;
				for (int sy = y0; sy < y1; sy++)
				for (int sx = x0; sx < x1; sx++)
				{
					int i = (sy * srcW + sx) * 4;
					r += src[i]; g += src[i+1]; b += src[i+2]; a += src[i+3]; n++;
				}
				int oi = (dy * dstW + dx) * 4;
				dst[oi] = (byte)(r/n); dst[oi+1] = (byte)(g/n); dst[oi+2] = (byte)(b/n); dst[oi+3] = (byte)(a/n);
			}
			return dst;
		}

		private void RebuildIndices()
		{
			float scale = Math.Min((float)Width / _rawW, (float)Height / _rawH);
			_scaledW = Math.Max(1, (int)(_rawW * scale));
			_scaledH = Math.Max(1, (int)(_rawH * scale));
			byte[] scaled = ScaleRgba(_rawRgba, _rawW, _rawH, _scaledW, _scaledH);
			using (Palette pal = BuildPalette())
				_indices = PngFile.ToIndices(scaled, _scaledW, _scaledH, pal);
			_builtForW = Width;
			_builtForH = Height;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			if (_rawRgba is not null && (Width != _builtForW || Height != _builtForH))
				RebuildIndices();

			this.CassetteBackground();

			if (_indices is not null)
			{
				int ox = (Width  - _scaledW) / 2;
				int oy = (Height - _scaledH) / 2;
				for (int y = 0; y < _scaledH; y++)
				for (int x = 0; x < _scaledW; x++)
					Bitmap[ox + x, oy + y] = _indices[y, x];
			}

			this.AddScanlines();

			this.DrawText($"{_cityName} has built {_name}.", 0, CassetteTheme.INK_HIGH, Width / 2, Height - 9, TextAlign.Center);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)  { Destroy(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		internal ImprovementArtScreen(string artPath, string improvementName, string cityName)
		{
			_name = improvementName;
			_cityName = cityName;
			OnResize += (s, e) => _update = true;

			using Palette pal = BuildPalette();
			Palette = pal;
			_rawRgba = PngFile.ReadRgba(artPath, out _rawW, out _rawH);
		}
	}
}
