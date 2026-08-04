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
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.UserInterface;
using CivOne.Units;

namespace CivOne.Screens.GamePlayPanels
{
	// Ten preset zoom levels expressed in basis points. 1000 = 100% (the default,
	// 16 pixels per tile). Stops descend by 100 down to 200, then 125 — chosen by
	// testing in ChrisWi's fork; preserving the same scale keeps muscle memory
	// portable for anyone who plays both forks. NormalizeBasisPoints clamps
	// values loaded from save files into the allowed range.
	public static class MapZoomSettings
	{
		public const int DefaultBasisPoints = 1000;
		public const int MinBasisPoints     = 125;
		public const int MaxBasisPoints     = 1000;
		public static readonly int[] Presets = [DefaultBasisPoints, 900, 800, 700, 600, 500, 400, 300, 200, MinBasisPoints];

		public static int NormalizeBasisPoints(int basisPoints)
		{
			if (basisPoints <= 0) return DefaultBasisPoints;
			return Math.Max(MinBasisPoints, Math.Min(MaxBasisPoints, basisPoints));
		}
	}

	internal class GameMap : BaseScreen
	{
		private IUnit? ActiveUnit => Game.ActiveUnit;

		// Tile pixel size at 100% zoom. All hardcoded "16" multipliers in the
		// rendering path were replaced with _tilePixelSize to support Ctrl+wheel
		// cursor-focused zoom; this constant is the upper bound (100%).
		private const int BaseTilePixelSize = 16;

		private Point _helperDirection = new Point(0, 0);
		private bool _update = true;
		private bool _fullRedraw = false;
		private bool _reframeRequired = false;
		private int _x, _y;
		private IUnit? _lastUnit;
		private ushort _lastTurn;

		private int _tilesX = 15, _tilesY = 12;
		private int _zoomBasisPoints = MapZoomSettings.DefaultBasisPoints;
		private int _tilePixelSize   = BaseTilePixelSize;

		internal int X => _x;
		internal int Y => _y;
		internal int TilesX => _tilesX;
		internal int TilesY => _tilesY;
		internal int TilePixelSize => _tilePixelSize;
		internal int ZoomBasisPoints => _zoomBasisPoints;

		// Live zoom level for the player whose turn it is. CurrentPlayer may be null
		// briefly during load; fall back to default during that window.
		private int CurrentZoomBasisPoints => Game.Started && Game.CurrentPlayer is not null
			? MapZoomSettings.NormalizeBasisPoints(Game.CurrentPlayer.MapZoomBasisPoints)
			: MapZoomSettings.DefaultBasisPoints;

		private static int TilePixelSizeFromBasisPoints(int basisPoints)
			=> Math.Max(1, (BaseTilePixelSize * basisPoints + 500) / 1000);

		// Nearest-neighbour downscale into a destination Bytemap. Used to render the
		// 16-pixel-per-tile bitmap returned by Tiles.ToBitmap() at the active zoom.
		private static Bytemap ScaleBitmap(Bytemap source, int targetWidth, int targetHeight)
		{
			Bytemap output = new Bytemap(Math.Max(1, targetWidth), Math.Max(1, targetHeight));
			if (source is null) return output;
			for (int y = 0; y < targetHeight; y++)
			{
				int sy = (y * source.Height) / targetHeight;
				for (int x = 0; x < targetWidth; x++)
				{
					int sx = (x * source.Width) / targetWidth;
					output[x, y] = source[sx, sy];
				}
			}
			return output;
		}

		private void DrawScaledBitmap(IBitmap source, int left, int top, int width, int height)
		{
			using Bytemap scaled = ScaleBitmap(source.Bitmap, width, height);
			this.AddLayer(scaled, left, top);
		}

		// Bytemap overload — unit sprites are returned as raw Bytemaps (no palette
		// owner), so wrapping them in `new Picture(bytemap, null)` to fit the IBitmap
		// signature crashes Picture's constructor at Picture.cs:83 on palette.Copy().
		// Call this overload directly with the Bytemap.
		private void DrawScaledBitmap(Bytemap source, int left, int top, int width, int height)
		{
			using Bytemap scaled = ScaleBitmap(source, width, height);
			this.AddLayer(scaled, left, top);
		}

