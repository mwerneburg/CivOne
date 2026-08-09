// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

// MapPreview — lightweight world-generation review screen, invoked by
// `civone-sdl --mapgen-preview`. Reads a key=value config file from the data
// directory, calls Map.Generate, renders a thumbnail of the result with a stats
// overlay, and lets the user re-roll or reload the config without restarting.
//
// Intentionally bypasses splash/setup/credits/main-menu/customize — the whole
// point is to iterate on Map.Generate.cs and tile art without clicking through
// the game shell.
//
// Hotkeys:
//   R       re-generate (re-roll) using the current in-memory config
//   C       reload config file from disk and re-generate
//   Esc/Q   quit the application

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;
using CivOne.Tiles;

namespace CivOne.Screens
{
	[Expand]
	internal class MapPreview : BaseScreen
	{
		// Plain text key=value, # for comments. Lives at:
		//   ~/Library/Application Support/CivOne/maptest.conf
		// All keys optional; defaults below apply when missing or invalid.
		private static string ConfigPath =>
			Path.Combine(Settings.Instance.DataDirectory, "maptest.conf");

		private class PreviewConfig
		{
			public int Width            = 80;
			public int Height           = 50;
			public int LandMass         = 1;
			public int Temperature      = 1;
			public int Climate          = 1;
			public int Age              = 1;
			public int NumSeeds         = 0;  // 0 = use formula
			public int SeedSeparation   = 0;
			public int RiverTarget      = 0;
			public int RiverSeparation  = 0;
			public int RiverMinLength   = 0;
		}

		private PreviewConfig _config = new();
		private bool _hasUpdate = true;
		private bool _everRendered = false;
		private string _configStatus = "";

		public MapPreview()
		{
			// Same palette setup as CustomizeWorld (line 198-204): default palette with
			// the Cassette theme merged into indices 1-17 so DrawText / FillRectangle
			// references resolve. Without this, RuntimeHandler.OnDraw NREs on
			// TopScreen.Palette.Copy().
			using Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			LoadConfig();
			StartGenerate();
		}

		private void LoadConfig()
		{
			_config = new PreviewConfig();
			if (!File.Exists(ConfigPath))
			{
				WriteDefaultConfig();
				_configStatus = $"Wrote defaults to {ConfigPath}";
				return;
			}
			try
			{
				foreach (string raw in File.ReadAllLines(ConfigPath))
				{
					string line = raw.Trim();
					if (line.Length == 0 || line[0] == '#') continue;
					int eq = line.IndexOf('=');
					if (eq <= 0) continue;
					string key = line.Substring(0, eq).Trim().ToLowerInvariant();
					string val = line.Substring(eq + 1).Trim();
					int hash = val.IndexOf('#');
					if (hash >= 0) val = val.Substring(0, hash).Trim();
					if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) continue;
					switch (key)
					{
						case "width":            _config.Width = n; break;
						case "height":           _config.Height = n; break;
						case "land_mass":        _config.LandMass = n; break;
						case "temperature":      _config.Temperature = n; break;
						case "climate":          _config.Climate = n; break;
						case "age":              _config.Age = n; break;
						case "num_seeds":        _config.NumSeeds = n; break;
						case "seed_separation":  _config.SeedSeparation = n; break;
						case "river_target":     _config.RiverTarget = n; break;
						case "river_separation": _config.RiverSeparation = n; break;
						case "river_min_length": _config.RiverMinLength = n; break;
					}
				}
				_configStatus = $"Loaded {ConfigPath}";
			}
			catch (Exception e)
			{
				_configStatus = $"Config error: {e.Message}";
			}
		}

