// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using CivOne.Enums;
using CivOne.IO;
using CivOne.Tiles;

using static CivOne.Enums.Direction;

namespace CivOne.Graphics.Sprites
{
	public static class MapTile
	{
		private static Direction[] Cross => [North, East, South, West];
		private static Free Free => Free.Instance;
		private static Resources Resources => Resources.Instance;
		private static Settings Settings => Settings.Instance;

		private static bool GFX256 => (Settings.GraphicsMode == GraphicsMode.Graphics256);

		private static Bytemap GetLandBase() => Free.LandBase;

		// Always uses the free ocean tile. The original resource lookup was
		// Resources["TER257"/"SPRITES"].Bitmap[0, 160, 16, 16].
		private static Bytemap GetOceanBase() => Free.OceanBase;

		private static Bytemap GetLakeBase() => Free.LakeTile();

		private static Bytemap? GetLakeShoreLayer(Direction directions)
		{
			if (directions == None) return null;
			return Free.LakeShoreLayer(directions);
		}

		private static bool DrawCoastCorners(ref Bytemap output, Direction land)
		{
			if (!Resources.Exists("SP299")) return false;

			Bytemap pic = Resources["SP299"].Bitmap;

			if (land.And(South | East) && land.Not(North | West | SouthWest | NorthEast)) output.AddLayer(pic[224, 100, 16, 16]);
			else if (land.And(North | West) && land.Not(South | East | NorthEast | SouthWest)) output.AddLayer(pic[240, 100, 16, 16]);
			else if (land.And(North | East) && land.Not(South | West | NorthWest | SouthEast)) output.AddLayer(pic[256, 100, 16, 16]);
			else if (land.And(South | West) && land.Not(North | East | SouthEast | NorthWest)) output.AddLayer(pic[272, 100, 16, 16]);
			else return false;
			return true;
		}

		private static void DrawCoastSegments(ref Bytemap output, Direction land)
		{
			if (!Resources.Exists("TER257")) return;

			Bytemap pic = Resources["TER257"].Bitmap;
			
			if (land.And(North))
			{
				int xw = land.And(West) ? 80 : land.And(NorthWest) ? 96 : 64;
				int xe = land.And(East) ? 88 : land.And(NorthEast) ? 56 : 24;
				
				output.AddLayer(pic[xw, 176, 8, 8], 0, 0);
				output.AddLayer(pic[xe, 176, 8, 8], 8, 0);
			}
			if (land.And(East))
			{
				int xn = land.And(North) ? 88 : land.And(NorthEast) ? 104 : 72;
				int xs = land.And(South) ? 88 : land.And(SouthEast) ? 56 : 24;
				
				output.AddLayer(pic[xn, 176, 8, 8], 8, 0);
				output.AddLayer(pic[xs, 184, 8, 8], 8, 8);
			}
			if (land.And(South))
			{
				int xw = land.And(West) ? 80 : land.And(SouthWest) ? 48 : 16;
				int xe = land.And(East) ? 88 : land.And(SouthEast) ? 104 : 72;

				output.AddLayer(pic[xw, 184, 8, 8], 0, 8);
				output.AddLayer(pic[xe, 184, 8, 8], 8, 8);
			}
			if (land.And(West))
			{
				int xn = land.And(North) ? 80 : land.And(NorthWest) ? 48 : 16;
				int xs = land.And(South) ? 80 : land.And(SouthWest) ? 96 : 64;
				
				output.AddLayer(pic[xn, 176, 8, 8], 0, 0);
				output.AddLayer(pic[xs, 184, 8, 8], 0, 8);
			}
		}

		private static void DrawCoastDiagonal(ref Bytemap output, Direction land)
		{
			if (!Resources.Exists("TER257")) return;

			Bytemap pic = Resources["TER257"].Bitmap;

			if (land.And(NorthWest) && land.Not(North | West)) output.AddLayer(pic[32, 176, 8, 8], 0, 0);
			if (land.And(NorthEast) && land.Not(North | East)) output.AddLayer(pic[40, 176, 8, 8], 8, 0);
			if (land.And(SouthWest) && land.Not(South | West)) output.AddLayer(pic[32, 184, 8, 8], 0, 8);
			if (land.And(SouthEast) && land.Not(South | East)) output.AddLayer(pic[40, 184, 8, 8], 8, 8);
		}

