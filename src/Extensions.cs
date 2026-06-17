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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;
using CivOne.IO;
using CivOne.Leaders;

namespace CivOne
{
	internal static class Extensions
	{
		private static Settings Settings => Settings.Instance;

		public static byte[] Clear(this byte[] byteArray, byte value = 0)
		{
			for (int i = byteArray.GetUpperBound(0); i >= 0; i--)
				byteArray[i] = value;
			return byteArray;
		}

		public static string ToString(this byte[] bytes, int startIndex, int length)
		{
			StringBuilder output = new StringBuilder();
			for (int i = startIndex; i < startIndex + length; i++)
			{
				if (bytes[i] == 0) break;
				output.Append((char)bytes[i]);
			}
			return output.ToString().Trim();
		}

		public static IEnumerable<byte> FromBitIds(this byte[] bytes, int startIndex, int length)
		{
			byte index = 0;
			for (int i = startIndex; i < startIndex + length; i++)
			for (int b = 0; b < 8; b++)
			{
				if ((bytes[i] & (1 << b)) > 0) yield return index;
				index++;
			}
		}

		public static byte[] ToBitIds(this byte[] bytes, int startIndex, int length, byte[] values)
		{
			foreach (byte value in values)
			{
				int bitNo = value % 8;
				int byteNo = (value - bitNo) / 8;
				if (length <= byteNo) continue;
				bytes[startIndex + byteNo] |= (byte)(1 << bitNo);
			}
			return bytes;
		}

		public static string YesNo(this bool value) => value ? "Yes" : "No";
		public static string OnOff(this bool value) => value ? "On" : "Off";
		public static string EnabledDisabled(this bool value) => value ? "Enabled" : "Disabled";

		public static string ToText(this AspectRatio aspectRatio)
		{
			switch (aspectRatio)
			{
				case AspectRatio.Auto: return "Automatic";
				case AspectRatio.Fixed: return "Fixed";
				case AspectRatio.Scaled: return "Scaled (blurry)";
				case AspectRatio.ScaledFixed: return "Scaled and fixed (blurry)";
				case AspectRatio.Expand: return "Expand (experimental)";
				default: return null!;
			}
		}

		public static string ToText(this GraphicsMode graphicsMode)
		{
			switch (graphicsMode)
			{
				case GraphicsMode.Graphics256: return "256 colors";
				case GraphicsMode.Graphics16: return "16 colors";
				default: return null!;
			}
		}

		public static string ToText(this CursorType cursorType)
		{
			switch (cursorType)
			{
				case CursorType.Default: return "Default";
				case CursorType.Builtin: return "Built-in";
				case CursorType.Native: return "Native";
				default: return null!;
			}
		}

		public static string ToText(this DestroyAnimation destroyAnimation)
		{
			switch (destroyAnimation)
			{
				case DestroyAnimation.Sprites: return "Sprites (original)";
				case DestroyAnimation.Noise: return "Noise";
				default: return null!;
			}
		}

		public static string ToText(this GameOption gameOption)
		{
			switch (gameOption)
			{
				case GameOption.Default: return "Default";
				case GameOption.On: return "On";
				case GameOption.Off: return "Off";
				default: return null!;
			}
		}

		public static string ToText(this AggressionLevel aggression)
		{
			switch (aggression)
			{
				case AggressionLevel.Friendly: return "Friendly";
				case AggressionLevel.Normal: return "Normal";
				case AggressionLevel.Aggressive: return "Aggressive";
				default: return null!;
			}
		}

		public static string ToText(this DevelopmentLevel development)
		{
			switch (development)
			{
				case DevelopmentLevel.Perfectionist: return "Perfectionist";
				case DevelopmentLevel.Normal: return "Normal";
				case DevelopmentLevel.Expansionistic: return "Expansionistic";
				default: return null!;
			}
		}

		public static string ToText(this MilitarismLevel militarism)
		{
			switch (militarism)
			{
				case MilitarismLevel.Civilized: return "Civilized";
				case MilitarismLevel.Normal: return "Normal";
				case MilitarismLevel.Militaristic: return "Militaristic";
				default: return null!;
			}
		}

		public static string[] Traits(this ILeader leader)
		{
			List<string> output = new();
			if (leader.Aggression != AggressionLevel.Normal) output.Add(leader.Aggression.ToText());
			if (leader.Development != DevelopmentLevel.Normal) output.Add(leader.Development.ToString());
			if (leader.Militarism != MilitarismLevel.Normal) output.Add(leader.Militarism.ToString());
			return output.ToArray();
		}

