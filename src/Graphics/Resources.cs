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
using CivOne.Enums;
using CivOne.Graphics.ImageFormats;
using CivOne.IO;
using CivOne.Tiles;

namespace CivOne.Graphics
{
	public class Resources
	{
		private static Settings Settings => Settings.Instance;

		private static void Log(string text, params object[] parameters) => RuntimeHandler.Runtime.Log(text, parameters);

		private readonly Dictionary<string, Picture> _cache = new();
		private readonly Dictionary<string, Bytemap> _textCache = new();
		private readonly IFont _defaultFont = new DefaultFont();
		private readonly List<Fontset> _fonts = new();
		private readonly Dictionary<Direction, IBitmap> _fog = new();
		
		internal void ClearTextCache() => _textCache.Clear();
		
		private void LoadFonts()
		{
			byte[] file;
			string filename = Path.Combine(Settings.DataDirectory, "FONTS.CV");
			if (!File.Exists(filename))
			{
				Log("Font file not found, fallback to default font");
				return;
			}

			using (FileStream fs = new FileStream(filename, FileMode.Open))
			{
				file = new byte[fs.Length];
				fs.Read(file, 0, file.Length);
			}
			
			List<ushort> fontOffsets = new();
			int index = 0;
			uint fontCount = BitConverter.ToUInt16(file, index);
			index += 2;
			
			for (int i = 0; i < fontCount; i++)
			{
				fontOffsets.Add(BitConverter.ToUInt16(file, index));
				index += 2;
			}
			
			foreach (ushort offset in fontOffsets)
			{
				_fonts.Add(new Fontset(file, offset));
			}
		}
		
		public bool ValidCharacter(int fontId, char c)
		{
			byte asciiChar = (byte)c;
			return (asciiChar >= Font(fontId).FirstChar && asciiChar <= Font(fontId).LastChar);
		}
		
		public Size GetTextSize(int font, string text)
		{
			int width = 0, height = 0;
			foreach (char c in text)
			{
				Size size = GetLetterSize(font, c);
				width += size.Width + 1;
				if (height < size.Height) height = size.Height;
			}
			return new Size(width, height);
		}
		
		public Picture GetText(string text, int font, byte colour)
		{
			return GetText(text, font, colour, colour);
		}
		
		public Picture GetText(string text, int font, byte colourFirstLetter, byte colour)
		{
			if (text is null) text = "[MISSING STRING]";

			List<Bytemap> letters = new();
			bool isFirstLetter = true;
			foreach (char c in text)
			{
				letters.Add(GetLetter(isFirstLetter ? colourFirstLetter : colour, font, c));
				isFirstLetter = false;
			}
			
			int width = 0, height = 0;
			foreach (Bytemap letter in letters)
			{
				width += letter.Width + 1;
				if (height < letter.Height) height = letter.Height;
			}
			
			Picture output = new Picture(width, height);
			
			int xx = 0;
			foreach (Bytemap letter in letters)
			{
				output.AddLayer(letter, xx, 0);
				xx += letter.Width + 1;
			}
			
			return output;
		}
		
		internal Size GetLetterSize(int font, char letter) => GetLetter(5, font, letter).Size;

		private IFont Font(int font)
		{
			if (font < 0 || (_fonts.Count - 1) < font)
				return _defaultFont;
			return _fonts[font];
		}
		
		public int GetFontHeight(int font)
		{
			return Font(font).FontHeight;
		}
		
		private Bytemap GetLetter(byte colour, int font, char letter)
		{
			string key = $"letter{colour}|{font}|{letter}";
			if (!_textCache.ContainsKey(key))
			{
				// Characters above ASCII 127 are special glyphs (♥, ★, etc.) defined only
				// in DefaultFont; the Civ1 bitmap fonts only cover 32–127.
				IFont f = letter > 127 ? _defaultFont : Font(font);
				_textCache.Add(key, f.GetLetter(letter, colour));
			}
			return _textCache[key];
		}

		public bool Exists(string filename)
		{
			if (RuntimeHandler.Runtime.Settings.Free) return false;
			return PicFile.Exists(filename);
		}
		
