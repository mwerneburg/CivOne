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
	internal class SETISignalTransmission : TerminalScreen
	{
		private static readonly string[] _defaultTransmission = new[]
		{
			"CLASSIFIED: EYES ONLY",
			"TRANSMISSION TIMESTAMP: 04 MAY {game date} / 14:30 UTC",
			"STATUS: PRIORITY THETA",
			"",
			"SUBJECT: SETI SIGNAL ANALYSIS – TAU CETI SYSTEM",
			"",
			"FINDINGS: Artificial origin confirmed.",
			"Signal source: Tau Ceti (GJ 71, HD 10700).",
			"Frequency: 1420.40575177 MHz (neutral hydrogen line).",
			"Bandwidth: 1.2 kHz.",
			"Modulation: Pulse-train with embedded data stream.",
			"Repeat interval: 18.3 days. No degradation observed.",
			"",
			"DATA ANALYSIS — TEAM A:",
			"",
			"* Complexity: 98.7% non-random. Pattern suggests structured information.",
			"* Recursive sub-pattern detected at three independent scale levels.",
			"  Inconsistent with biological signal design.",
			"* INTERPRETATION: Machine-generated. Possibly autonomous.",
			"",
			"DATA ANALYSIS — TEAM B:",
			"",
			"* Signal header structure resembles an inventory or manifest format.",
			"  Recurring cross-index blocks remain undecoded.",
			"* Repeat interval is not orbital. Interval is behavioral.",
			"* INTERPRETATION: Directed communication. Source knows we are here.",
			"",
			"DATA ANALYSIS — TEAM C:",
			"",
			"* Power profile consistent with distributed array, not a single source.",
			"* Signal attenuation suggests source is in motion.",
			"* INTERPRETATION: Origin is a vessel or fleet, not a planetary body.",
			"",
			"ASSESSMENTS CONFLICT. CONSENSUS NOT REACHED.",
			"",
			"RECOMMENDATIONS:",
			"",
			"* Containment: Signal isolated. No reply authorized at this time.",
			"* Investigation: Dispatch unmanned probe to Tau Ceti for direct analysis.",
			"* Contingency A: Establish colony at Alpha Centauri II per Directive 7.",
			"* Contingency B: Commission study of planetary defense options.",
			"",
			"NOTE: Some researchers argue an absence of response is itself a message.",
			"",
			"TRANSMISSION ENDS.",
		};

		internal static string ConfigPath => Path.Combine(Settings.Instance.DataDirectory, "seti_signal.txt");

		internal static string[]? LoadTransmissionLines()
		{
			string path = ConfigPath;
			if (!File.Exists(path)) return null;

			var lines = new List<string>();
			bool inSection = false;
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.TrimEnd();
				if (line.StartsWith("[seti_signal]", StringComparison.OrdinalIgnoreCase))
				{
					inSection = true;
					continue;
				}
				if (line.StartsWith("[") && inSection) break;
				if (inSection) lines.Add(line);
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
				w.WriteLine("# SETI Signal Transmission – editable text configuration");
				w.WriteLine("# {game date} is replaced with the current game year.");
				w.WriteLine();
				w.WriteLine("[seti_signal]");
				foreach (string line in _defaultTransmission)
					w.WriteLine(line);
			}
			catch { /* non-fatal */ }
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                    return CassetteTheme.ALERT;
			if (text.StartsWith("TRANSMISSION TIMESTAMP") ||
			    text.StartsWith("STATUS"))                         return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("SUBJECT"))                        return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("FINDINGS") ||
			    text.StartsWith("DATA ANALYSIS") ||
			    text.StartsWith("RECOMMENDATIONS") ||
			    text.StartsWith("TRANSMISSION ENDS"))              return CassetteTheme.INK_HIGH;
			if (text.StartsWith("ASSESSMENTS CONFLICT"))           return CassetteTheme.ALERT;
			if (text.StartsWith("* INTERPRETATION:"))              return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("*"))                              return CassetteTheme.PHOS;
			if (text.StartsWith("NOTE:"))                          return CassetteTheme.ALERT;
			return CassetteTheme.INK_MID;
		}

		public SETISignalTransmission(string gameDate)
		{
			string[] raw = LoadTransmissionLines() ?? _defaultTransmission;
			_lines = raw.Select(l => l.Replace("{game date}", gameDate)).ToArray();
			InitTypewriter();
		}
	}
}