		private static void DrawRiverMouths(ref Bytemap output, Direction rivers)
		{
			// The river_overlays.txt deltas draw on the RIVER tile; the legacy
			// TER257 mouths on the sea tile would double them.
			if (Free.HasRiverOverlays) return;
			if (!Resources.Exists("TER257")) return;

			Bytemap pic = Resources["TER257"].Bitmap;

			if (rivers.And(North)) output.AddLayer(pic[128, 176, 16, 16]);
			if (rivers.And(East)) output.AddLayer(pic[144, 176, 16, 16]);
			if (rivers.And(South)) output.AddLayer(pic[160, 176, 16, 16]);
			if (rivers.And(West)) output.AddLayer(pic[176, 176, 16, 16]);
		}

		private static Bytemap? GetOceanLayer((Direction Land, Direction Rivers) directions)
		{
			if (directions.Land == Direction.None && directions.Rivers == Direction.None)
				return null;

			Bytemap output = new Bytemap(16, 16);
			if (directions.Land != Direction.None)
				output.AddLayer(Free.CoastLayer(directions.Land));
			DrawRiverMouths(ref output, directions.Rivers);
			return output;
		}
		
		private static Bytemap? GetRiverLayer(Direction directions) => Free.River(directions);

		private static Bytemap? GetTileLayer<T>(Direction directions) where T : ITile, new()
		{
			if (typeof(T) == typeof(Plains))
				return Free.PlainsTexture();
			if (typeof(T) == typeof(Grassland))
				return Free.GrasslandTexture();
			if (typeof(T) == typeof(Hills))
				return Free.HillTexture(directions);
			if (typeof(T) == typeof(Mountains))
				return Free.Mountains;
			if (typeof(T) == typeof(Swamp))
				return Free.Swamp;
			if (typeof(T) == typeof(Forest))
				return Free.Forest;
			if (typeof(T) == typeof(Jungle))
				return Free.Jungle;
			if (typeof(T) == typeof(Desert))
				return Free.Desert;
			// Free art unconditionally, like every terrain above: terrain id 13 is ours, and
			// the original TER257/SPRITES sheets have no row there to index into.
			if (typeof(T) == typeof(SaltFlat))
				return Free.SaltFlat;
			if (typeof(T) == typeof(Arctic))
				return Free.Arctic;
			if (typeof(T) == typeof(Tundra))
				return Free.Tundra;
			int terrainId = (int)new T().Type;
			string picFile = (GFX256 ? "TER257" : "SPRITES");
			if (!Resources.Exists(picFile))
			{
				switch (new T().Type)
				{
					case Terrain.Arctic: return Free.Arctic;
					case Terrain.Desert: return Free.Desert;
					case Terrain.SaltFlat: return Free.SaltFlat;
					case Terrain.Forest: return Free.Forest;
					case Terrain.Grassland1:
					case Terrain.Grassland2: return Free.Grassland;
					case Terrain.Jungle: return Free.Jungle;
					case Terrain.Hills: return Free.Hills;
					case Terrain.Mountains: return Free.Mountains;
					case Terrain.Plains: return Free.Plains;
					case Terrain.Swamp: return Free.Swamp;
					case Terrain.Tundra: return Free.Tundra;
				}
				return null;
			}
			if (!GFX256)
				return Resources[picFile].Bitmap[terrainId * 16, (directions == Alternating) ? 0 : 16, 16, 16];
			return Resources[picFile].Bitmap[(int)directions * 16, terrainId * 16, 16, 16];
		}

		private static Bytemap GetSpecial<T>() where T : ITile, new()
		{
			if (typeof(T) == typeof(Ocean))
				return Free.Special(Terrain.Ocean);
			if (typeof(T) == typeof(Jungle))
				return Free.Special(Terrain.Jungle);
			if (typeof(T) == typeof(Mountains))
				return Free.Special(Terrain.Mountains);
			if (typeof(T) == typeof(Desert))
				return Free.Special(Terrain.Desert);
			if (typeof(T) == typeof(Forest))
				return Free.Special(Terrain.Forest);
			if (typeof(T) == typeof(Plains))
				return Free.Special(Terrain.Plains);
			if (typeof(T) == typeof(Hills))
				return Free.Special(Terrain.Hills);
			if (typeof(T) == typeof(Swamp))
				return Free.Special(Terrain.Swamp);
			if (typeof(T) == typeof(Arctic))
				return Free.Special(Terrain.Arctic);
			if (typeof(T) == typeof(Tundra))
				return Free.Special(Terrain.Tundra);
			if (typeof(T) == typeof(Grassland))
				return Free.HayBale();
			return Free.Special(new T().Type);
		}

