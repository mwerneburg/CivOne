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
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Tiles;

namespace CivOne.Screens
{
	[Expand]
	internal class GameReplay : BaseScreen
	{
		// ── map geometry ─────────────────────────────────────────────────────
		private int TileW => Math.Max(1, Width  / Map.WIDTH);
		private int TileH => Math.Max(1, Height / Map.HEIGHT);
		private int OX    => (Width  - Map.WIDTH  * TileW) / 2;
		private int OY    => (Height - Map.HEIGHT * TileH) / 2;

		// ── replay state ─────────────────────────────────────────────────────
		private readonly int[] _eventTurns;     // sorted unique turns with events
		private int _turnIdx  = -1;             // index into _eventTurns (-1 = not started)
		private bool _paused  = false;
		private bool _done    = false;
		private int  _ticksHeld = 0;
		private const int TICKS_PER_STEP = 20; // ~0.6 s at 30 fps per event-turn

		// city dot state: map position → (ownerIndex, cityName)
		private readonly Dictionary<(int x, int y), (byte owner, string name)> _cities
			= new Dictionary<(int x, int y), (byte owner, string name)>();

		// territory ownership: NO_OWNER = neutral
		private const byte NO_OWNER = 255;
		private readonly byte[,] _territory = new byte[Map.WIDTH, Map.HEIGHT];

		// event log (most-recent last)
		private readonly List<string> _log = new List<string>();
		private const int LOG_LINES = 9;

		// ── terrain backdrop ─────────────────────────────────────────────────
		private readonly Picture _terrain;

		// ── replay data grouped by turn ──────────────────────────────────────
		private readonly ILookup<int, ReplayData> _byTurn;
		private readonly int _finalTurn;

		// ── dirty flag ───────────────────────────────────────────────────────
		private bool _dirty = true;

		// ─────────────────────────────────────────────────────────────────────

		private void BuildTerrain()
		{
			int tw = TileW, th = TileH, ox = OX, oy = OY;
			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				ITile tile = Map[x, y];
				Terrain type = tile.Type;
				if (type == Terrain.Grassland2) type = Terrain.Grassland1;
				bool altTile = ((x + y) % 2 == 1);
				int tx = ((int)type) * 4;
				int ty = altTile ? 4 : 0;
				byte colour = Resources.WorldMapTiles.Bitmap[tx, ty];
				_terrain.FillRectangle(ox + x * tw, oy + y * th, tw, th, colour);
			}
		}