		internal string[] GetCivilopediaText(string name)
		{
			List<string> textLines = new();
			string text = string.Join(" ", TextFile.Instance.GetGameText(name));
			string t = "";
			while (text.Length > 0)
			{
				int space = text.IndexOf(' ');
				// Last segment: no space left, so this is the tail of the entry. It must be
				// FLUSHED here — the original reached the flush below by falling through, and
				// an early `continue` silently dropped the final line.
				if (space == -1)
				{
					if (t.Length > 0 && GetTextSize(6, string.Join(" ", t, text)).Width < 294)
					{
						textLines.Add(string.Join(" ", t, text));
					}
					else
					{
						if (t.Length > 0) textLines.Add(t);
						textLines.Add(text);
					}
					t = "";
					text = "";
					continue;
				}

				string word = text.Substring(0, space);
				if (GetTextSize(6, t + word).Width < 294)
				{
					if (t.Length > 0) t += " ";
					t += word;
					text = text.Substring(space).Trim();
					continue;
				}

				// The word does not fit on the current line. If the line already holds
				// something, flush it and try the word again on a fresh one.
				if (t.Length > 0)
				{
					textLines.Add(t);
					t = "";
					continue;
				}

				// ...but if the line is ALREADY empty the word cannot be made to fit at all,
				// and this is where the loop used to hang: the old code flushed `t`, set it to
				// "", and went round again WITHOUT consuming any of `text` — so a word wider
				// than the column produced an infinite loop appending empty strings to a
				// growing list. At font 6 a glyph is 10px and the column is 294, so any
				// 30-character token does it.
				//
				// Observed as a hard hang mid-game: 100% of a core, memory climbing, and a
				// window that would not even repaint its cursor, because the main thread never
				// returned from here. The headless harness sails past it, since nothing there
				// renders text.
				//
				// Emit the over-long word on a line of its own and consume it. It will overrun
				// the column, which is a cosmetic fault; not consuming it is a frozen game.
				textLines.Add(word);
				text = text.Substring(space).Trim();
			}
			return textLines.ToArray();
		}
		
		private static Picture _worldMapTiles = null!;
		public static Picture WorldMapTiles
		{
			get
			{
				if (_worldMapTiles is null)
				{
					Picture sp299 = Instance["SP299"];
					_worldMapTiles = new Picture(48, 8, sp299.Palette);
					_worldMapTiles.AddLayer(sp299[160, 111, 48, 8]);
				}
				return _worldMapTiles;
			}
		}

		public Picture this[string filename]
		{
			get
			{
				string key = filename.ToUpper();
				if (_cache.ContainsKey(key))
				{
					return new Picture(_cache[key].Bitmap, _cache[key].Palette);
				}
				
				Picture? output = null;
				PicFile picFile = new PicFile(filename);
				if ((Settings.GraphicsMode == GraphicsMode.Graphics256 && picFile.GetPicture256 is not null) || picFile.GetPicture16 is null)
				{
					output = new Picture(picFile.GetPicture256!, picFile.GetPalette256);
				}
				else
				{
					output = new Picture(picFile.GetPicture16, picFile.GetPalette16);
				}
				
				if (!_cache.ContainsKey(key)) _cache.Add(key, output);
				return new Picture(_cache[key].Bitmap, _cache[key].Palette);
			}
		}

		// Runtime-injected images (set by the SDL/API layer at startup)
		public static IBitmap SpacedockImage { get; set; } = null!;
		public static SplashData SplashRawImage { get; set; } = null!;

		private static Resources _instance = null!;
		public static Resources Instance
		{
			get
			{
				if (_instance is null)
				{
					_instance = new Resources();
				}
				return _instance;
			}
		}

		public static void ClearInstance()
		{
			_instance = null!;
			_worldMapTiles = null!;
			PicFile.ClearCache();
			TextFile.ClearInstance();
			Sprites.Cursor.ClearCache();
		}
		
		private Resources()
		{
			if (!RuntimeHandler.Runtime.Settings.Free) LoadFonts();
		}
	}
}