		private void WriteDefaultConfig()
		{
			try
			{
				Directory.CreateDirectory(Settings.Instance.DataDirectory);
				File.WriteAllText(ConfigPath, @"# CivOne map-preview config
# Edit and press C in the preview window to reload.
# Use 0 on an override knob to fall back to the engine's default formula.

# Map dimensions
width=80
height=50

# Standard generator settings (0=low, 1=normal, 2=high)
land_mass=1
temperature=1
climate=1
age=1

# Lower-level Pangaea knobs
num_seeds=0
seed_separation=0       # 0 = engine default (2); raise to 4-6 to fragment further

# Lower-level river knobs
river_target=0          # 0 = engine default formula
river_separation=0      # 0 = engine default (4)
river_min_length=0      # 0 = engine default (3)
");
			}
			catch { /* best effort */ }
		}

		private void StartGenerate()
		{
			// Drop the singleton so Map.Generate runs from scratch each time.
			Map.ResetForPreview();
			Map.Instance.PreviewNumSeeds        = Math.Max(0, _config.NumSeeds);
			Map.Instance.PreviewSeedSeparation  = Math.Max(0, _config.SeedSeparation);
			Map.Instance.PreviewRiverTarget     = Math.Max(0, _config.RiverTarget);
			Map.Instance.PreviewRiverSeparation = Math.Max(0, _config.RiverSeparation);
			Map.Instance.PreviewRiverMinLength  = Math.Max(0, _config.RiverMinLength);

			int w = Math.Max(20, _config.Width);
			int h = Math.Max(15, _config.Height);
			Map.Instance.Generate(
				landMass:    Clamp01_2(_config.LandMass),
				temperature: Clamp01_2(_config.Temperature),
				climate:     Clamp01_2(_config.Climate),
				age:         Clamp01_2(_config.Age),
				width:  w, height: h);
			_everRendered = false;
			_hasUpdate = true;
		}

		private static int Clamp01_2(int v) => Math.Max(0, Math.Min(2, v));

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (args.Key == Key.Escape)
			{
				Environment.Exit(0);
				return true;
			}
			char c = char.ToUpperInvariant(args.KeyChar);
			switch (c)
			{
				case 'Q':
					Environment.Exit(0);
					return true;
				case 'R':
					StartGenerate();
					return true;
				case 'C':
					LoadConfig();
					StartGenerate();
					return true;
				case 'E':
					LoadEarth();
					return true;
			}
			return false;
		}

		private void LoadEarth()
		{
			Map.ResetForPreview();
			// Map.EarthEpicPath, not the data directory alone: this looked only where
			// build_earth_map.py writes by DEFAULT, so the 'E' preview and the new-game menu
			// (which goes through the resolver) could load two different worlds — and with no
			// user copy present, the preview reported the shipped map as missing.
			string path = Map.EarthEpicPath;
			if (Map.Instance.LoadEarthBin(path))
			{
				_configStatus = $"Loaded {Path.GetFileName(path)}";
			}
			else
			{
				_configStatus = $"Earth file missing: {path}";
				// Re-roll generator so the screen still has something to draw.
				StartGenerate();
				return;
			}
			_everRendered = false;
			_hasUpdate = true;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!Map.Instance.Ready)
			{
				if (!_everRendered)
				{
					this.Clear(CassetteTheme.BG0)
					    .DrawText("Generating map...", 0, CassetteTheme.PHOS, this.Width() / 2, this.Height() / 2, TextAlign.Center);
					return true;
				}
				return false;
			}