		private ITile[,] Tiles => Map[_x, _y, _tilesX, _tilesY];

		private int GetX(ITile tile)
		{
			ITile[,] tiles = Tiles;
			for (int xx = 0; xx < Tiles.GetLength(0); xx++)
			{
				if (tiles[xx, 0].X == tile.X) return xx;
			}
			return -1;
		}

		private int GetY(ITile tile)
		{
			ITile[,] tiles = Tiles;
			for (int yy = 0; yy < Tiles.GetLength(1); yy++)
			{
				if (tiles[0, yy].Y == tile.Y) return yy;
			}
			return -1;
		}

		private IEnumerable<ITile> TileList
		{
			get
			{
				ITile[,] tiles = Tiles;
				for (int yy = 0; yy < tiles.GetLength(1); yy++)
				for (int xx = 0; xx < tiles.GetLength(0); xx++)
				{
					ITile tile = tiles[xx, yy];
					if (!Settings.RevealWorld && !Human.Visible(tile)) continue;
					yield return tile;
				}
			}
		}

		private void DrawHelperArrows(int x, int y)
		{
			if (_helperDirection.X == 0 && _helperDirection.Y == 0) return;
			
			if (_helperDirection.X < 0)
			{
				this.AddLayer(Icons.HelperArrow(Direction.North), x - 16, y - 16)
					.AddLayer(Icons.HelperArrow(Direction.West), x - 16, y)
					.AddLayer(Icons.HelperArrow(Direction.South), x - 16, y + 16);
			}
			if (_helperDirection.X > 0)
			{
				this.AddLayer(Icons.HelperArrow(Direction.North), x + 16, y - 16)
					.AddLayer(Icons.HelperArrow(Direction.East), x + 16, y)
					.AddLayer(Icons.HelperArrow(Direction.South), x + 16, y + 16);
			}
			if (_helperDirection.Y < 0)
			{
				this.AddLayer(Icons.HelperArrow(Direction.West), x - 16, y - 16)
					.AddLayer(Icons.HelperArrow(Direction.North), x, y - 16)
					.AddLayer(Icons.HelperArrow(Direction.East), x + 16, y - 16);
			}
			if (_helperDirection.Y > 0)
			{
				this.AddLayer(Icons.HelperArrow(Direction.West), x - 16, y + 16)
					.AddLayer(Icons.HelperArrow(Direction.South), x, y + 16)
					.AddLayer(Icons.HelperArrow(Direction.East), x + 16, y + 16);
			}
		}
		