		private static Bytemap GetFog(Direction directions)
		{
			Bytemap output = new Bytemap(16, 16);
			if (directions == None) return output;

			foreach (Direction direction in Cross)
			{
				if (((int)directions & (int)direction) == 0) continue;
				output.AddLayer(Free.Instance.Fog(direction));
			}

			return output;
		}

		private static Bytemap GetRoad(Direction directions)
		{
			// Dirt road: brown spokes toward each connected neighbour, hub at centre.
			const byte ROAD = 6;  // INK_LOW brown
			Bytemap output = new Bytemap(16, 16);

			if (directions == Direction.None)
				return output.FillRectangle(7, 7, 2, 2, ROAD);

			DrawSpokes(output, directions, ROAD, 0, "road");
			output.FillRectangle(7, 7, 2, 2, ROAD);
			return output;
		}

		private static Bytemap GetRailRoad(Direction directions)
		{
			// Railroad: steel-grey spokes with dark crossties, hub at centre.
			const byte RAIL = 20;  // light steel grey (greyscale ramp 16-31)
			const byte TIE  = 5;   // BORDER — dark ties
			Bytemap output = new Bytemap(16, 16);

			if (directions == Direction.None)
				return output.FillRectangle(7, 7, 2, 2, RAIL);

			DrawSpokes(output, directions, RAIL, TIE, "rail");
			output.FillRectangle(7, 7, 2, 2, RAIL);
			return output;
		}

		private static readonly (Direction Dir, string Suffix)[] Spokes =
		[
			(North, "n"), (South, "s"), (East, "e"), (West, "w"),
			(NorthEast, "ne"), (NorthWest, "nw"), (SouthEast, "se"), (SouthWest, "sw"),
		];

		// Draw one spoke per connected neighbour toward the tile edge. Each direction
		// first looks for a "<prefix>_<suffix>" override in improvement_tiles.txt; absent
		// that, it draws the procedural spoke (plus crossties for cardinal rail segments
		// when tie != 0). Transport tubes keep their own glow variant.
		private static void DrawSpokes(Bytemap output, Direction directions, byte colour, byte tie, string overridePrefix)
		{
			foreach (var (dir, suffix) in Spokes)
			{
				if ((directions & dir) == 0) continue;

				byte[]? over = Free.Improvement($"{overridePrefix}_{suffix}");
				if (over is not null)
				{
					output.AddLayer(new Bytemap(16, 16).FromByteArray(over));
					continue;
				}

				DrawSpoke(output, dir, colour, tie);
			}
		}

		// Procedural single-direction spoke; ties (tie != 0) apply only to cardinals.
		private static void DrawSpoke(Bytemap output, Direction dir, byte colour, byte tie)
		{
			switch (dir)
			{
				case North: output.FillRectangle(7, 0, 2, 7, colour); break;
				case South: output.FillRectangle(7, 9, 2, 7, colour); break;
				case East:  output.FillRectangle(9, 7, 7, 2, colour); break;
				case West:  output.FillRectangle(0, 7, 7, 2, colour); break;
				case NorthEast: DrawDiagonalLine(output, 15, 0, 9, 6, colour); return;
				case NorthWest: DrawDiagonalLine(output, 0, 0, 6, 6, colour); return;
				case SouthEast: DrawDiagonalLine(output, 15, 15, 9, 9, colour); return;
				case SouthWest: DrawDiagonalLine(output, 0, 15, 6, 9, colour); return;
			}

			if (tie == 0) return;
			foreach (int t in (int[])[2, 4])
			{
				if (dir == North) output.FillRectangle(6, t, 4, 1, tie);
				if (dir == South) output.FillRectangle(6, 15 - t, 4, 1, tie);
				if (dir == West)  output.FillRectangle(t, 6, 1, 4, tie);
				if (dir == East)  output.FillRectangle(15 - t, 6, 1, 4, tie);
			}
		}

