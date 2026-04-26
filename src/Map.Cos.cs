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
using CivOne.Persistence;
using CivOne.Tiles;

namespace CivOne
{
	public partial class Map
	{
		internal void InitializeForCosLoad(int terrainSeed)
		{
			_terrainMasterWord = terrainSeed;
			_tiles = new ITile[WIDTH, HEIGHT];
			Ready = false;
		}

		internal void FinalizeForCosLoad()
		{
			CalculateLandValue();
			Ready = true;
			Log("Map: Ready (loaded from COS)");
		}

		internal CosMap SaveToCos()
		{
			var terrain = new byte[WIDTH * HEIGHT];
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				byte code;
				switch (_tiles[x, y].Type)
				{
					case Enums.Terrain.Forest:     code =  2; break;
					case Enums.Terrain.Swamp:      code =  3; break;
					case Enums.Terrain.Plains:     code =  6; break;
					case Enums.Terrain.Tundra:     code =  7; break;
					case Enums.Terrain.River:      code =  9; break;
					case Enums.Terrain.Grassland1:
					case Enums.Terrain.Grassland2: code = 10; break;
					case Enums.Terrain.Jungle:     code = 11; break;
					case Enums.Terrain.Hills:      code = 12; break;
					case Enums.Terrain.Mountains:  code = 13; break;
					case Enums.Terrain.Desert:     code = 14; break;
					case Enums.Terrain.Arctic:     code = 15; break;
					default:                       code =  1; break; // Ocean
				}
				terrain[y * WIDTH + x] = code;
			}

			var improvements = new List<CosImprovement>();
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				var t = _tiles[x, y];
				if (!t.Road && !t.RailRoad && !t.Irrigation && !t.Mine && !t.Hut) continue;
				improvements.Add(new CosImprovement
				{
					X = x, Y = y,
					Road      = t.Road,
					Railroad  = t.RailRoad,
					Irrigation = t.Irrigation,
					Mine      = t.Mine,
					Hut       = t.Hut
				});
			}

			return new CosMap
			{
				TerrainSeed  = _terrainMasterWord,
				Terrain      = Convert.ToBase64String(terrain),
				Improvements = improvements
			};
		}

		internal void LoadFromCos(CosMap cos)
		{
			InitializeForCosLoad(cos.TerrainSeed);

			byte[] terrain = Convert.FromBase64String(cos.Terrain);
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				byte code = terrain[y * WIDTH + x];
				bool special = TileIsSpecial(x, y);
				ITile tile;
				switch (code)
				{
					case  2: tile = new Forest    (x, y, special); break;
					case  3: tile = new Swamp     (x, y, special); break;
					case  6: tile = new Plains    (x, y, special); break;
					case  7: tile = new Tundra    (x, y, special); break;
					case  9: tile = new River     (x, y);          break;
					case 10: tile = new Grassland (x, y);          break;
					case 11: tile = new Jungle    (x, y, special); break;
					case 12: tile = new Hills     (x, y, special); break;
					case 13: tile = new Mountains (x, y, special); break;
					case 14: tile = new Desert    (x, y, special); break;
					case 15: tile = new Arctic    (x, y, special); break;
					default: tile = new Ocean     (x, y, special); break;
				}
				_tiles[x, y] = tile;
			}

			if (cos.Improvements != null)
			{
				foreach (var imp in cos.Improvements)
				{
					if (imp.X < 0 || imp.X >= WIDTH || imp.Y < 0 || imp.Y >= HEIGHT) continue;
					var t = _tiles[imp.X, imp.Y];
					t.Road       = imp.Road;
					t.RailRoad   = imp.Railroad;
					t.Irrigation = imp.Irrigation;
					t.Mine       = imp.Mine;
					t.Hut        = imp.Hut;
				}
			}

			FinalizeForCosLoad();
		}
	}
}