		public bool MustUpdate(uint gameTick)
		{
			IUnit? unit = ActiveUnit;

			if ((gameTick % 2) == 0 && (_lastTurn != Game.GameTurn || _lastUnit != unit))
			{
				if (unit is not null && Game.Human == unit.Owner)
				{
					if (!unit.Goto.IsEmpty)
						CenterOnUnit();
					else if (_lastUnit != unit && ShouldCenter())
						CenterOnUnit();
				}
				_fullRedraw = true;
				_update = true;
				_lastUnit = unit;
				_lastTurn = Game.GameTurn;
			}

			// Check if the active unit is on the screen and the blink status has changed.
			if (unit is null)
			{
				_update = true;
				return false;
			}

			if (TileList.Any(t => t is not null && t.X == unit.X && t.Y == unit.Y) && (gameTick % 2) == 0)
			{
				_update = true;
			}
			else if (unit.Moving)
			{
				_update = true;
			}
			else if (unit != _lastUnit && ShouldCenter() && Human == unit.Owner)
			{
				CenterOnUnit();
				_update = true;
				_fullRedraw = true;
			}
			else
			{
				_update = (unit != _lastUnit);
			}
			return _update;
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			// Sync the player's saved zoom into the rendering fields. No-op when the
			// zoom hasn't changed; otherwise sets _fullRedraw so the next compose
			// repaints the entire viewport at the new tile size.
			SyncZoomState();

			if (!(_update || _fullRedraw)) return false;
			if (Game.MovingUnit is null && (gameTick % 2 == 1)) return false;

			Player? renderPlayer = Settings.RevealWorld ? null : Human;
			int px = _tilePixelSize;

			IUnit? activeUnit = ActiveUnit;
			if (Game.MovingUnit is not null)
			{
				IUnit movingUnit = Game.MovingUnit;
				ITile tile = movingUnit.Tile;
				int dx = GetX(tile);
				int dy = GetY(tile);
				if (dx >= 0 && dy >= 0 && dx < _tilesX && dy < _tilesY)
				{
					dx *= px; dy *= px;

					if (_reframeRequired)
					{
						_reframeRequired = false;
						_fullRedraw = false;
						using IBitmap framePic = Tiles.ToBitmap(player: renderPlayer);
						using Bytemap scaledFrame = ScaleBitmap(framePic.Bitmap, _tilesX * px, _tilesY * px);
						this.Clear(5).AddLayer(scaledFrame, 0, 0);
					}

					MoveUnit movement = movingUnit.Movement!;
					using (IBitmap movingArea = Map[movingUnit.X - 1, movingUnit.Y - 1, 3, 3].ToBitmap(player: renderPlayer))
					using (Bytemap scaledMoving = ScaleBitmap(movingArea.Bitmap, 3 * px, 3 * px))
					{
						this.FillRectangle(dx - px, dy - px, 3 * px, 3 * px, 5)
							.AddLayer(scaledMoving, dx - px, dy - px);
					}
					Bytemap unitPicture = movingUnit.ToBitmap();
					// Movement.X / Y are pixel deltas at full tile size; scale them so the
					// animated unit slides at the same fractional rate at any zoom level.
					int mvx = movement.X * px / BaseTilePixelSize;
					int mvy = movement.Y * px / BaseTilePixelSize;
					DrawScaledBitmap(unitPicture, dx + mvx, dy + mvy, px, px);
					if (movingUnit is IBoardable && tile.Units.Any(u => u.Class == UnitClass.Land && (tile.City is null || (tile.City is not null && u.Sentry))))
					{
						DrawScaledBitmap(unitPicture, dx + mvx - 1, dy + mvy - 1, px, px);
					}
					return true;
				}
			}

			if (_fullRedraw)
			{
				_fullRedraw = false;
				using IBitmap fullPic = Tiles.ToBitmap(player: renderPlayer);
				using Bytemap scaledFull = ScaleBitmap(fullPic.Bitmap, _tilesX * px, _tilesY * px);
				this.Clear(5).AddLayer(scaledFull, 0, 0);
			}

			if (activeUnit is not null && Game.CurrentPlayer == Human && !GameTask.Any())
			{
				ITile tile = activeUnit.Tile;
				int dx = GetX(tile);
				int dy = GetY(tile);
				if (dx < _tilesX && dy < _tilesY)
				{
					dx *= px; dy *= px;

					// blink status
					TileSettings setting = ((gameTick / 2) % 3 < 2) ? TileSettings.BlinkOn : TileSettings.BlinkOff;
					DrawScaledBitmap(tile.ToBitmap(setting), dx, dy, px, px);

					DrawHelperArrows(dx, dy);
				}
				return true;
			}

			_update = false;
			return true;
		}

		internal void ForceRefresh()
		{
			_fullRedraw = true;
		}
		
		internal void CenterOnPoint(int x, int y)
		{
			_x = x - (_tilesX / 2);
			_y = y - (_tilesY / 2);
			while (_x < 0) _x += Map.WIDTH;
			while (_x >= Map.WIDTH) _x -= Map.WIDTH;
			while (_y < 0) _y++;
			while (_y + _tilesY > Map.HEIGHT) _y--;
			_update = true;
			_fullRedraw = true;
			_reframeRequired = true;
		}

		private void CenterOnUnit()
		{
			if (Game.ActiveUnit is null) return;
			CenterOnPoint(Game.ActiveUnit.X, Game.ActiveUnit.Y);
		}

		private bool ShouldCenter(int relX = 0, int relY = 0)
		{
			IUnit? unit = Game.ActiveUnit;
			if (unit is null) return false;
			int viewRange = 1;
			if (unit.Class == UnitClass.Water)
			{
				viewRange = (unit as BaseUnitSea)!.Range;
			}
			if (unit.Class == UnitClass.Air)
			{
				viewRange = 2;
			}
			return (!Map.QueryMapPart(_x + viewRange, _y + viewRange, (_tilesX - (viewRange * 2)), (_tilesY - (viewRange * 2))).Any(t => t.X == unit.X + relX && t.Y == unit.Y + relY));
		}

