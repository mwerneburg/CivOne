// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CivOne.IO
{
	internal class TextFile
	{
		private static void Log(string text, params object[] parameters) => RuntimeHandler.Runtime.Log(text, parameters);

		private readonly string[] TEXT_FILES = ["BLURB0", "BLURB1", "BLURB2", "BLURB3", "BLURB4", "ERROR", "HELP", "KING", "PRODUCE"];
		private readonly Dictionary<string, string[]> _gameTexts = new();
		
		public string[] LoadArray(string filename)
		{
			filename += ".TXT";
			
			Regex rgx = new Regex("[^a-zA-Z0-9 -_]");
			List<string> textLines = new();
			if (!File.Exists(Path.Combine(Settings.Instance.DataDirectory, filename)))
			{
				Log($"File not found: {filename}");
				return new string[0];
			}
			using (FileStream fs = new FileStream(Path.Combine(Settings.Instance.DataDirectory, filename), FileMode.Open, FileAccess.Read))
			using (StreamReader sr = new StreamReader(fs))
				while (!sr.EndOfStream)
					textLines.Add(rgx.Replace(sr.ReadLine(), "").Trim());
			return textLines.ToArray();
		}
		
		// CC0 replacements for the original ERROR.TXT entries, used when the DOS text
		// files are absent — which is the normal case now, since asset-free mode ships
		// no *.TXT at all. Returning an empty array there meant every one of these
		// refusals popped an EMPTY message box: the rule fired, the move was denied, and
		// the player was told nothing. The zone-of-control case is the one that bites, as
		// it stops a unit two tiles from a city it is at war with and looks for all the
		// world like a bug in the map.
		private static readonly Dictionary<string, string[]> _fallback = new()
		{
			// The opening address, shown once on the new-game screen and immediately followed
			// by the list of what the tribe already knows — so its LAST line has to run into
			// that list. Without this, asset-free mode dropped the whole address and left the
			// player looking at a fragment: "Alphabet, and Roads."
			//
			// $RPLC1 is the leader name and $US the plural tribe name; NewGame substitutes
			// both. Kept to 30-odd characters a line, which is what fits from x=88.
			["KING/INIT"] = [
				"In 4000 BCE, $RPLC1 rose to lead",
				"the $US: a few hundred people, a",
				"river worth staying beside, and",
				"no memory of anywhere else.",
				"",
				"They came to you already knowing",
			],
			["ERROR/ZOC"] = [
				"You cannot move directly from one",
				"tile beside an enemy unit to",
				"another. Attack it, move to a",
				"tile held by your own unit, or",
				"step back before going around.",
			],
			["ERROR/OCCUPY"] = [
				"That tile is already occupied.",
			],
			// Shown when a non-land unit is sent into an UNDEFENDED enemy city. The only
			// call site (BaseUnit.Confront) used to reach for ERROR/OCCUPY here, which told
			// the player the tile was occupied when the truth is the opposite: the city is
			// empty and a bomber has nothing to attack and cannot take ground.
			["ERROR/NOCAPTURE"] = [
				"Only land units can capture",
				"a city. There is nothing here",
				"to attack.",
			],
			["ERROR/AMPHIB"] = [
				"Units cannot attack from aboard",
				"ship. Put them ashore first.",
			],
			["ERROR/TRIREME"] = [
				"A TRIREME must end its turn",
				"within one tile of land, or risk",
				"being lost at sea.",
			],
			["ERROR/NOIRR"] = [
				"This tile cannot be irrigated.",
				"Irrigation needs fresh water in an",
				"adjacent tile: a river, a lake, or",
				"a tile already irrigated.",
			],
		};

		public string[] GetGameText(string key)
		{
			if (_gameTexts.TryGetValue(key, out string[] text) && text.Length > 0)
				return text;
			if (_fallback.TryGetValue(key, out string[] spare))
				return spare;
			return new string[0];
		}
		
		private static TextFile _instance = null!;
		public static TextFile Instance
		{
			get
			{
				if (_instance is null)
					_instance = new TextFile();
				return _instance;
			}
		}

		public static void ClearInstance()
		{
			_instance = null!;
		}
		
		private TextFile()
		{
			foreach (string file in TEXT_FILES)
			{
				string[] textfile = LoadArray(file);
				List<string> keys = new();
				List<string> lines = new();
				for (int i = 0; i < textfile.Length; i++)
				{
					if (!textfile[i].StartsWith("*")) continue;
					if (textfile[i] == "*END") break;
					keys.Clear();
					lines.Clear();
					while (textfile.Length > i && textfile[i].StartsWith("*"))
						keys.Add(textfile[i++].Substring(1));
					while (textfile.Length > i && textfile[i].Length > 0 && !textfile[i].StartsWith("*"))
						lines.Add(textfile[i++]);
					
					if (lines.Count == 0) continue;
					foreach (string key in keys)
					{
						string ckey = $"{file}/{key}";
						if (!_gameTexts.ContainsKey(ckey))
						{
							_gameTexts.Add(ckey, lines.ToArray());
						}
					}
					if (textfile[i].StartsWith("*")) i--;
				}
			}
		}
	}
}