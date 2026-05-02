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
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;

namespace CivOne.Screens
{
	[Expand]
	internal class Splash : BaseScreen
	{
		private const int AutoAdvanceTicks = 300; // ~5 seconds

		private Picture _picture;
		private int _ticks;

		private struct PixelEntry
		{
			public byte R, G, B;
			public int Idx;
			public PixelEntry(byte r, byte g, byte b, int idx) { R = r; G = g; B = b; Idx = idx; }
		}

		// Box-filter downscale (or upscale) from srcW×srcH RGBA to dstW×dstH RGBA.
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
					r += src[i]; g += src[i + 1]; b += src[i + 2]; a += src[i + 3]; n++;
				}
				int oi = (dy * dstW + dx) * 4;
				dst[oi] = (byte)(r / n); dst[oi + 1] = (byte)(g / n); dst[oi + 2] = (byte)(b / n); dst[oi + 3] = (byte)(a / n);
			}
			return dst;
		}

		// Median-cut quantization: RGBA pixels → 256-colour indexed Picture.
		private static Picture Quantize(byte[] rgba, int w, int h)
		{
			int n = w * h;
			var pixels = new List<PixelEntry>(n);
			for (int i = 0; i < n; i++)
			{
				int o = i * 4;
				pixels.Add(new PixelEntry(rgba[o], rgba[o + 1], rgba[o + 2], i));
			}

			var buckets = new List<List<PixelEntry>> { pixels };

			while (buckets.Count < 256)
			{
				int bestIdx = -1, bestRange = 0;
				for (int i = 0; i < buckets.Count; i++)
				{
					var b = buckets[i];
					if (b.Count <= 1) continue;
					byte lo0 = 255, hi0 = 0, lo1 = 255, hi1 = 0, lo2 = 255, hi2 = 0;
					for (int j = 0; j < b.Count; j++)
					{
						var p = b[j];
						if (p.R < lo0) lo0 = p.R; if (p.R > hi0) hi0 = p.R;
						if (p.G < lo1) lo1 = p.G; if (p.G > hi1) hi1 = p.G;
						if (p.B < lo2) lo2 = p.B; if (p.B > hi2) hi2 = p.B;
					}
					int range = Math.Max(hi0 - lo0, Math.Max(hi1 - lo1, hi2 - lo2));
					if (range > bestRange) { bestRange = range; bestIdx = i; }
				}

				if (bestIdx < 0 || bestRange == 0) break;

				var bucket = buckets[bestIdx];
				byte rLo = 255, rHi = 0, gLo = 255, gHi = 0, bLo = 255, bHi = 0;
				for (int j = 0; j < bucket.Count; j++)
				{
					var p = bucket[j];
					if (p.R < rLo) rLo = p.R; if (p.R > rHi) rHi = p.R;
					if (p.G < gLo) gLo = p.G; if (p.G > gHi) gHi = p.G;
					if (p.B < bLo) bLo = p.B; if (p.B > bHi) bHi = p.B;
				}
				int rR = rHi - rLo, gR = gHi - gLo, bR = bHi - bLo;
				int ch = rR >= gR && rR >= bR ? 0 : (gR >= bR ? 1 : 2);

				if (ch == 0) bucket.Sort((a, b2) => a.R - b2.R);
				else if (ch == 1) bucket.Sort((a, b2) => a.G - b2.G);
				else bucket.Sort((a, b2) => a.B - b2.B);

				int mid = bucket.Count / 2;
				buckets.RemoveAt(bestIdx);
				buckets.Add(bucket.GetRange(0, mid));
				buckets.Add(bucket.GetRange(mid, bucket.Count - mid));
			}

			var palette = new Colour[256];
			var indexed = new byte[n];
			for (int bi = 0; bi < buckets.Count && bi < 256; bi++)
			{
				var b = buckets[bi];
				long rs = 0, gs = 0, bs2 = 0;
				for (int j = 0; j < b.Count; j++) { rs += b[j].R; gs += b[j].G; bs2 += b[j].B; }
				int cnt = b.Count;
				palette[bi] = new Colour((byte)(rs / cnt), (byte)(gs / cnt), (byte)(bs2 / cnt));
				for (int j = 0; j < b.Count; j++) indexed[b[j].Idx] = (byte)bi;
			}

			var bytemap = new Bytemap(w, h);
			for (int i = 0; i < n; i++) bytemap[i % w, i / w] = indexed[i];
			return new Picture(bytemap, (Palette)palette);
		}

		private void Build()
		{
			SplashData raw = Resources.SplashRawImage;
			if (raw == null) return;
			byte[] scaled = ScaleRgba(raw.Rgba, raw.Width, raw.Height, Width, Height);
			_picture = Quantize(scaled, Width, Height);
			Palette = _picture.Palette;
			this.AddLayer(_picture, 0, 0);
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (_picture == null)
			{
				Build();
				if (_picture == null)
				{
					Destroy();
					Common.AddScreen(new NewGame());
					return false;
				}
				return true;
			}

			_ticks++;
			if (_ticks >= AutoAdvanceTicks)
			{
				Destroy();
				Common.AddScreen(new NewGame());
			}
			return false;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			Destroy();
			Common.AddScreen(new NewGame());
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			Common.AddScreen(new NewGame());
			return true;
		}

		public void Resize(object sender, ResizeEventArgs args)
		{
			_picture = null;
			Bitmap.Clear();
		}

		public Splash()
		{
			OnResize += Resize;
		}
	}
}