		private bool MoveTo(int relX, int relY)
		{
			_helperDirection = new Point(0, 0);
			
			if (Game.ActiveUnit is null)
				return false;
			
			return Game.ActiveUnit.MoveTo(relX, relY);
		}

		private void TaskStarted(object sender, TaskEventArgs args)
		{
			if (!(sender is GameTask)) return;
			switch (sender)
			{
				case MoveUnit moveUnit:
					IUnit unit = moveUnit.ActiveUnit;
					if (unit is null || (Human != unit.Owner && !Game.EnemyMoves) || (!Settings.RevealWorld && Human != unit.Owner && !Human.Visible(unit.X, unit.Y)))
					{
						args.Abort();
						return;
					}
					if (ShouldCenter(moveUnit.RelX, moveUnit.RelY))
					{
						CenterOnUnit();
					}
					return;
			}
		}

		private bool KeyDownActiveUnit(KeyboardEventArgs args)
		{
			if (Game.ActiveUnit is null || Game.ActiveUnit.Moving)
				return false;
			
			if (args.Key == Key.Space)
			{
				Game.ActiveUnit.SkipTurn();
				return true;
			}
			if (args.Key == Key.Tab)
			{
				Game.UnitWait();
				return true;
			}
			else if (Settings.ArrowHelper)
			{
				switch (args.Key)
				{
					case Key.NumPad1:
					case Key.End:
						return MoveTo(-1, 1);
					case Key.NumPad2:
						return MoveTo(0, 1);
					case Key.NumPad3:
					case Key.PageDown:
						return MoveTo(1, 1);
					case Key.NumPad4:
						return MoveTo(-1, 0);
					case Key.NumPad5:
						GameTask.Enqueue(Show.Empty);
						return true;
					case Key.NumPad6:
						return MoveTo(1, 0);
					case Key.NumPad7:
					case Key.Home:
						return MoveTo(-1, -1);
					case Key.NumPad8:
						return MoveTo(0, -1);
					case Key.NumPad9:
					case Key.PageUp:
						return MoveTo(1, -1);
					case Key.Escape:
						_helperDirection = new Point(0, 0);
						return true;
					case Key.Down:
						_helperDirection.Y++;
						break;
					case Key.Up:
						_helperDirection.Y--;
						break;
					case Key.Left:
						_helperDirection.X--;
						break;
					case Key.Right:
						_helperDirection.X++;
						break;
					default:
						_helperDirection = new Point(0, 0);
						break;
				}

				if (Math.Abs(_helperDirection.X) + Math.Abs(_helperDirection.Y) >= 2)
				{
					int x = 0, y = 0;
					if (_helperDirection.X < 0)
						x = -1;
					else if (_helperDirection.X > 0)
						x = 1;
					
					if (_helperDirection.Y < 0)
						y = -1;
					else if (_helperDirection.Y > 0)
						y = 1;
					
					_helperDirection = new Point(0, 0);
					return MoveTo(x, y);
				}
			}
			else
			{
				switch (args.Key)
				{
					case Key.NumPad1:
					case Key.End:
						return MoveTo(-1, 1);
					case Key.NumPad2:
					case Key.Down:
						return MoveTo(0, 1);
					case Key.NumPad3:
					case Key.PageDown:
						return MoveTo(1, 1);
					case Key.NumPad4:
					case Key.Left:
						return MoveTo(-1, 0);
					case Key.NumPad5:
						GameTask.Enqueue(Show.Empty);
						return true;
					case Key.NumPad6:
					case Key.Right:
						return MoveTo(1, 0);
					case Key.NumPad7:
					case Key.Home:
						return MoveTo(-1, -1);
					case Key.NumPad8:
					case Key.Up:
						return MoveTo(0, -1);
					case Key.NumPad9:
					case Key.PageUp:
						return MoveTo(1, -1);
				}
			}
			
			switch (args.KeyChar)
			{
				case 'B':
					// A Longboat founds by putting its colonists on an adjacent coast, not
					// on its own tile — Orders.FoundCity requires a Settlers and would do
					// nothing at all here, which left an arrived boat with no way to settle.
					if (Game.ActiveUnit is Units.Longboat landing)
						return landing.GoAshore();
					GameTask.Enqueue(Orders.FoundCity(Game.ActiveUnit));
					return true;
				case 'C':
					if (Game.ActiveUnit is null) break;
					CenterOnUnit();
					return true;
				case 'D':
					if (!args.Shift) break;
					Game.DisbandUnit(Game.ActiveUnit);
					return true;
				case 'H':
					// 'h' is also the Settler's "Raise to Hills" terraform shortcut. Inside a
					// city it always means Set-Home-city: a city sitting on Plains satisfies
					// the raise-terrain condition too, so the terraform used to win, zero the
					// settler's moves and pass focus to the next unit — leaving no way to
					// re-home it that turn. Outside a city, prefer the terraform.
					if (Map[Game.ActiveUnit.X, Game.ActiveUnit.Y].City is null
					    && ActivateUnitMenuShortcut("h")) return true;
					Game.ActiveUnit.SetHome();
					return true;
				case 'I':
					GameTask.Enqueue(Orders.BuildIrrigation(Game.ActiveUnit));
					return true;
				case 'M':
					GameTask.Enqueue(Orders.BuildMines(Game.ActiveUnit));
					break;
				case 'P':
					Game.ActiveUnit.Pillage();
					break;
				case 'R':
					GameTask.Enqueue(Orders.BuildRoad(Game.ActiveUnit));
					break;
				case 'S':
					Game.ActiveUnit.Sentry = true;
					break;
				case 'F':
					if (Game.ActiveUnit is Settlers)
					{
						GameTask.Enqueue(Orders.BuildFortress(Game.ActiveUnit));
						break;
					}
					Game.ActiveUnit.Fortify = true;
					break;
				case 'U':
					if (Game.ActiveUnit is IBoardable)
					{
						return (Game.ActiveUnit as BaseUnitSea)!.Unload();;
					}
					break;
				case 'W':
					GameTask.Enqueue(Orders.Wait(Game.ActiveUnit));
					break;
				// Settler terraform / auto actions: dispatch to the active unit's own
				// menu item by its shortcut, so these stay in sync with the unit menu.
				// (An inapplicable action is simply absent from MenuItems, so the key
				// falls through — see ActivateUnitMenuShortcut.)
				case 'A':   // Build Aquafarm
				case 'V':   // Plant Forest
				case 'E':   // Auto-Improve
				case 'N':   // Engineer River
				case 'O':   // Build Road To...
				case 'L':   // Lower to Plains
				case 'J':   // Plant Jungle
				case 'K':   // Thaw to Grassland
				case 'Q':   // Build Canopy Array
					return ActivateUnitMenuShortcut(char.ToLower(args.KeyChar).ToString());
			}

			return false;
		}

