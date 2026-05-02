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
using CivOne.Graphics;

namespace CivOne
{
	// Minimal PNG decoder supporting 8-bit RGB (type 2), Indexed (type 3), and RGBA (type 6).
	// Interlaced images are not supported.
	internal static class PngDecoder
	{
		private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

		public static SplashData Load(string path)
		{
			if (!File.Exists(path)) return null;
			try
			{
				return Decode(File.ReadAllBytes(path));
			}
			catch { return null; }
		}

		private static SplashData Decode(byte[] data)
		{
			for (int i = 0; i < 8; i++)
				if (data[i] != Signature[i]) return null;

			int pos = 8, width = 0, height = 0, colorType = 0, bitDepth = 0;
			byte[] plte = null;
			var idatData = new List<byte[]>();

			while (pos + 12 <= data.Length)
			{
				int len = ReadBE32(data, pos); pos += 4;
				string type = System.Text.Encoding.ASCII.GetString(data, pos, 4); pos += 4;
				byte[] chunk = new byte[len];
				if (len > 0) Buffer.BlockCopy(data, pos, chunk, 0, len);
				pos += len + 4; // skip CRC

				switch (type)
				{
					case "IHDR":
						width = ReadBE32(chunk, 0);
						height = ReadBE32(chunk, 4);
						bitDepth = chunk[8];
						colorType = chunk[9];
						break;
					case "PLTE":
						plte = chunk;
						break;
					case "IDAT":
						idatData.Add(chunk);
						break;
					case "IEND":
						goto done;
				}
			}
			done:

			if (width <= 0 || height <= 0 || bitDepth != 8) return null;
			if (colorType != 2 && colorType != 3 && colorType != 6) return null;
			if (colorType == 3 && plte == null) return null;

			byte[] compressed = Concat(idatData);
			int bpp = colorType == 6 ? 4 : (colorType == 2 ? 3 : 1);
			byte[] raw = Decompress(compressed, height * (width * bpp + 1));

			byte[] rgba = new byte[width * height * 4];
			Unfilter(raw, rgba, width, height, bpp, colorType, plte);
			return new SplashData(width, height, rgba);
		}

		private static byte[] Concat(List<byte[]> chunks)
		{
			int total = 0;
			foreach (var c in chunks) total += c.Length;
			byte[] result = new byte[total];
			int offset = 0;
			foreach (var c in chunks) { Buffer.BlockCopy(c, 0, result, offset, c.Length); offset += c.Length; }
			return result;
		}

		private static byte[] Decompress(byte[] data, int expectedSize)
		{
			// PNG zlib stream: skip 2-byte header (CMF + FLG), then raw deflate
			using (var ms = new MemoryStream(data, 2, data.Length - 2))
			using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
			using (var output = new MemoryStream(expectedSize))
			{
				ds.CopyTo(output);
				return output.ToArray();
			}
		}

		private static void Unfilter(byte[] raw, byte[] rgba, int w, int h, int bpp, int colorType, byte[] plte)
		{
			int stride = w * bpp;
			byte[] prev = new byte[stride];
			byte[] curr = new byte[stride];

			for (int y = 0; y < h; y++)
			{
				int rawBase = y * (stride + 1);
				int filter = raw[rawBase];
				Buffer.BlockCopy(raw, rawBase + 1, curr, 0, stride);

				for (int x = 0; x < stride; x++)
				{
					byte a = x >= bpp ? curr[x - bpp] : (byte)0;
					byte b = prev[x];
					byte c = x >= bpp ? prev[x - bpp] : (byte)0;
					switch (filter)
					{
						case 1: curr[x] += a; break;
						case 2: curr[x] += b; break;
						case 3: curr[x] = (byte)(curr[x] + (a + b) / 2); break;
						case 4: curr[x] += Paeth(a, b, c); break;
					}
				}

				// Convert to RGBA
				int rgbaBase = y * w * 4;
				if (colorType == 6) // RGBA
				{
					Buffer.BlockCopy(curr, 0, rgba, rgbaBase, stride);
				}
				else if (colorType == 2) // RGB
				{
					for (int x = 0; x < w; x++)
					{
						rgba[rgbaBase + x * 4]     = curr[x * 3];
						rgba[rgbaBase + x * 4 + 1] = curr[x * 3 + 1];
						rgba[rgbaBase + x * 4 + 2] = curr[x * 3 + 2];
						rgba[rgbaBase + x * 4 + 3] = 255;
					}
				}
				else // Indexed (type 3)
				{
					for (int x = 0; x < w; x++)
					{
						int pi = curr[x] * 3;
						rgba[rgbaBase + x * 4]     = plte[pi];
						rgba[rgbaBase + x * 4 + 1] = plte[pi + 1];
						rgba[rgbaBase + x * 4 + 2] = plte[pi + 2];
						rgba[rgbaBase + x * 4 + 3] = 255;
					}
				}

				// Swap prev/curr
				byte[] tmp = prev; prev = curr; curr = tmp;
				Array.Clear(curr, 0, stride);
			}
		}

		private static byte Paeth(byte a, byte b, byte c)
		{
			int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
			return pa <= pb && pa <= pc ? a : (pb <= pc ? b : c);
		}

		private static int ReadBE32(byte[] d, int o) =>
			(d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
	}
}
