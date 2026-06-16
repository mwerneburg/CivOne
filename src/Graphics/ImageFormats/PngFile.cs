#nullable enable
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
using System.IO.Compression;
using System.Text;

namespace CivOne.Graphics.ImageFormats
{
	// Minimal PNG decoder. Supports 8-bit depth (no interlacing, no 16-bit).
	// Color types: 0 grayscale, 2 RGB, 3 indexed, 4 gray+alpha, 6 RGBA.
	internal static class PngFile
	{
		// Convert RGBA bytes (from ReadRgba) to palette indices.
		// Transparent pixels (alpha < 128) become CassetteTheme.BG0.
		// Repeated colours are memoized so images with few unique tones convert quickly.
		internal static byte[,] ToIndices(byte[] rgba, int w, int h, Palette pal)
		{
			var pr = new byte[pal.Length];
			var pg = new byte[pal.Length];
			var pb = new byte[pal.Length];
			var pa = new byte[pal.Length];
			for (int i = 0; i < pal.Length; i++)
			{
				Colour c = pal[i];
				pa[i] = c.A; pr[i] = c.R; pg[i] = c.G; pb[i] = c.B;
			}

			var memo = new Dictionary<int, byte>();
			var out_ = new byte[h, w];
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				int i = (y * w + x) * 4;
				if (rgba[i + 3] < 128) { out_[y, x] = CassetteTheme.BG0; continue; }

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
				out_[y, x] = idx;
			}
			return out_;
		}


		private static readonly byte[] _sig = { 137, 80, 78, 71, 13, 10, 26, 10 };

		// Returns flat RGBA bytes (width*height*4), or null on failure.
		internal static byte[] ReadRgba(string path, out int width, out int height)
		{
			width = height = 0;
			try
			{
				return Decode(File.ReadAllBytes(path), out width, out height);
			}
			catch { return null!; }
		}

		private static int ReadBE32(byte[] d, int i)
			=> (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

		private static byte Paeth(byte a, byte b, byte c)
		{
			int p = a + b - c;
			int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
			return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
		}

		private static byte[] Decode(byte[] data, out int width, out int height)
		{
			width = height = 0;
			for (int i = 0; i < 8; i++)
				if (data[i] != _sig[i]) return null!;

			int w = 0, h = 0, bitDepth = 0, colorType = 0;
			byte[]? plt = null;
			var idatBlocks = new List<byte[]>();

			int pos = 8;
			while (pos + 8 <= data.Length)
			{
				int len       = ReadBE32(data, pos);
				string type   = Encoding.ASCII.GetString(data, pos + 4, 4);
				int dataStart = pos + 8;
				pos           = dataStart + len + 4; // advance past data + CRC

				switch (type)
				{
					case "IHDR":
						w         = ReadBE32(data, dataStart);
						h         = ReadBE32(data, dataStart + 4);
						bitDepth  = data[dataStart + 8];
						colorType = data[dataStart + 9];
						if (data[dataStart + 12] != 0) return null!; // no Adam7
						break;
					case "PLTE":
						plt = new byte[len];
						Array.Copy(data, dataStart, plt, 0, len);
						break;
					case "IDAT":
						var block = new byte[len];
						Array.Copy(data, dataStart, block, 0, len);
						idatBlocks.Add(block);
						break;
					case "IEND":
						goto done;
				}
			}
			done:

			if (w == 0 || h == 0 || bitDepth != 8 || idatBlocks.Count == 0) return null!;

			// Concatenate IDAT blocks and decompress (skip 2-byte zlib header)
			int total = 0;
			foreach (var b in idatBlocks) total += b.Length;
			if (total <= 2) return null!;

			byte[] idat = new byte[total];
			int off = 0;
			foreach (var b in idatBlocks) { Array.Copy(b, 0, idat, off, b.Length); off += b.Length; }

			byte[] raw;
			using (var src = new MemoryStream(idat, 2, idat.Length - 2))
			using (var def = new DeflateStream(src, CompressionMode.Decompress))
			using (var dst = new MemoryStream())
			{
				def.CopyTo(dst);
				raw = dst.ToArray();
			}

			int bpp    = colorType == 0 ? 1 : colorType == 2 ? 3 : colorType == 3 ? 1 : colorType == 4 ? 2 : 4;
			int stride = w * bpp;
			byte[] result = new byte[w * h * 4];
			byte[] prior  = new byte[stride];

			for (int y = 0; y < h; y++)
			{
				int rowOff = y * (stride + 1);
				byte filter = raw[rowOff];
				byte[] row  = new byte[stride];
				Array.Copy(raw, rowOff + 1, row, 0, stride);

				switch (filter)
				{
					case 1:
						for (int x = bpp; x < stride; x++)
							row[x] = (byte)(row[x] + row[x - bpp]);
						break;
					case 2:
						for (int x = 0; x < stride; x++)
							row[x] = (byte)(row[x] + prior[x]);
						break;
					case 3:
						for (int x = 0; x < stride; x++)
						{
							byte left = x >= bpp ? row[x - bpp] : (byte)0;
							row[x] = (byte)(row[x] + (left + prior[x]) / 2);
						}
						break;
					case 4:
						for (int x = 0; x < stride; x++)
						{
							byte a = x >= bpp ? row[x - bpp] : (byte)0;
							byte c = x >= bpp ? prior[x - bpp] : (byte)0;
							row[x] = (byte)(row[x] + Paeth(a, prior[x], c));
						}
						break;
				}
				Array.Copy(row, prior, stride);

				for (int x = 0; x < w; x++)
				{
					int di = (y * w + x) * 4;
					switch (colorType)
					{
						case 0:
							result[di] = result[di + 1] = result[di + 2] = row[x];
							result[di + 3] = 255;
							break;
						case 2:
							result[di]     = row[x * 3];
							result[di + 1] = row[x * 3 + 1];
							result[di + 2] = row[x * 3 + 2];
							result[di + 3] = 255;
							break;
						case 3:
							int pi = row[x] * 3;
							if (plt is not null && pi + 2 < plt.Length)
							{
								result[di] = plt[pi]; result[di + 1] = plt[pi + 1]; result[di + 2] = plt[pi + 2];
							}
							result[di + 3] = 255;
							break;
						case 4:
							result[di] = result[di + 1] = result[di + 2] = row[x * 2];
							result[di + 3] = row[x * 2 + 1];
							break;
						case 6:
							result[di]     = row[x * 4];
							result[di + 1] = row[x * 4 + 1];
							result[di + 2] = row[x * 4 + 2];
							result[di + 3] = row[x * 4 + 3];
							break;
					}
				}
			}

			width = w; height = h;
			return result;
		}
	}
}