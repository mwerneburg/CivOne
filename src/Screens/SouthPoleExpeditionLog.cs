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
using System.Linq;
using CivOne.Graphics;

namespace CivOne.Screens
{
	internal class SouthPoleExpeditionLog : TerminalScreen
	{
		private static readonly string[] _defaultLog = new[]
		{
			"EXPEDITION LOG – SOUTH POLE MISSION",
			"CLASSIFIED: EYES ONLY",
			"TRANSMISSION TIMESTAMP: {game year}",
			"",
			"SUBJECT: UNEXPECTED FINDINGS – SOUTH POLE, ANTARCTICA",
			"",
			"DIRECTIVE COMPLIANCE: Primary mission objectives achieved.",
			"Team intact. Coordinates secured.",
			"",
			"DISCOVERY: Anomalous structure located 800 meters SSE of geographic",
			"pole. Non-terrestrial origin confirmed. Structure exhibits properties",
			"inconsistent with known human engineering. No organic or inorganic",
			"life detected. No signs of habitation.",
			"",
			"RECOVERED COMPONENTS:",
			"",
			"1. PRIMARY CORE UNIT",
			"   Composition: Unknown alloy.",
			"   Thermal signature: -196°C (stable).",
			"   Structural integrity: Intact. No corrosion.",
			"   No known terrestrial equivalent.",
			"",
			"2. SECONDARY DRIVE CASING",
			"   Composition: Unknown alloy.",
			"   Surface etching: Geometric patterns (non-Euclidean).",
			"   Magnetic resonance: Anomalous.",
			"   No power source identified.",
			"",
			"ANALYSIS: Components exhibit characteristics consistent with",
			"propulsion system technology. No manuals, schematics, or",
			"instructions recovered. No damage observed. No signs of wear.",
			"",
			"TRANSMISSION ENDS.",
		};

		internal static string ConfigPath => Path.Combine(Settings.Instance.DataDirectory, "south_pole_expedition.txt");

		internal static string[]? LoadLogLines()
		{
			string path = ConfigPath;
			if (!File.Exists(path)) return null;

			var lines = new List<string>();
			bool inSection = false;
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.TrimEnd();
				if (line.StartsWith("[expedition_log]", StringComparison.OrdinalIgnoreCase))
				{
					inSection = true;
					continue;
				}
				if (line.StartsWith("[") && inSection) break;
				if (inSection) lines.Add(line);
			}
			return lines.Count > 0 ? lines.ToArray() : null;
		}

		internal static string[]? LoadIntelLines()
		{
			string path = ConfigPath;
			if (!File.Exists(path)) return null;

			var lines = new List<string>();
			bool inSection = false;
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.TrimEnd();
				if (line.StartsWith("[intel_report]", StringComparison.OrdinalIgnoreCase))
				{
					inSection = true;
					continue;
				}
				if (line.StartsWith("[") && inSection) break;
				if (inSection && line.Length > 0) lines.Add(line);
			}
			return lines.Count > 0 ? lines.ToArray() : null;
		}

		internal static void EnsureConfigFile()
		{
			string path = ConfigPath;
			if (File.Exists(path)) return;

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				using var w = new StreamWriter(path);
				w.WriteLine("# South Pole Expedition – editable text configuration");
				w.WriteLine("# {game year} is replaced with the current game year.");
				w.WriteLine();
				w.WriteLine("[intel_report]");
				w.WriteLine("Satellite analysis reveals an anomalous formation at the South Pole.");
				w.WriteLine("Norwegian scientists have confirmed the structure is of non-terrestrial origin.");
				w.WriteLine("A classified expedition has been dispatched. Further details: EYES ONLY.");
				w.WriteLine();
				w.WriteLine("[expedition_log]");
				foreach (string line in _defaultLog)
					w.WriteLine(line);
			}
			catch { /* non-fatal */ }
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                          return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("CLASSIFIED"))                           return CassetteTheme.ALERT;
			if (text.StartsWith("TRANSMISSION TIMESTAMP"))               return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("RECOVERED") || text.StartsWith("ANALYSIS") ||
			    text.StartsWith("DISCOVERY") || text.StartsWith("DIRECTIVE") ||
			    text.StartsWith("SUBJECT")   || text.StartsWith("TRANSMISSION ENDS")) return CassetteTheme.INK_HIGH;
			if (text.StartsWith("1.") || text.StartsWith("2."))          return CassetteTheme.PHOS;
			return CassetteTheme.INK_MID;
		}

		public SouthPoleExpeditionLog(string gameYear)
		{
			string[] raw = LoadLogLines() ?? _defaultLog;
			_lines = raw.Select(l => l.Replace("{game year}", gameYear)).ToArray();
			InitTypewriter();
		}
	}
}
