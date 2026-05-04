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
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens
{
	[Expand, Modal]
	internal class SouthPoleExpeditionLog : BaseScreen
	{
		private const int FONT_ID  = 0;
		private const int PAD      = 10;

		private readonly string[] _lines;
		private int _scrollY;
		private bool _dirty = true;

		// ── config loading ────────────────────────────────────────────────────

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

		internal static string[] LoadLogLines()
		{
			string path = ConfigPath;
			if (!File.Exists(path))
				return null; // caller uses defaults

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
				if (line.StartsWith("[") && inSection)
					break;
				if (inSection)
					lines.Add(line);
			}
			return lines.Count > 0 ? lines.ToArray() : null;
		}

		internal static string[] LoadIntelLines()
		{
			string path = ConfigPath;
			if (!File.Exists(path))
				return null;

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
				if (line.StartsWith("[") && inSection)
					break;
				if (inSection && line.Length > 0)
					lines.Add(line);
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

		// ── drawing ───────────────────────────────────────────────────────────

		private void Redraw()
		{
			int fh   = Resources.GetFontHeight(FONT_ID);
			int bodyH = Height - PAD * 2;
			int maxScroll = Math.Max(0, _lines.Length * fh - bodyH);
			_scrollY = Math.Max(0, Math.Min(_scrollY, maxScroll));

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);

			// Outer border
			this.DrawRectangle(2, 2, Width - 4, Height - 4, CassetteTheme.BORDER);

			int y = PAD - _scrollY;
			for (int i = 0; i < _lines.Length; i++)
			{
				if (y + fh < PAD || y >= Height - PAD) { y += fh; continue; }

				string text = _lines[i];
				byte color;

				if (i == 0)
					color = CassetteTheme.PHOS_GLOW;
				else if (text.StartsWith("CLASSIFIED"))
					color = CassetteTheme.ALERT;
				else if (text.StartsWith("TRANSMISSION TIMESTAMP"))
					color = CassetteTheme.PHOS_DIM;
				else if (text.StartsWith("RECOVERED") || text.StartsWith("ANALYSIS") ||
				         text.StartsWith("DISCOVERY") || text.StartsWith("DIRECTIVE") ||
				         text.StartsWith("SUBJECT") || text.StartsWith("TRANSMISSION ENDS"))
					color = CassetteTheme.INK_HIGH;
				else if (text.StartsWith("1.") || text.StartsWith("2."))
					color = CassetteTheme.PHOS;
				else
					color = CassetteTheme.INK_MID;

				this.DrawText(text, FONT_ID, color, PAD + 4, y);
				y += fh;
			}

			// Scroll hint
			if (maxScroll > 0)
			{
				int pct = (int)(100.0 * _scrollY / maxScroll);
				string hint = $"[ ↑↓ TO SCROLL  {pct}%  ANY KEY DISMISSES ]";
				this.DrawText(hint, FONT_ID, CassetteTheme.INK_LOW,
				              Width / 2, Height - PAD + 1, TextAlign.Center);
			}
			else
			{
				this.DrawText("[ ANY KEY OR CLICK TO DISMISS ]", FONT_ID, CassetteTheme.INK_LOW,
				              Width / 2, Height - PAD + 1, TextAlign.Center);
			}

			_dirty = false;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_dirty) return false;
			Redraw();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			int fh = Resources.GetFontHeight(FONT_ID);
			int bodyH = Height - PAD * 2;
			int maxScroll = Math.Max(0, _lines.Length * fh - bodyH);

			if (maxScroll > 0 && (args.Key == Key.Up || args.Key == Key.NumPad8))
			{
				_scrollY = Math.Max(0, _scrollY - fh * 3);
				_dirty = true;
				return true;
			}
			if (maxScroll > 0 && (args.Key == Key.Down || args.Key == Key.NumPad2))
			{
				_scrollY = Math.Min(maxScroll, _scrollY + fh * 3);
				_dirty = true;
				return true;
			}

			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		// ── constructor ───────────────────────────────────────────────────────

		public SouthPoleExpeditionLog(string gameYear)
		{
			string[] raw = LoadLogLines() ?? _defaultLog;
			_lines = raw.Select(l => l.Replace("{game year}", gameYear)).ToArray();

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