		public static IAdvance ToInstance(this Advance advance) => Common.Advances.FirstOrDefault(x => x.Id == (byte)advance);
		public static ILeader? ToInstance(this Leader leader)
		{
			switch (leader)
			{
				case Leader.Atilla:        return new Atilla();
				case Leader.Caesar:        return new Caesar();
				case Leader.Hammurabi:     return new Hammurabi();
				case Leader.Frederick:     return new Frederick();
				case Leader.Ramesses:      return new Ramesses();
				case Leader.Lincoln:       return new Lincoln();
				case Leader.Alexander:     return new Alexander();
				case Leader.Gandhi:        return new Gandhi();
				case Leader.Shaka:         return new Shaka();
				case Leader.Napoleon:      return new Napoleon();
				case Leader.Montezuma:     return new Montezuma();
				case Leader.Elizabeth:     return new Elizabeth();
				case Leader.Genghis:       return new Genghis();
				case Leader.Peter:         return new Peter();
				case Leader.Deng:          return new Deng();
				case Leader.OlvirCouncil:  return new OlvirCouncil();
				case Leader.Tokugawa:      return new Tokugawa();
				case Leader.Cyrus:         return new Cyrus();
				case Leader.SittingBull:   return new SittingBull();
				case Leader.HarunAlRashid: return new HarunAlRashid();
				case Leader.Jayavarman:    return new Jayavarman();
				case Leader.MansaMusa:     return new MansaMusa();
				case Leader.Pachacuti:     return new Pachacuti();
				case Leader.Suleiman:      return new Suleiman();
				case Leader.HaileSelassie: return new HaileSelassie();
				case Leader.Hiawatha:      return new Hiawatha();
				default: return null;
			}
		}

		public static IBitmap? GifToBitmap(this byte[] buffer)
		{
			using (GifFile gifFile = new GifFile(buffer))
			{
				return gifFile.GetBitmap();
			}
		}

		public static Bytemap MatchColours(this IBitmap input, Palette palette, int startIndex, int length)
		{
			Dictionary<int, int> matches = new();

			Colour[] pal = input.Palette.Entries.ToArray();
			Colour[] cmp = palette.Entries.ToArray();
			for (int i = 0; i < pal.Length; i++)
			{
				if (pal[i].A == 0)
				{
					matches.Add(i, 0);
					continue;
				}

				int entry = 0;
				int mx = 768;
				for (int j = startIndex; j < cmp.Length && j < (startIndex + length); j++)
				{
					int total = Math.Abs((int)pal[i].R - cmp[j].R) + Math.Abs((int)pal[i].G - cmp[j].G) + Math.Abs((int)pal[i].B - cmp[j].B);
					if (total >= mx) continue;
					entry = j;
					mx = total;
				}
				matches.Add(i, entry);
			}
			
			Bytemap output = new Bytemap(input.Width(), input.Height());
			for (int yy = 0; yy < input.Height(); yy++)
			for (int xx = 0; xx < input.Height(); xx++)
			{
				output[xx, yy] = (byte)matches[input.Bitmap[xx, yy]];
			}
			return output;
		}

		public static Picture MakePalette(this IBitmap bitmap, int startIndex, int colourLength)
		{
			Dictionary<byte, int> colourCount = new();
			foreach (byte colourIndex in bitmap.Bitmap.ToByteArray())
			{
				if (bitmap.Palette[colourIndex].A == 0) continue; // Do not count transparent
				if (colourCount.ContainsKey(colourIndex))
				{
					colourCount[colourIndex]++;
					continue;
				}
				colourCount.Add(colourIndex, 1);
			}

			Colour[] colours = colourCount.OrderByDescending(x => x.Value).Select(x => bitmap.Palette[x.Key]).Take(colourLength).ToArray();
			Colour[] palette;
			if (Settings.GraphicsMode == GraphicsMode.Graphics256)
			{
				palette = new Colour[256];
				palette[0] = Colour.Transparent;
				Array.Copy(colours, 0, palette, startIndex, Math.Min(colourLength, colours.Length));
			}
			else
			{
				palette = Common.GetPalette16.Entries.ToArray();
			}
			
			Bytemap bytemap = bitmap.MatchColours(palette, startIndex, colourLength);
			return new Picture(bytemap, palette);
		}
	}
}