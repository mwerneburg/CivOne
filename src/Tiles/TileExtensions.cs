// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.Sprites;
using CivOne.IO;
using CivOne.Units;

using static CivOne.Enums.Direction;

namespace CivOne.Tiles
{
	public static class TileExtensions
	{
		private static Game Game => Game.Instance;
		private static Resources Resources => Resources.Instance;
		private static Palette Palette => Resources["SP257"].Palette;
		private static Settings Settings => Settings.Instance;
		
		private static bool GFX256 => (Settings.GraphicsMode == GraphicsMode.Graphics256);

		private static TextSettings CityLabel = TextSettings.ShadowText(15, 5);

		public static bool DrawRoad(this ITile tile)
		{
			if (tile.TransportTube) return false;
			bool hasRail = tile.RailRoad;
			return (tile.Road || hasRail) && (!hasRail || tile.BorderRoads() != tile.BorderRailRoads());
		}
		public static bool DrawRailRoad(this ITile tile) => tile.RailRoad && !tile.TransportTube;
		public static bool DrawTransportTube(this ITile tile) => tile.TransportTube;
		public static bool DrawIrrigation(this ITile tile) => tile.Irrigation && tile.City is null;
		public static bool DrawMine(this ITile tile) => tile.Mine;
		public static bool DrawFortress(this ITile tile) => tile.Fortress && tile.City is null;
		public static bool DrawHut(this ITile tile) => tile.Hut;

		public static int DistanceTo(this ITile tile, int x, int y) => Common.DistanceToTile(tile.X, tile.Y, x, y);
		public static int DistanceTo(this ITile tile, Point point) => Common.DistanceToTile(tile.X, tile.Y, point.X, point.Y);
		public static int DistanceTo(this ITile tile, ITile destinationTile) => Common.DistanceToTile(tile.X, tile.Y, destinationTile.X, destinationTile.Y);
		public static int DistanceTo(this ITile tile, City city) => Common.DistanceToTile(tile.X, tile.Y, city.X, city.Y);

		public static Terrain GetBorderType(this ITile tile, Direction direction)
		{
			ITile? borderTile = GetBorderTile(tile, direction);
			if (borderTile is null) return Terrain.None;
			if (borderTile.Type == Terrain.Grassland2) return Terrain.Grassland1;
			return borderTile.Type;
		}

		public static ITile? GetBorderTile(this ITile tile, Direction direction)
		{
			switch (direction)
			{
				case North: return tile[0, -1];
				case East: return tile[1, 0];
				case South: return tile[0, 1];
				case West: return tile[-1, 0];
				case NorthWest: return tile[-1, -1];
				case NorthEast: return tile[1, -1];
				case SouthWest: return tile[-1, 1];
				case SouthEast: return tile[1, 1];
			}
			return null;
		}
		
		public static IEnumerable<ITile> GetBorderTiles(this ITile tile)
		{
			for (int relY = -1; relY <= 1; relY++)
			for (int relX = -1; relX <= 1; relX++)
			{
				if (relX == 0 && relY == 0) continue;
				if (tile[relX, relY] is null) continue;
				yield return tile[relX, relY];
			}
		}

		public static IEnumerable<ITile> CrossTiles(this ITile tile)
		{
			for (int relY = -1; relY <= 1; relY++)
			for (int relX = -1; relX <= 1; relX++)
			{
				if (relX == 0 && relY == 0) continue;
				if (relX != 0 && relY != 0) continue;
				if (tile[relX, relY] is null) continue;
				yield return tile[relX, relY];
			}
		}

		public static Direction BorderRoads(this ITile tile)
		{
			Direction output = Direction.None;
			for (int i = 1; i <= 128; i *= 2)
			{
				ITile? borderTile = GetBorderTile(tile, (Direction)i);
				if (borderTile is null || (!borderTile.Road && !borderTile.RailRoad && !borderTile.TransportTube && borderTile.City is null)) continue;
				output += i;
			}
			return output;
		}

		public static Direction BorderRailRoads(this ITile tile)
		{
			Direction output = Direction.None;
			for (int i = 1; i <= 128; i *= 2)
			{
				ITile? borderTile = GetBorderTile(tile, (Direction)i);
				if (borderTile is null || (!borderTile.RailRoad && !borderTile.TransportTube && borderTile.City is null)) continue;
				output += i;
			}
			return output;
		}

		public static Direction BorderTransportTubes(this ITile tile)
		{
			Direction output = Direction.None;
			for (int i = 1; i <= 128; i *= 2)
			{
				ITile? borderTile = GetBorderTile(tile, (Direction)i);
				if (borderTile is null || (!borderTile.TransportTube && borderTile.City is null)) continue;
				output += i;
			}
			return output;
		}