		private void PaintTerritory(int cx, int cy, byte owner)
		{
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				int tx = (cx + dx + Map.WIDTH) % Map.WIDTH;
				int ty = cy + dy;
				if (ty >= 0 && ty < Map.HEIGHT)
					_territory[tx, ty] = owner;
			}
		}

		private void ClearTerritory(int cx, int cy)
		{
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				int tx = (cx + dx + Map.WIDTH) % Map.WIDTH;
				int ty = cy + dy;
				if (ty >= 0 && ty < Map.HEIGHT)
					_territory[tx, ty] = NO_OWNER;
			}
		}

		private void Draw(uint gameTick)
		{
			string yearStr = _turnIdx >= 0 && _turnIdx < _eventTurns.Length
				? Common.YearString((ushort)_eventTurns[_turnIdx])
				: (_done ? Common.YearString((ushort)_finalTurn) : "…");

			// ── terrain ──────────────────────────────────────────────────────
			this.AddLayer(_terrain, 0, 0);

			int tw = TileW, th = TileH, ox = OX, oy = OY;

			// ── territory overlay
			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				if (_territory[x, y] == NO_OWNER) continue;
				int dx = ox + x * tw;
				int dy = oy + y * th;
				byte col = Common.ColourDark[_territory[x, y] % Common.ColourDark.Length];
				this.FillRectangle(dx, dy, tw, th, col);
			}

			// ── city dots ────────────────────────────────────────────────────
			foreach (var kv in _cities)
			{
				int dx = ox + kv.Key.x * tw;
				int dy = oy + kv.Key.y * th;
				byte col = Common.ColourLight[kv.Value.owner % Common.ColourLight.Length];
				this.FillRectangle(dx, dy, tw, th, col);
			}

			// ── right-side log panel ──────────────────────────────────────────
			int panW  = Math.Min(120, Width / 3);
			int panX  = Width - panW;
			int fh    = Resources.GetFontHeight(0);
			int panH  = LOG_LINES * fh + fh + 8;
			int panY  = (Height - panH) / 2;

			this.FillRectangle(panX - 1, panY, panW + 1, panH, CassetteTheme.BG0);
			this.DrawRectangle(panX - 1, panY, panW + 1, panH, CassetteTheme.BORDER);

			// year heading
			this.DrawText(yearStr, 0, CassetteTheme.PHOS_GLOW, panX + panW / 2, panY + 3, TextAlign.Center);

			int maxChars = (panW - 6) / Math.Max(1, fh / 2); // rough char-width estimate
			int ly = panY + fh + 6;
			int start = Math.Max(0, _log.Count - LOG_LINES);
			for (int i = start; i < _log.Count; i++)
			{
				string line = _log[i];
				if (line.Length > maxChars) line = line.Substring(0, maxChars);
				this.DrawText(line, 0, CassetteTheme.INK_MID, panX + 3, ly);
				ly += fh;
			}

			// ── bottom hint ──────────────────────────────────────────────────
			string hint = _done
				? "[ ANY KEY — EXIT ]"
				: (_paused ? "[ SPACE — play   ← → — step   ESC — exit ]"
				           : "[ SPACE — pause  ESC — exit ]");
			this.DrawText(hint, 0, CassetteTheme.INK_LOW, Width / 2, Height - fh - 2, TextAlign.Center);

			// ── progress bar ─────────────────────────────────────────────────
			if (_eventTurns.Length > 1 && !_done)
			{
				int barW  = Width - 4;
				int barX  = 2;
				int barY  = Height - fh - 6;
				int fill  = (_turnIdx + 1) * barW / _eventTurns.Length;
				this.FillRectangle(barX,        barY, barW, 2, CassetteTheme.BG2);
				this.FillRectangle(barX,        barY, fill, 2, CassetteTheme.PHOS);
			}

			_dirty = false;
		}

		private void AdvanceTurn()
		{
			_turnIdx++;
			if (_turnIdx >= _eventTurns.Length)
			{
				_done   = true;
				_paused = true;
				return;
			}

			int turn = _eventTurns[_turnIdx];
			string year = Common.YearString((ushort)turn);

			foreach (ReplayData r in _byTurn[turn])
			{
				switch (r)
				{
					case ReplayData.CityBuilt cb:
					{
						string cname = cb.CityNameId < Game.CityNames.Length ? Game.CityNames[cb.CityNameId] : "?";
						_cities[(cb.X, cb.Y)] = (cb.OwnerId, cname);
						PaintTerritory(cb.X, cb.Y, cb.OwnerId);
						string tribe = PlayerTribeName(cb.OwnerId);
						_log.Add($"{tribe}: {cname} founded");
						break;
					}
					case ReplayData.CityCaptured cc:
					{
						string cname = cc.CityNameId < Game.CityNames.Length ? Game.CityNames[cc.CityNameId] : "?";
						if (_cities.ContainsKey((cc.X, cc.Y)))
							_cities[(cc.X, cc.Y)] = (cc.NewOwnerId, cname);
						PaintTerritory(cc.X, cc.Y, cc.NewOwnerId);
						string tribe = PlayerTribeName(cc.NewOwnerId);
						_log.Add($"{tribe} captures {cname}");
						break;
					}
					case ReplayData.CityDestroyed cd:
					{
						string cname = cd.CityNameId < Game.CityNames.Length ? Game.CityNames[cd.CityNameId] : "?";
						ClearTerritory(cd.X, cd.Y);
						_cities.Remove((cd.X, cd.Y));
						_log.Add($"{cname} destroyed");
						break;
					}
					case ReplayData.WonderBuilt wb:
					{
						string tribe = PlayerTribeName(wb.OwnerId);
						_log.Add($"{tribe}: {wb.WonderName}");
						break;
					}
					case ReplayData.TechDiscovered td:
					{
						string tribe = PlayerTribeName(td.OwnerId);
						_log.Add($"{tribe}: {td.TechName}");
						break;
					}
					case ReplayData.CivilizationDestroyed cvd:
					{
						string dead = CivName(cvd.DestroyedId);
						string killer = CivName(cvd.DestroyedById);
						_log.Add($"{dead} destroyed by {killer}");
						break;
					}
				}
			}
		}

		private string PlayerTribeName(byte ownerId)
		{
			Player p = Game.Players.ElementAtOrDefault(ownerId);
			return p?.TribeName ?? $"Civ{ownerId}";
		}

		private string CivName(int civId)
		{
			Player p = Game.Players.FirstOrDefault(x => x.Civilization.Id == civId);
			return p?.TribeName ?? $"Civ{civId}";
		}

		// ── update / input ────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_done && !_paused)
			{
				if (++_ticksHeld >= TICKS_PER_STEP)
				{
					_ticksHeld = 0;
					AdvanceTurn();
					_dirty = true;
				}
			}

			if (!_dirty) return false;
			Draw(gameTick);
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (_done) { Destroy(); return true; }

			switch (args.Key)
			{
				case Key.Escape:
					Destroy();
					return true;
				case Key.Space:
					_paused = !_paused;
					_dirty  = true;
					return true;
				case Key.Right:
				case Key.NumPad6:
					if (_paused)
					{
						_ticksHeld = 0;
						AdvanceTurn();
						_dirty = true;
					}
					return true;
				case Key.Left:
				case Key.NumPad4:
					// step back: rebuild state from beginning to (_turnIdx - 1)
					if (_paused && _turnIdx > 0)
					{
						int target = _turnIdx - 1;
						_cities.Clear();
						_log.Clear();
						for (int xi = 0; xi < Map.WIDTH; xi++)
						for (int yi = 0; yi < Map.HEIGHT; yi++)
							_territory[xi, yi] = NO_OWNER;
						_turnIdx = -1;
						while (_turnIdx < target)
							AdvanceTurn();
						_dirty = true;
					}
					return true;
			}
			return false;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_done) { Destroy(); return true; }
			_paused = !_paused;
			_dirty  = true;
			return true;
		}

		// ── constructor ───────────────────────────────────────────────────────

		public GameReplay()
		{
			Palette = Resources.WorldMapTiles.Palette.Copy();
			using (Palette cassette = CassetteTheme.CreatePalette())
				Palette.MergePalette(cassette, 1, 17);

			_terrain = new Picture(Width, Height, Palette);
			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
				_territory[x, y] = NO_OWNER;
			BuildTerrain();

			ReplayData[] allData = Game.GetReplayData();
			_byTurn     = allData.ToLookup(r => r.Turn);
			_eventTurns = allData.Select(r => r.Turn).Distinct().OrderBy(t => t).ToArray();
			_finalTurn  = (int)Game.GameTurn;
		}
	}
}