			if (!_hasUpdate) return false;
			_hasUpdate = false;
			_everRendered = true;
			DrawMap();
			return true;
		}

		// Terrain → palette index. Uses CassetteTheme indices that are stable across the
		// codebase; not the SP257 tile palette (which would require sampling sprites).
		// Resulting image is a coloured thumbnail — enough to see continents, rivers, huts
		// at a glance without depending on tile-art changes.
		private void DrawMap()
		{
			this.Clear(CassetteTheme.BG0);

			int mapW = Map.WIDTH, mapH = Map.HEIGHT;
			int canvasW = this.Width(), canvasH = this.Height();

			// Reserve a strip on the right for the stats overlay.
			int statsW = 120;
			int viewW = Math.Max(1, canvasW - statsW);
			int viewH = canvasH;

			// Largest integer pixels-per-tile that fits.
			int pxW = viewW / mapW;
			int pxH = viewH / mapH;
			int px = Math.Max(1, Math.Min(pxW, pxH));
			int drawW = px * mapW;
			int drawH = px * mapH;
			int ox = (viewW - drawW) / 2;
			int oy = (viewH - drawH) / 2;

			for (int y = 0; y < mapH; y++)
			for (int x = 0; x < mapW; x++)
			{
				ITile t = Map.Instance[x, y];
				byte c = MiniMap.TerrainColour(t);
				this.FillRectangle(ox + x * px, oy + y * px, px, px, c);
			}

			DrawStats(canvasW - statsW + 4, 4, mapW, mapH);
		}

		private void DrawStats(int sx, int sy, int mapW, int mapH)
		{
			var counts = new Dictionary<Terrain, int>();
			int land = 0, ocean = 0, river = 0, hut = 0;
			for (int y = 0; y < mapH; y++)
			for (int x = 0; x < mapW; x++)
			{
				ITile t = Map.Instance[x, y];
				if (t is null) continue;
				if (t.Hut) hut++;
				if (t.IsOcean) ocean++; else land++;
				if (t.Type == Terrain.River) river++;
				counts.TryGetValue(t.Type, out int v);
				counts[t.Type] = v + 1;
			}

			// Continent count + largest share. Use ContinentId which CalculateContinentSize
			// has already populated by the time HasUpdate runs.
			var conSize = new Dictionary<byte, int>();
			for (int y = 0; y < mapH; y++)
			for (int x = 0; x < mapW; x++)
			{
				ITile t = Map.Instance[x, y];
				if (t is null || t.IsOcean) continue;
				byte cid = t.ContinentId;
				if (cid == 0) continue;
				conSize.TryGetValue(cid, out int v);
				conSize[cid] = v + 1;
			}
			int continents = conSize.Count;
			int largest = 0;
			foreach (var v in conSize.Values) if (v > largest) largest = v;
			double largestPct = land > 0 ? (100.0 * largest / land) : 0.0;
			double riverPct   = land > 0 ? (100.0 * river / land)   : 0.0;

			int y0 = sy;
			void Line(string s, byte colour)
			{
				this.DrawText(s, 0, colour, sx, y0);
				y0 += 9;
			}

			Line("─── MAP PREVIEW ───", CassetteTheme.PHOS_GLOW);
			Line($"{mapW}x{mapH}", CassetteTheme.INK_MID);
			Line($"LM={_config.LandMass} TMP={_config.Temperature}", CassetteTheme.INK_MID);
			Line($"CLM={_config.Climate} AGE={_config.Age}", CassetteTheme.INK_MID);
			y0 += 4;
			Line($"Land: {land}", CassetteTheme.INK_HIGH);
			Line($"Ocean: {ocean}", CassetteTheme.INK_HIGH);
			Line($"Continents: {continents}", CassetteTheme.INK_HIGH);
			byte pangaeaColour = largestPct >= 80 ? CassetteTheme.ALERT
			                   : largestPct >= 60 ? CassetteTheme.PHOS_GLOW
			                   : CassetteTheme.OK;
			Line($"Largest: {largestPct:F0}%", pangaeaColour);
			Line($"Rivers: {river} ({riverPct:F1}%)", CassetteTheme.CYAN);
			Line($"Huts: {hut}", CassetteTheme.ALERT);
			y0 += 4;
			Line("─ TERRAIN ─", CassetteTheme.PHOS_GLOW);
			Action<Terrain, string> show = (terr, lbl) =>
			{
				counts.TryGetValue(terr, out int v);
				if (v > 0) Line($"{lbl}: {v}", CassetteTheme.INK_MID);
			};
			show(Terrain.Forest,     "Forest");
			show(Terrain.Jungle,     "Jungle");
			show(Terrain.Grassland1, "Grass1");
			show(Terrain.Grassland2, "Grass2");
			show(Terrain.Plains,     "Plains");
			show(Terrain.Desert,     "Desert");
			show(Terrain.Hills,      "Hills");
			show(Terrain.ForestedHills, "WdHills");
			show(Terrain.Mountains,  "Mtns");
			show(Terrain.Swamp,      "Swamp");
			show(Terrain.Tundra,     "Tundra");
			show(Terrain.Arctic,     "Arctic");
			y0 += 4;
			Line("R re-roll", CassetteTheme.INK_MID);
			Line("C reload+roll", CassetteTheme.INK_MID);
			Line("E load Earth", CassetteTheme.INK_MID);
			Line("Esc/Q quit", CassetteTheme.INK_MID);
			if (_configStatus.Length > 0)
			{
				y0 += 4;
				Line(_configStatus, CassetteTheme.INK_LOW);
			}
		}
	}
}