		// Procedurally generated transport-tube sprite (glowing conduit).
		// Same visual works on land and ocean — cyan reads against grass, sand, and dark blue water.
		private static Bytemap GetTransportTube(Direction directions)
		{
			const byte LINE = 11;
			const byte HUB  = 15;

			Bytemap output = new Bytemap(16, 16);

			if (directions == Direction.None)
			{
				output.FillRectangle(6, 6, 4, 4, LINE);
				output.FillRectangle(7, 7, 2, 2, HUB);
				return output;
			}

			if ((directions & North) != 0) output.FillRectangle(7, 0, 2, 7, LINE);
			if ((directions & South) != 0) output.FillRectangle(7, 9, 2, 7, LINE);
			if ((directions & East)  != 0) output.FillRectangle(9, 7, 7, 2, LINE);
			if ((directions & West)  != 0) output.FillRectangle(0, 7, 7, 2, LINE);

			if ((directions & NorthEast) != 0) DrawDiagonalLine(output, 15, 0, 9, 6, LINE);
			if ((directions & NorthWest) != 0) DrawDiagonalLine(output, 0, 0, 6, 6, LINE);
			if ((directions & SouthEast) != 0) DrawDiagonalLine(output, 15, 15, 9, 9, LINE);
			if ((directions & SouthWest) != 0) DrawDiagonalLine(output, 0, 15, 6, 9, LINE);

			output.FillRectangle(7, 7, 2, 2, HUB);
			if ((directions & North) != 0) output.FillRectangle(7, 6, 2, 1, HUB);
			if ((directions & South) != 0) output.FillRectangle(7, 9, 2, 1, HUB);
			if ((directions & East)  != 0) output.FillRectangle(9, 7, 1, 2, HUB);
			if ((directions & West)  != 0) output.FillRectangle(6, 7, 1, 2, HUB);

			return output;
		}

		private static void DrawDiagonalLine(Bytemap b, int x0, int y0, int x1, int y1, byte colour)
		{
			int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = -System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;
			while (true)
			{
				b.FillRectangle(x0, y0, 1, 1, colour);
				if (x0 == x1 && y0 == y1) break;
				int e2 = 2 * err;
				if (e2 >= dy) { err += dy; x0 += sx; }
				if (e2 <= dx) { err += dx; y0 += sy; }
			}
		}

		private static Bytemap GetIrrigation()
		{
			return Free.Irrigation();
		}

		// Mine: [mine] override from improvement_tiles.txt, else a procedural dark
		// shaft cut into the rock with amber ore glints.
		private static Bytemap GetMine()
		{
			byte[]? over = Free.Improvement("mine");
			if (over is not null) return new Bytemap(16, 16).FromByteArray(over);

			const byte rock = 5;   // BORDER dark rock
			const byte shaft = 1;  // BG0 near-black opening
			const byte ore = 12;   // PHOS amber glint
			Bytemap output = new Bytemap(16, 16);
			output.FillRectangle(5, 6, 7, 7, rock);
			output.FillRectangle(6, 8, 5, 4, shaft);
			output[7, 9] = ore; output[9, 10] = ore; output[10, 7] = ore;
			return output;
		}

		// Fortress: [fortress] override, else a procedural crenellated stone wall.
		private static Bytemap GetFortress()
		{
			byte[]? over = Free.Improvement("fortress");
			if (over is not null) return new Bytemap(16, 16).FromByteArray(over);

			const byte wall = 7;  // INK_MID stone
			Bytemap output = new Bytemap(16, 16);
			// Crenellations along the top
			foreach (int x in (int[])[2, 5, 8, 11]) output.FillRectangle(x, 1, 2, 2, wall);
			output.FillRectangle(2, 3, 12, 2, wall);   // top band
			output.FillRectangle(2, 3, 2, 11, wall);   // left
			output.FillRectangle(12, 3, 2, 11, wall);  // right
			output.FillRectangle(2, 12, 12, 2, wall);  // bottom
			return output;
		}