		// Find the active unit's menu item whose shortcut matches and trigger it.
		// Reuses the unit menu's own tile/tech conditions (an inapplicable action is
		// simply absent from MenuItems, so the key does nothing).
		private bool ActivateUnitMenuShortcut(string shortcut)
		{
			if (Game.ActiveUnit is null) return false;
			foreach (MenuItem<int> item in Game.ActiveUnit.MenuItems)
			{
				if (item is null || item.Shortcut != shortcut || !item.Enabled) continue;
				item.Select();
				return true;
			}
			return false;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (Game.CurrentPlayer != Human)
			{
				// Ignore all keypresses if the current player is not human
				return false;
			}
			
			switch (args.KeyChar)
			{
				case 'G':
					GameTask.Enqueue(Show.Goto);
					return true;
				case 'T':
					GameTask.Enqueue(Show.Terrain);
					return true;
				case 'W':
				{
					IUnit sleeping = Game.GetUnits().FirstOrDefault(u => Human == u.Owner && (u.Sentry || u.Fortify));
					if (sleeping is not null)
					{
						sleeping.Busy    = false;
						sleeping.MovesLeft = sleeping.Move;
						Game.ActiveUnit  = sleeping;
						return true;
					}
					return false;
				}
			}

			if (Game.ActiveUnit is not null)
			{
				return KeyDownActiveUnit(args);
			}

			switch (args.Key)
			{
				case Key.Space:
				case Key.Enter:
					GameTask.Enqueue(Turn.End());
					return true;
				case Key.Tab:
					Game.UnitWait();
					return true;
			}
			return false;
		}
		