		public static Direction DrawRoadDirections(this ITile tile)
		{
			bool hasRail = tile.RailRoad || tile.TransportTube;
			if (hasRail)
				return (Direction)(BorderRoads(tile) - BorderRailRoads(tile));
			if (!tile.Road)
				return Direction.None;
			return BorderRoads(tile);
		}

		public static Direction DrawRailRoadDirections(this ITile tile)
		{
			if (!tile.RailRoad && !tile.TransportTube)
				return Direction.None;
			return BorderRailRoads(tile);
		}

		public static Direction DrawTransportTubeDirections(this ITile tile)
		{
			if (!tile.TransportTube)
				return Direction.None;
			return BorderTransportTubes(tile);
		}

		// ONE statement of "can this tile draw water", shared by every caller.
		//
		// It was stated FIVE times — here, Settlers.BuildIrrigation, Settlers'
		// auto-improve scan, AI.WorkAvailable and the AI's neglected-countryside test — and
		// this file already carries three bug comments about exactly that kind of duplication.
		//
		// The rule itself: irrigation normally spreads from any adjacent irrigated tile, which
		// is how one oasis seeds a line of green marching a hundred tiles across the Gobi.
		// DESERT DOES NOT CHAIN. It needs a real source in the cross — a river, a lake, or
		// freshwater coast — so riverbank and oasis agriculture still work (Egypt and
		// Mesopotamia happened exactly that way) while the daisy-chain stops at the second
		// tile. The deep interior gets the Moisture Farm instead, and a late-game river cut
		// across dry land is worth far more than it used to be.
		public static bool HasIrrigationSource(this ITile tile)
		{
			if (tile is River) return true;   // a river tile is its own source

			bool Natural(ITile x) => x is River || x is Swamp
			                      || (x.IsOcean && Map.Instance.IsFreshwaterAt(x.X, x.Y));
			bool mayChain = tile is not Desert;

			return CrossTiles(tile).Any(x => x.City is null
			                              && (Natural(x) || (mayChain && x.Irrigation)));
		}

		public static bool AllowIrrigation(this ITile tile)
		{
			if (tile.Irrigation) return false;
			if (!(tile is Desert || tile is Grassland || tile is Hills || tile is Plains || tile is River)) return false;
			return tile.HasIrrigationSource();
		}

		public static bool AllowChangeTerrain(this ITile tile)
		{
			return (tile is Forest || tile is Jungle || tile is Swamp || tile is ForestedHills);
		}

		public static IBitmap ToBitmap(this ITile[,] tiles, TileSettings? settings = null, Player? player = null)
		{
			if (settings is null) settings = TileSettings.Default;

			IBitmap output = new Picture(16 * tiles.GetLength(0), 16 * tiles.GetLength(1), Palette);

			for (int yy = 0; yy < tiles.GetLength(1); yy++)
			for (int xx = 0; xx < tiles.GetLength(0); xx++)
			{
				ITile tile = tiles[xx, yy];
				if (tile is null || player is not null && !player.Visible(tile)) continue;

				int x = (xx * 16), y = (yy * 16);
				output.AddLayer(tile.ToBitmap(settings, player), x, y, dispose: true);
			}

			if (settings.CityLabels)
			{
				for (int yy = 0; yy < tiles.GetLength(1) - 1; yy++)
				for (int xx = 0; xx < tiles.GetLength(0); xx++)
				{
					ITile tile = tiles[xx, yy];
					if (tile is null || tile.City is null || player is not null && !player.Visible(tile)) continue;
					int x = (xx == 0) ? 0 : (xx * 16) - 8;
					int y = (yy * 16) + 16;
					string label = tile.City.Name;
					output.DrawText(label, x, y, CityLabel);
				}
			}

			return output;
		}