		// Goody hut: [hut] override, else a procedural brown hut with peaked roof.
		private static Bytemap GetHut()
		{
			byte[]? over = Free.Improvement("hut");
			if (over is not null) return new Bytemap(16, 16).FromByteArray(over);

			const byte wall = 6;  // INK_LOW brown
			const byte roof = 5;  // BORDER darker brown
			const byte door = 1;  // BG0 dark doorway
			Bytemap output = new Bytemap(16, 16);
			output.FillRectangle(4, 8, 8, 5, wall);   // body
			output.FillRectangle(6, 5, 4, 2, roof);   // roof peak
			output.FillRectangle(3, 7, 10, 1, roof);  // eaves
			output.FillRectangle(7, 10, 2, 3, door);  // doorway
			return output;
		}

		private static Bytemap GetPollution()
		{
			// Amber phosphor smog cloud: idx 57 = orange-amber body, 9 = bright yellow glow, 1 = golden shadow
			Bytemap output = new Bytemap(16, 16);
			const byte body = 57;   // #e88440 orange-amber
			const byte glow = 9;    // #e8f454 bright yellow highlight
			const byte shadow = 1;  // #a88020 golden shadow edge

			// Left lobe
			for (int x = 3; x <= 7; x++)
			for (int y = 8; y <= 11; y++)
				output[x, y] = body;
			// Right lobe
			for (int x = 7; x <= 11; x++)
			for (int y = 7; y <= 10; y++)
				output[x, y] = body;
			// Bright glow centre pixels
			output[5, 9] = glow; output[6, 9] = glow;
			output[8, 8] = glow; output[9, 8] = glow;
			// Shadow edges
			output[3, 11] = shadow; output[4, 11] = shadow;
			output[10, 10] = shadow; output[11, 10] = shadow;
			output[11, 9] = shadow; output[11, 8] = shadow;
			return output;
		}
		
		public static readonly ISprite LandBase  = new CachedSprite(GetLandBase);
		public static readonly ISprite OceanBase = new CachedSprite(GetOceanBase);
		public static readonly ISprite LakeBase  = new CachedSprite(GetLakeBase);
		public static readonly ISpriteCollection<Direction> LakeShore = new CachedSpriteCollection<Direction>(GetLakeShoreLayer);
		public static readonly ISpriteCollection<Direction> Arctic = new CachedSpriteCollection<Direction>(GetTileLayer<Arctic>);
		public static readonly ISpriteCollection<Direction> Desert = new CachedSpriteCollection<Direction>(GetTileLayer<Desert>);
		public static readonly ISpriteCollection<Direction> Forest = new CachedSpriteCollection<Direction>(GetTileLayer<Forest>);
		public static readonly ISpriteCollection<Direction> Grassland = new CachedSpriteCollection<Direction>(GetTileLayer<Grassland>);
		public static readonly ISpriteCollection<Direction> Hills = new CachedSpriteCollection<Direction>(GetTileLayer<Hills>);
		public static readonly ISpriteCollection<Direction> Jungle = new CachedSpriteCollection<Direction>(GetTileLayer<Jungle>);
		public static readonly ISpriteCollection<Direction> Mountains = new CachedSpriteCollection<Direction>(GetTileLayer<Mountains>);
		public static readonly ISpriteCollection<(Direction, Direction)> Ocean = new CachedSpriteCollection<(Direction, Direction)>(GetOceanLayer);
		public static readonly ISpriteCollection<Direction> Plains = new CachedSpriteCollection<Direction>(GetTileLayer<Plains>);
		public static readonly ISpriteCollection<Direction> SaltFlat = new CachedSpriteCollection<Direction>(GetTileLayer<SaltFlat>);
		public static readonly ISpriteCollection<Direction> River = new CachedSpriteCollection<Direction>(GetRiverLayer);
		// v2 rivers: keyed by (river mask, sea-mouth mask, variant cut). The creator
		// falls back to the legacy layer if the needed section is missing mid-file.
		public static readonly ISpriteCollection<(Direction Rivers, Direction Mouths, int Variant)> RiverOverlay =
			new CachedSpriteCollection<(Direction, Direction, int)>(key =>
				Free.RiverOverlay(key.Item1, key.Item2, key.Item3) ?? GetRiverLayer(key.Item1));
		public static readonly ISpriteCollection<Direction> Swamp = new CachedSpriteCollection<Direction>(GetTileLayer<Swamp>);
		public static readonly ISpriteCollection<Direction> Tundra = new CachedSpriteCollection<Direction>(GetTileLayer<Tundra>);
		public static readonly ISpriteCollection<Direction> Fog = new CachedSpriteCollection<Direction>(GetFog);
		public static readonly ISpriteCollection<Direction> Road = new CachedSpriteCollection<Direction>(GetRoad);
		public static readonly ISpriteCollection<Direction> RailRoad = new CachedSpriteCollection<Direction>(GetRailRoad);
		public static readonly ISpriteCollection<Direction> TransportTube = new CachedSpriteCollection<Direction>(GetTransportTube);
		public static readonly ISprite Irrigation = new CachedSprite(GetIrrigation);
		public static readonly ISprite Mine = new CachedSprite(GetMine);
		public static readonly ISprite Fortress = new CachedSprite(GetFortress);
		public static readonly ISprite Hut = new CachedSprite(GetHut);
		public static readonly ISprite Pollution = new CachedSprite(GetPollution);
		public static readonly ISprite Seals = new CachedSprite(GetSpecial<Arctic>);
		public static readonly ISprite Oasis = new CachedSprite(GetSpecial<Desert>);
		public static readonly ISprite Game = new CachedSprite(GetSpecial<Forest>);
		public static readonly ISprite Shield = new CachedSprite(GetSpecial<Grassland>);
		public static readonly ISprite Coal = new CachedSprite(GetSpecial<Hills>);
		public static readonly ISprite Gems = new CachedSprite(GetSpecial<Jungle>);
		public static readonly ISprite Gold = new CachedSprite(GetSpecial<Mountains>);
		public static readonly ISprite Fish = new CachedSprite(GetSpecial<Ocean>);
		public static readonly ISprite Horses = new CachedSprite(GetSpecial<Plains>);
		public static readonly ISprite Oil = new CachedSprite(GetSpecial<Swamp>);
		public static readonly ISprite TundraGame = new CachedSprite(GetSpecial<Tundra>);