		public override bool MouseDown(ScreenEventArgs args)
		{
			int tilePx = Math.Max(1, _tilePixelSize);
			int x = (int)Math.Floor((float)args.X / tilePx);
			int y = (int)Math.Floor((float)args.Y / tilePx);
			
			int xx = _x + x;
			int yy = _y + y;
			while (xx  < 0) xx += Map.WIDTH;
			while (xx  >= Map.WIDTH) xx -= Map.WIDTH;
			
			City city = Map[_x + x, _y + y].City;
			
			if ((args.Buttons & MouseButton.Right) > 0)
			{
				if (Game.ActiveUnit is not null && (Game.ActiveUnit as BaseUnit)!.MoveTargets.Any(t => t.X == xx && t.Y == yy))
				{
					int relX = xx - Game.ActiveUnit.X;
					int relY = yy - Game.ActiveUnit.Y;
					if (relX < -1) relX = 1;
					if (relY > 1) relY = -1; 

					MoveTo(relX, relY);
					_update = true;
					return true;
				}

				Common.AddScreen(new Civilopedia(Map[_x + x, _y + y]));
				return _update;
			}
			if ((args.Buttons & MouseButton.Left) > 0)
			{
				if (city is not null && (Human == city.Owner || Settings.RevealWorld) && !GameTask.Any())
				{
					Common.AddScreen(new CityManager(city));
				}
				else if (Map[xx, yy].Units.Any(u => Human == u.Owner))
				{
					GameTask.Enqueue(Show.UnitStack(xx, yy));
				}
				else
				{
					_x += x - 8;
					_y += y - 6;
					while (_x < 0) _x += Map.WIDTH;
					while (_x >= Map.WIDTH) _x -= Map.WIDTH;
					while (_y < 0) _y++;
					while (_y + _tilesY > Map.HEIGHT) _y--;
					_update = true;
					_fullRedraw = true;
				}
			}
			return _update;
		}

		public void Resize(int width, int height)
		{
			_tilePixelSize = TilePixelSizeFromBasisPoints(CurrentZoomBasisPoints);
			_zoomBasisPoints = CurrentZoomBasisPoints;
			_tilesX = Math.Min((int)Math.Ceiling((double)width / _tilePixelSize), Map.WIDTH);
			_tilesY = Math.Min((int)Math.Ceiling((double)height / _tilePixelSize), Map.HEIGHT);
			if (_tilesX < 1) _tilesX = 1;
			if (_tilesY < 1) _tilesY = 1;

			Bitmap = new Bytemap(width, height);

			if (_y < 0) _y = 0;
			while (_y + _tilesY > Map.HEIGHT) _y--;
			_update = true;
			_fullRedraw = true;
		}

