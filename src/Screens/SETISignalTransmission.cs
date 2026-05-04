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
	internal class SETISignalTransmission : BaseScreen
	{
		private const int FONT_ID = 0;
		private const int PAD     = 10;

		private readonly string[] _lines;
		private int _scrollY;
		private bool _dirty = true;

		// ── config loading ────────────────────────────────────────────────────

		private static readonly string[] _defaultTransmission = new[]
		{
			"CLASSIFIED: EYES ONLY – WAYLAND-YUTANI CORPORATE ARCHIVE",
			"TRANSMISSION TIMESTAMP: 04 MAY {game date} / 14:30 UTC",
			"STATUS: PRIORITY THETA",
			"",
			"SUBJECT: SETI SIGNAL ANALYSIS – TAU CETI SYSTEM",
			"",
			"DIRECTIVE COMPLIANCE: SETI deep-space array monitoring initiated per",
			"Protocol 9-X. Signal detected 04 MAY {game date} / 09:44 UTC",
			"",
			"FINDINGS: Artificial origin confirmed.",
			"Signal source: Tau Ceti (GJ 71, HD 10700).",
			"Frequency: 1420.40575177 MHz (neutral hydrogen line).",
			"Bandwidth: 1.2 kHz.",
			"Modulation: Pulse-train with embedded data stream.",
			"",
			"DATA ANALYSIS:",
			"",
			"* Complexity: 98.7% non-random. Pattern suggests structured information.",
			"* Encryption: Present. Attempts to decode failed.",
			"  No known terrestrial cipher matched.",
			"* Content: Uninterpretable. No repeating sequences, headers,",
			"  or recognizable symbols.",
			"* Repeat Interval: 18.3 days (consistent). No degradation observed.",
			"",
			"RECOMMENDATIONS:",
			"",
			"* Containment: Signal isolated. No further transmission attempts authorized.",
			"* Investigation: Request dispatch of unmanned spacecraft for on-site analysis.",
			"* Contingency: Establish colony on Alpha Centauri II per Directive 7.",
			"",
			"TRANSMISSION ENDS.",
		};

		internal static string ConfigPath => Path.Combine(Settings.Instance.DataDirectory, "seti_signal.txt");

		internal static string[] LoadTransmissionLines()
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

		// ── drawing ───────────────────────────────────────────────────────────

		private void Redraw()
		{
			int fh    = Resources.GetFontHeight(FONT_ID);
			int bodyH = Height - PAD * 2;
			int maxScroll = Math.Max(0, _lines.Length * fh - bodyH);
			_scrollY = Math.Max(0, Math.Min(_scrollY, maxScroll));

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawRectangle(2, 2, Width - 4, Height - 4, CassetteTheme.BORDER);

			int y = PAD - _scrollY;
			for (int i = 0; i < _lines.Length; i++)
			{
				if (y + fh < PAD || y >= Height - PAD) { y += fh; continue; }

				string text = _lines[i];
				byte color;

				if (i == 0)
					color = CassetteTheme.ALERT;            // CLASSIFIED header
				else if (text.StartsWith("TRANSMISSION TIMESTAMP") || text.StartsWith("STATUS"))
					color = CassetteTheme.PHOS_DIM;
				else if (text.StartsWith("SUBJECT"))
					color = CassetteTheme.PHOS_GLOW;
				else if (text.StartsWith("DIRECTIVE") || text.StartsWith("FINDINGS") ||
				         text.StartsWith("DATA ANALYSIS") || text.StartsWith("RECOMMENDATIONS") ||
				         text.StartsWith("TRANSMISSION ENDS"))
					color = CassetteTheme.INK_HIGH;
				else if (text.StartsWith("*"))
					color = CassetteTheme.PHOS;
				else
					color = CassetteTheme.INK_MID;

				this.DrawText(text, FONT_ID, color, PAD + 4, y);
				y += fh;
			}

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

		public SETISignalTransmission(string gameDate)
		{
			string[] raw = LoadTransmissionLines() ?? _defaultTransmission;
			_lines = raw.Select(l => l.Replace("{game date}", gameDate)).ToArray();

			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