		public static Bytemap? LandCoastErosion(ITile tile)
		{
			if (tile.IsOcean) return null;
			bool N  = tile[0,  -1]?.IsOcean == true;
			bool E  = tile[1,   0]?.IsOcean == true;
			bool S  = tile[0,   1]?.IsOcean == true;
			bool W  = tile[-1,  0]?.IsOcean == true;
			bool NW = tile[-1, -1]?.IsOcean == true;
			bool NE = tile[1,  -1]?.IsOcean == true;
			bool SW = tile[-1,  1]?.IsOcean == true;
			bool SE = tile[1,   1]?.IsOcean == true;

			bool nibbleNW = N && W && NW;
			bool nibbleNE = N && E && NE;
			bool nibbleSW = S && W && SW;
			bool nibbleSE = S && E && SE;

			if (!nibbleNW && !nibbleNE && !nibbleSW && !nibbleSE) return null;

			const byte water = 17;  // CYAN — just water, no second foam line
			Bytemap output = new Bytemap(16, 16);
			if (nibbleNW) { output[0, 0] = water; output[1, 0] = water; output[0, 1] = water; }
			if (nibbleNE) { output[15, 0] = water; output[14, 0] = water; output[15, 1] = water; }
			if (nibbleSW) { output[0, 15] = water; output[1, 15] = water; output[0, 14] = water; }
			if (nibbleSE) { output[15, 15] = water; output[14, 15] = water; output[15, 14] = water; }
			return output;
		}