		public static IBitmap ToBitmap(this ITile tile, TileSettings? settings = null, Player? player = null)
		{
			if (settings is null) settings = TileSettings.Default;

			IBitmap output = new Picture(16, 16, Palette);

			output.AddLayer(MapTile.TileBase(tile));
			output.AddLayer(MapTile.TileLayer(tile));
			// Irrigation goes on top of the terrain texture: the desert texture is fully
			// opaque, so drawing under it (the old order) hid the channels entirely.
			if (GFX256 && settings.Improvements && tile.DrawIrrigation()) output.AddLayer(MapTile.Irrigation);
			output.AddLayer(MapTile.TileSpecial(tile));
			Bytemap? erosion = MapTile.LandCoastErosion(tile);
			if (erosion is not null) output.AddLayer(erosion, dispose: true);
			
			// Add tile improvements
			if (tile.Type != Terrain.River && settings.Improvements)
			{
				if (!GFX256 && tile.DrawIrrigation()) output.AddLayer(MapTile.Irrigation);
				if (tile.DrawMine()) output.AddLayer(MapTile.Mine);
				if (tile.Terrace) output.AddLayer(MapTile.Terrace);
				if (tile.MoistureFarm) output.AddLayer(MapTile.MoistureFarm);
			}
			if (settings.Roads)
			{
				if (tile.DrawRoad()) output.AddLayer(MapTile.Road[tile.DrawRoadDirections()]);
				if (tile.DrawRailRoad()) output.AddLayer(MapTile.RailRoad[tile.DrawRailRoadDirections()]);
				if (tile.DrawTransportTube()) output.AddLayer(MapTile.TransportTube[tile.DrawTransportTubeDirections()]);
			}
			if (tile.DrawFortress()) output.AddLayer(MapTile.Fortress);
			if (tile.DrawHut()) output.AddLayer(MapTile.Hut);
			if (tile.Pollution && !tile.IsOcean) output.AddLayer(MapTile.Pollution);
			// Strategic resource camps render as a fortified mine — the walls say
			// "claimed", the shaft says "working" (ownerless visual; flags change
			// hands via Game.ProcessResourceCamps).
			if (!tile.IsOcean && Game.Instance is not null
			    && Game.Instance.ResourceCamps.ContainsKey((tile.X, tile.Y)))
			{
				output.AddLayer(MapTile.Mine);
				output.AddLayer(MapTile.Fortress);
			}

			if (Game is not null && Game.OlvirImprovements.TryGetValue((tile.X, tile.Y), out var olvirImp))
				output.AddLayer(OlvirSprites.Get(olvirImp));

			if (player is not null)
			{
				Direction fog = Direction.None;
				foreach (Direction direction in (Direction[])[West, North, East, South])
				{
					if (player.Visible(tile, direction)) continue;
					fog += (int)direction;
				}
				if (fog != None) output.AddLayer(MapTile.Fog[fog]);
			}

			if (settings.Cities && tile.City is not null)
			{
				output.AddLayer(Icons.City(tile.City, smallFont: settings.CitySmallFonts));
				if (settings.ActiveUnit && tile.Units.Any(u => u == Game!.ActiveUnit && u.Owner != Game.PlayerNumber(player!)))
				{
					output.AddLayer(tile.UnitsToPicture(), -1, -1, dispose: true);
				}
			}
			
			if ((settings.EnemyUnits || settings.Units) && (tile.City is null || tile.Units.Any(u => u == Game!.ActiveUnit)))
			{
				int unitCount = tile.Units.Count(u => settings.Units || player is null || u.Owner != Game!.PlayerNumber(player));
				if (unitCount > 0)
				{
					output.AddLayer(tile.UnitsToPicture(), dispose: true);
				}
			}

			return output;
		}

		public static IBitmap? UnitsToPicture(this ITile tile)
		{
			if (tile is null || tile.Units.Length == 0 || (tile.Units.Length == 1 && tile.Units[0] == Game.MovingUnit)) return null;
			
			IUnit[] units = tile.Units.OrderBy(x => (tile.IsOcean && x.Class == UnitClass.Water) ? 1 : 0).Where(x => x != Game.MovingUnit).ToArray();
			if (units.Length == 0) return null;

			bool stack = (units.Length > 1);
			IUnit unit = units.First();
			// On water, prefer the boat: a land unit asleep as CARGO should not be drawn over
			// the ship carrying it. The trailing fallback is what stops that becoming a lie —
			// sea tubes let a land unit stand on ocean with no boat under it at all, and
			// without it a tile holding only sentried land units resolved to null and drew
			// NOTHING. An occupied tile rendered as empty sea hid an Olvir settler parked on a
			// trans-Atlantic tube for the rest of a game: caravans routed around a tile that
			// looked empty, and the player's own sleeping caravans vanished where they stood.
			// Sixteen tiles in that one save were invisibly occupied.
			//
			// Whatever else is true, a tile with a unit on it must draw a unit.
			if (tile.IsOcean) unit = units.FirstOrDefault(x => x.Class == UnitClass.Water)
			                      ?? units.FirstOrDefault(x => !(x.Class == UnitClass.Land && x.Sentry))
			                      ?? units.First();
			if (Game.Started && Game.ActiveUnit is not null && !Game.ActiveUnit.Moving && Game.ActiveUnit.X == tile.X && Game.ActiveUnit.Y == tile.Y) unit = Game.ActiveUnit;
			if (unit is null) return null;
			
			IBitmap output = new Picture(16, 16, Palette);
			Bytemap unitPicture = unit.ToBitmap();
			if (tile.City is null) output.AddLayer(unitPicture);
			if (stack || tile.City is not null) output.AddLayer(unitPicture, -1, -1);
			return output;
		}
	}
}