		// Sync the active zoom from the player's persisted MapZoomBasisPoints into the
		// rendering fields. Called every HasUpdate so a zoom change picks up on the
		// next frame without needing a Resize. Returns true if the zoom level changed,
		// which triggers a full redraw. keepFocus + focusPixel preserve the world tile
		// under the cursor across the zoom step (cursor-focused zoom).
		private bool SyncZoomState(bool keepFocus = false, Point? focusPixel = null)
		{
			int basisPoints = CurrentZoomBasisPoints;
			int tilePx = TilePixelSizeFromBasisPoints(basisPoints);
			if (_zoomBasisPoints == basisPoints && _tilePixelSize == tilePx) return false;

			Point? focusTile = null;
			if (keepFocus && focusPixel.HasValue)
			{
				int safeTilesX = Math.Max(1, _tilesX);
				int safeTilesY = Math.Max(1, _tilesY);
				int safePx     = Math.Max(1, _tilePixelSize);
				int localTileX = Math.Min(safeTilesX - 1, Math.Max(0, focusPixel.Value.X) / safePx);
				int localTileY = Math.Min(safeTilesY - 1, Math.Max(0, focusPixel.Value.Y) / safePx);
				int worldX = _x + localTileX;
				while (worldX < 0) worldX += Map.WIDTH;
				while (worldX >= Map.WIDTH) worldX -= Map.WIDTH;
				int worldY = Math.Max(0, Math.Min(Map.HEIGHT - 1, _y + localTileY));
				focusTile = new Point(worldX, worldY);
			}

			_zoomBasisPoints = basisPoints;
			_tilePixelSize   = tilePx;
			int width  = Bitmap.Width;
			int height = Bitmap.Height;
			_tilesX = Math.Min((int)Math.Ceiling((double)width  / _tilePixelSize), Map.WIDTH);
			_tilesY = Math.Min((int)Math.Ceiling((double)height / _tilePixelSize), Map.HEIGHT);
			if (_tilesX < 1) _tilesX = 1;
			if (_tilesY < 1) _tilesY = 1;
			if (_y < 0) _y = 0;
			while (_y + _tilesY > Map.HEIGHT) _y--;

			if (keepFocus && focusPixel.HasValue && focusTile.HasValue)
			{
				_x = focusTile.Value.X - (focusPixel.Value.X / _tilePixelSize);
				_y = focusTile.Value.Y - (focusPixel.Value.Y / _tilePixelSize);
				while (_x < 0) _x += Map.WIDTH;
				while (_x >= Map.WIDTH) _x -= Map.WIDTH;
				if (_y < 0) _y = 0;
				while (_y + _tilesY > Map.HEIGHT) _y--;
			}

			_fullRedraw = true;
			_update = true;
			return true;
		}

		// Ctrl held → zoom; otherwise pan, so a plain two-finger trackpad swipe scrolls the
		// map instead of being swallowed.
		public override bool MouseWheel(ScreenEventArgs args)
			=> ZoomWheel(args) || PanWheel(args);

		// One tile per wheel notch, in whichever axes the event carries. X wraps with the
		// map, Y clamps at the poles — the same rule the keyboard scroll follows.
		internal bool PanWheel(ScreenEventArgs args)
		{
			int relX = Math.Sign(args.WheelDeltaX);
			int relY = -Math.Sign(args.WheelDelta);
			if (relX == 0 && relY == 0) return false;

			_x += relX;
			while (_x < 0) _x += Map.WIDTH;
			while (_x >= Map.WIDTH) _x -= Map.WIDTH;

			_y += relY;
			if (_y < 0) _y = 0;
			while (_y + _tilesY > Map.HEIGHT) _y--;

			_fullRedraw = true;
			_update = true;
			return true;
		}

		// Ctrl+wheel up = zoom in (smaller index → larger basis points), wheel down
		// = zoom out. The cursor position is captured so SyncZoomState can keep the
		// world tile under the cursor anchored across the zoom step.
		internal bool ZoomWheel(ScreenEventArgs args)
		{
			if ((args.Modifier & KeyModifier.Control) == 0) return false;

			int currentIdx = 0;
			int closestDist = int.MaxValue;
			for (int i = 0; i < MapZoomSettings.Presets.Length; i++)
			{
				int dist = Math.Abs(MapZoomSettings.Presets[i] - CurrentZoomBasisPoints);
				if (dist < closestDist) { closestDist = dist; currentIdx = i; }
			}
			int nextIdx = currentIdx;
			if (args.WheelDelta < 0)      nextIdx = Math.Min(currentIdx + 1, MapZoomSettings.Presets.Length - 1);
			else if (args.WheelDelta > 0) nextIdx = Math.Max(currentIdx - 1, 0);
			int nextBp = MapZoomSettings.Presets[nextIdx];
			if (nextBp == CurrentZoomBasisPoints) return true;

			if (Game.CurrentPlayer is not null) Game.CurrentPlayer.MapZoomBasisPoints = nextBp;
			SyncZoomState(keepFocus: true, focusPixel: args.Location);
			return true;
		}
		
		public GameMap()
		{
			GameTask.Started += TaskStarted;

			_x = 0;
			_y = 0;
			
			Palette = Resources["SP257"].Palette;
		}
	}
}