		public static ISprite TileBase(ITile tile)
		{
			if (tile.IsOcean && tile.X >= 0 && Map.Instance.IsFreshwaterAt(tile.X, tile.Y))
				return LakeBase;
			return tile.IsOcean ? OceanBase : LandBase;
		}
		public static ISprite? TileLayer(ITile tile)
		{
			Direction directions = None, riverDirections = None;
			if (tile is Ocean)
			{
				foreach (Direction direction in (Direction[])[North, East, South, West, NorthWest, NorthEast, SouthWest, SouthEast])
				{
					ITile? borderTile = tile.GetBorderTile(direction);
					if (borderTile is null) continue;
					if (borderTile is Ocean) continue;
					directions |= direction;
				}
				foreach (Direction direction in (Direction[])[North, East, South, West])
				{
					ITile? borderTile = tile.GetBorderTile(direction);
					if (borderTile is null) continue;
					if (borderTile is River) riverDirections |= direction;
				}
			}
			else
			{
				foreach (Direction direction in (Direction[])[North, East, South, West])
				{
					ITile? borderTile = tile.GetBorderTile(direction);
					if (borderTile is null) continue;

					switch (tile)
					{
						case River _:
							// Track which sea edges get a delta (riverDirections doubles
							// as the mouth mask for land river tiles).
							if (borderTile is Ocean) riverDirections |= direction;
							if (borderTile is River || borderTile is Ocean) break;
							continue;
						default:
							if (borderTile.GetType() == tile.GetType()) break;
							continue;
					}

					directions |= direction;
				}
			}

			if (!(tile is River || tile is Ocean) && !GFX256 && Resources.Exists("SPRITES"))
			{
				directions = ((tile.X + tile.Y) % 2 == 1) ? Alternating : Direction.None;
			}
			
			switch (tile)
			{
				case Arctic _: return Arctic[directions];
				case Desert _: return Desert[directions];
				case Forest _: return Forest[directions];
				case Grassland _: return Grassland[directions];
				case Hills _: return Hills[directions];
				case Jungle _: return Jungle[directions];
				case Mountains _: return Mountains[directions];
				case Ocean _:
					if (tile.X >= 0 && Map.Instance.IsFreshwaterAt(tile.X, tile.Y))
						return LakeShore[directions];
					return Ocean[(directions, riverDirections)];
				case Plains _: return Plains[directions];
				case River _:
					if (Free.HasRiverOverlays)
					{
						// Stable coordinate hash picks the _a/_b cut for straights and
						// bends — deterministic across frames and reloads, never RNG.
						int variant = ((tile.X * 7 + tile.Y * 13) & 0x7fffffff) % 2;
						return RiverOverlay[(directions, riverDirections, variant)];
					}
					return River[directions];
				// Without this case a salt flat fell out of the switch to `return null` and drew
				// as bare LandBase — the drained seabed was indistinguishable from grass.
				case SaltFlat _: return SaltFlat[directions];
				case Swamp _: return Swamp[directions];
				case Tundra _: return Tundra[directions];
			}

			return null;
		}
		private static readonly Dictionary<(Terrain, byte), CachedSprite> _faunaSprites = new();

		// Drop every cached tile bitmap so the next redraw recomposites from the
		// (freshly re-read) free_tiles.txt / shore / lake / river files. Covers the
		// base fields, all baseline terrain, rivers and lakes, improvement overlays,
		// and the special-resource sprites — the whole map repaints from the files.
		public static void ReloadTileCaches()
		{
			_faunaSprites.Clear();
			foreach (object o in new object[]
			{
				// base fields under every tile
				LandBase, OceanBase, LakeBase,
				// baseline terrain
				Arctic, Desert, Forest, Grassland, Hills, Jungle, Mountains, Ocean, Plains, SaltFlat, Swamp, Tundra,
				// rivers and lake shores
				River, RiverOverlay, LakeShore,
				// improvement overlays sourced from the tile files
				Irrigation, Mine, Fortress, Hut, Road, RailRoad, Pollution,
				// special-resource sprites
				Seals, Oasis, Game, Shield, Coal, Gems, Gold, Fish, Horses, Oil, TundraGame,
			})
				(o as ICached)?.Clear();
		}

		private static ISprite FaunaSprite(Terrain terrain, byte continentId)
		{
			var key = (terrain, continentId);
			if (!_faunaSprites.TryGetValue(key, out var sprite))
				_faunaSprites[key] = sprite = new CachedSprite(() => Free.Special(terrain, continentId));
			return sprite;
		}

		public static ISprite? TileSpecial(ITile tile)
		{
			if (tile is River || (!tile.Special && tile.Type != Terrain.Grassland2)) return null;
			switch (tile)
			{
				case Arctic _: return Seals;
				case Desert _: return Oasis;
				case Forest _: return FaunaSprite(Terrain.Forest, tile.ContinentId);
				case Grassland _: return Shield;
				case Hills _: return Coal;
				case Jungle _: return Gems;
				case Mountains _: return Gold;
				case Ocean _: return Fish;
				case Plains _: return FaunaSprite(Terrain.Plains, tile.ContinentId);
				case Swamp _: return Oil;
				case Tundra _: return FaunaSprite(Terrain.Tundra, tile.ContinentId);
			}
			return null;
		}
	}
}