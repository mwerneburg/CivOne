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
using CivOne.Graphics;
using CivOne.Tiles;

namespace CivOne
{
	public partial class Map
	{
		private static Resources Resources = Resources.Instance;
		private static void Log(string text, params object[] parameters) => RuntimeHandler.Runtime.Log(text, parameters);

		private static int _width = 80, _height = 50;
		public static int WIDTH => _width;
		public static int HEIGHT => _height;
		
		private int _terrainMasterWord;
		private int _landMass, _temperature, _climate, _age;
		private ITile[,] _tiles;
		private bool[,] _freshwater;

		// Flood-fill every connected ocean region.  Any region whose tile count is
		// at or below LAKE_MAX is a freshwater lake — too small to be the open sea.
		// Swamp tiles are also freshwater (wetlands).  This size-based approach is
		// robust against the polar rows being Arctic in a loaded save, and against
		// bays that happen to have a thin channel to the global ocean.
		internal void ComputeFreshwaterLakes()
		{
			_freshwater = new bool[WIDTH, HEIGHT];

			int lakeMax = Math.Max(20, (WIDTH * HEIGHT) / 120);  // ≈33 for 80×50

			var label  = new int[WIDTH, HEIGHT];
			var sizes  = new List<int> { 0 };  // index 0 unused
			int next   = 1;

			int[] ddx = { 0, 0, -1, 1 };
			int[] ddy = { -1, 1, 0, 0 };

			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				if (!_tiles[x, y].IsOcean || label[x, y] != 0) continue;

				int id   = next++;
				int size = 0;
				var q    = new Queue<(int, int)>();
				q.Enqueue((x, y));
				label[x, y] = id;

				while (q.Count > 0)
				{
					var (cx, cy) = q.Dequeue();
					size++;
					for (int d = 0; d < 4; d++)
					{
						int nx = (cx + ddx[d] + WIDTH) % WIDTH;
						int ny = cy + ddy[d];
						if (ny < 0 || ny >= HEIGHT || label[nx, ny] != 0) continue;
						if (!_tiles[nx, ny].IsOcean) continue;
						label[nx, ny] = id;
						q.Enqueue((nx, ny));
					}
				}

				sizes.Add(size);
			}

			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				int id = label[x, y];
				if (id > 0 && sizes[id] <= lakeMax)
					_freshwater[x, y] = true;
				else if (_tiles[x, y].Type == Terrain.Swamp)
					_freshwater[x, y] = true;
			}
		}

		// Returns true if (x,y) is a freshwater source: enclosed lake or swamp.
		internal bool IsFreshwaterAt(int x, int y) => _freshwater is not null && _freshwater[x, y];
		
		public bool Ready { get; private set; }
		public bool FixedStartPositions { get; private set; }

		public IEnumerable<ITile> QueryMapPart(int x, int y, int width, int height)
		{
			ITile[,] area = this[x, y, width, height];
			for (int yy = 0; yy < height; yy++)
			for (int xx = 0; xx < width; xx++)
			{
				yield return area[xx, yy];
			}
		}
		
		public IEnumerable<ITile> AllTiles()
		{
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				yield return this[x, y];
			}
		}
		
		private bool NearOcean(int x, int y)
		{
			for (int relY = -1; relY <= 1; relY++)
			for (int relX = -1; relX <= 1; relX++)
			{
				if (Math.Abs(relX) == Math.Abs(relY)) continue;
				int ny = y + relY;
				if (ny < 0 || ny >= HEIGHT) continue;
				int nx = (x + relX + WIDTH) % WIDTH;
				if (_tiles[nx, ny] is Ocean) return true;
			}
			return false;
		}
		
		internal static bool TileIsType(ITile tile, params Terrain[] terrain) => terrain.Any(x => tile.Type == x);

		public void ChangeTileType(int x, int y, Terrain type)
		{
			bool special = TileIsSpecial(x, y);
			bool road = _tiles[x, y].Road;
			bool railRoad = _tiles[x, y].RailRoad;
			switch(type)
			{
				case Terrain.Forest: _tiles[x, y] = new Forest(x, y, special); break;
				case Terrain.Swamp: _tiles[x, y] = new Swamp(x, y, special); break;
				case Terrain.Plains: _tiles[x, y] = new Plains(x, y, special); break;
				case Terrain.Tundra: _tiles[x, y] = new Tundra(x, y, special); break;
				case Terrain.River: _tiles[x, y] = new River(x, y); break;
				case Terrain.Grassland1:
				case Terrain.Grassland2: _tiles[x, y] = new Grassland(x, y); break;
				case Terrain.Jungle: _tiles[x, y] = new Jungle(x, y, special); break;
				case Terrain.Hills: _tiles[x, y] = new Hills(x, y, special); break;
				case Terrain.Mountains: _tiles[x, y] = new Mountains(x, y, special); break;
				case Terrain.Desert: _tiles[x, y] = new Desert(x, y, special); break;
				case Terrain.Arctic: _tiles[x, y] = new Arctic(x, y, special); break;
				case Terrain.Ocean: _tiles[x, y] = new Ocean(x, y, special); break;
			}
			_tiles[x, y].Road = road;
			_tiles[x, y].RailRoad = railRoad;
		}
		
		private int ModGrid(int x, int y) => (x % 4) * 4 + (y % 4);
		
		private bool TileIsSpecial(int x, int y)
		{
			if (y < 2 || y > (HEIGHT - 3)) return false;
			return ModGrid(x, y) == ((x / 4) * 13 + (y / 4) * 11 + _terrainMasterWord) % 16;
		}
		
		public IEnumerable<ITile> ContinentTiles(int continentId) => AllTiles().Where(t => t.ContinentId == continentId);
		
		public IEnumerable<City> ContentCities(int continentId) => ContinentTiles(continentId).Where(x => x.City is not null).Select(x => x.City).ToArray();
		
		public ITile this[int x, int y]
		{
			get
			{
				if (y < 0 || y >= HEIGHT) return null;
				
				while (x < 0) x += WIDTH;
				x = (x % WIDTH);
				
				return _tiles[x, y];
			}
			private set
			{
				while (x < 0) x += WIDTH;
				while (y < 0) y += HEIGHT;
				x = (x % WIDTH);
				y = (y % HEIGHT);
				
				_tiles[x, y] = value;
			}
		}
		
		public ITile[,] this[int x, int y, int width, int height]
		{
			get
			{
				if (width < 0)
				{
					width = Math.Abs(width);
					x -= width;
				}
				if (height < 0)
				{
					height = Math.Abs(height);
					y -= height;
				}

				ITile[,] output = new ITile[width, height];
				
				for (int yy = y; yy < y + height; yy++)
				for (int xx = x; xx < x + width; xx++)
				{
					output[xx - x, yy - y] = this[xx, yy];
				}
				
				return output;
			}
		}
		
		private static Map _instance;
		public static Map Instance
		{
			get
			{
				if (_instance is null)
					_instance = new Map();
				return _instance;
			}
		}
		
		private Map()
		{
			_terrainMasterWord = Common.Random.Next(16);
			Ready = false;
			
			Log("Map instance created");
		}
	}
}