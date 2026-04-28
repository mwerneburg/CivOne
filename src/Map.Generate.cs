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
using System.Threading.Tasks;
using CivOne.Enums;
using CivOne.Screens;
using CivOne.Tasks;
using CivOne.Tiles;

namespace CivOne
{
	public partial class Map
	{
		private bool[,] GenerateLandChunk(int[,] existingElevation, bool growFromExisting)
		{
			bool[,] stencil = new bool[WIDTH, HEIGHT];

			int x, y;
			if (growFromExisting)
			{
				// Find a random existing land tile to grow the continent from.
				// Random sampling is fast enough: at 10% land coverage the expected
				// number of tries is ~9; 256 attempts is a safe upper bound.
				int attempts = 0;
				do {
					x = Common.Random.Next(2, WIDTH - 2);
					y = Common.Random.Next(4, HEIGHT - 4);
					attempts++;
				} while (existingElevation[x, y] == 0 && attempts < 256);

				if (existingElevation[x, y] == 0)
				{
					// Fallback: no existing land found (shouldn't happen after seeding)
					x = Common.Random.Next(4, WIDTH - 4);
					y = Common.Random.Next(8, HEIGHT - 8);
				}
			}
			else
			{
				x = Common.Random.Next(4, WIDTH - 4);
				y = Common.Random.Next(8, HEIGHT - 8);
			}

			// Scale walk length with map area so continent proportions are consistent
			// across map sizes. sqrt(WIDTH*HEIGHT)/2 gives ~16 for Tiny, ~32 for Normal,
			// ~63 for Huge.
			int baseLen = (int)Math.Sqrt(WIDTH * HEIGHT) / 2;
			int pathLength = baseLen + Common.Random.Next(baseLen);

			for (int i = 0; i < pathLength; i++)
			{
				if (x >= 0 && x < WIDTH - 1 && y >= 0 && y < HEIGHT - 1)
				{
					stencil[x, y] = true;
					stencil[x + 1, y] = true;
					stencil[x, y + 1] = true;
				}
				switch (Common.Random.Next(4))
				{
					case 0: y--; break;
					case 1: x++; break;
					case 2: y++; break;
					default: x--; break;
				}
				if (x < 2 || y < 2 || x > (WIDTH - 3) || y > (HEIGHT - 4)) break;
			}

			return stencil;
		}

		private bool TileHasHut(int x, int y)
		{
			if (y < 2 || y > (HEIGHT - 3)) return false;
			return ModGrid(x, y) == ((x / 4) * 13 + (y / 4) * 11 + _terrainMasterWord + 8) % 32;
		}

		private int[,] GenerateLandMass()
		{
			Log("Map: Stage 1 - Generate land mass");

			int[,] elevation = new int[WIDTH, HEIGHT];
			int landMassSize = (int)((WIDTH * HEIGHT) / 12.5) * (_landMass + 2);

			// Seed count is inversely tied to land mass: less land spreads across more
			// pieces; more land consolidates into fewer, larger continents.
			// Small=0 → 4-6 seeds, Normal=1 → 3-5, Large=2 → 2-4.
			int numSeeds = (4 - _landMass) + Common.Random.Next(3);
			for (int i = 0; i < numSeeds; i++)
			{
				bool[,] seed = GenerateLandChunk(elevation, growFromExisting: false);
				for (int y = 0; y < HEIGHT; y++)
				for (int x = 0; x < WIDTH; x++)
					if (seed[x, y]) elevation[x, y]++;
			}

			// Grow from existing land until target coverage is reached.
			// Starting each walk from an existing land tile causes new land to
			// accrete onto existing masses rather than spawning isolated islands.
			while ((from int tile in elevation where tile > 0 select 1).Sum() < landMassSize)
			{
				bool[,] chunk = GenerateLandChunk(elevation, growFromExisting: true);
				for (int y = 0; y < HEIGHT; y++)
				for (int x = 0; x < WIDTH; x++)
				{
					if (chunk[x, y]) elevation[x, y]++;
				}
			}
			
			// remove narrow passages
			for (int y = 0; y < (HEIGHT - 1); y++)
			for (int x = 0; x < (WIDTH - 1); x++)
			{
				if ((elevation[x, y] > 0 && elevation[x + 1, y + 1] > 0) && (elevation[x + 1, y] == 0 && elevation[x, y + 1] == 0))
				{
					elevation[x + 1, y]++;
					elevation[x, y + 1]++;
				}
				else if ((elevation[x, y] == 0 && elevation[x + 1, y + 1] == 0) && (elevation[x + 1, y] > 0 && elevation[x, y + 1] > 0))
				{
					elevation[x + 1, y + 1]++;
				}
			}
			
			return elevation;
		}
		
		private int[,] TemperatureAdjustments()
		{
			Log("Map: Stage 2 - Temperature adjustments");
			
			int[,] latitude = new int[WIDTH, HEIGHT];
			
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				int l = (int)((float)Math.Abs(y * 2 - (HEIGHT - 1)) / HEIGHT * 29);
				l += Common.Random.Next(7);
				if (l < 0) l = -l;
				l += 1 - _temperature;
				
				l = (l / 6) + 1;
				
				switch (l)
				{
					case 0:
					case 1: latitude[x, y] = 0; break;
					case 2:
					case 3: latitude[x, y] = 1; break;
					case 4:
					case 5: latitude[x, y] = 2; break;
					case 6:
					default: latitude[x, y] = 3; break;
				}
			}
			
			return latitude;
		}
		
		private void MergeElevationAndLatitude(int[,] elevation, int[,] latitude)
		{
			Log("Map: Stage 3 - Merge elevation and latitude into the map");
			
			// merge elevation and latitude into the map
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				bool special = TileIsSpecial(x, y);
				switch (elevation[x, y])
				{
					case 0: _tiles[x, y] = new Ocean(x, y, special); break;
					case 1:
						{
							switch (latitude[x, y])
							{
								case 0: _tiles[x, y] = new Desert(x, y, special); break;
								case 1: _tiles[x, y] = new Plains(x, y, special); break;
								case 2: _tiles[x, y] = new Tundra(x, y, special); break;
								case 3: _tiles[x, y] = new Arctic(x, y, special); break;
							}
						}
						break;
					case 2: _tiles[x, y] = new Hills(x, y, special); break;
					default: _tiles[x, y] = new Mountains(x, y, special); break;
				}
			}
		}
		
		private void ClimateAdjustments()
		{
			Log("Map: Stage 4 - Climate adjustments");
			
			int wetness, latitude;
			
			for (int y = 0; y < HEIGHT; y++)
			{
				int yy = (int)(((float)y / HEIGHT) * 50);
				
				wetness = 0;
				latitude = Math.Abs(25 - yy);
				
				for (int x = 0; x < WIDTH; x++)
				{
					if (_tiles[x, y].Type == Terrain.Ocean)
					{
						// wetness yield
						int wy = latitude - 12;
						if (wy < 0) wy = -wy;
						wy += (_climate * 4);
						
						if (wy > wetness) wetness++;
					}
					else if (wetness > 0)
					{
						bool special = TileIsSpecial(x, y);
						int rainfall = Common.Random.Next(7 - (_climate * 2));
						wetness -= rainfall;
						
						switch (_tiles[x, y].Type)
						{
							case Terrain.Plains: _tiles[x, y] = new Grassland(x, y); break;
							case Terrain.Tundra: _tiles[x, y] = new Arctic(x, y, special); break;
							case Terrain.Hills: _tiles[x, y] = new Forest(x, y, special); break;
							case Terrain.Desert: _tiles[x, y] = new Plains(x, y, special); break;
							case Terrain.Mountains: wetness -= 3; break;
						}
					}
				}
				
				wetness = 0;
				latitude = Math.Abs(25 - yy);
				
				// reset row wetness to 0
				for (int x = WIDTH - 1; x >= 0; x--)
				{
					if (_tiles[x, y].Type == Terrain.Ocean)
					{
						// wetness yield
						int wy = (latitude / 2) + _climate;
						if (wy > wetness) wetness++;
					}
					else if (wetness > 0)
					{
						bool special = TileIsSpecial(x, y);
						int rainfall = Common.Random.Next(7 - (_climate * 2));
						wetness -= rainfall;
						
						switch (_tiles[x, y].Type)
						{
							case Terrain.Swamp: _tiles[x, y] = new Forest(x, y, special); break;
							case Terrain.Plains: _tiles[x, y] = new Grassland(x, y); break;
							case Terrain.Grassland1:
							case Terrain.Grassland2: _tiles[x, y] = new Jungle(x, y, special); break;
							case Terrain.Hills: _tiles[x, y] = new Forest(x, y, special); break;
							case Terrain.Mountains: _tiles[x, y] = new Forest(x, y, special); wetness -= 3; break;
							case Terrain.Desert: _tiles[x, y] = new Plains(x, y, special); break;
						}
					}
				}
			}
		}
		
		private void AgeAdjustments()
		{
			Log("Map: Stage 5 - Age adjustments");
			
			int x = 0;
			int y = 0;
			int ageRepeat = (int)(((float)800 * (1 + _age) / (80 * 50)) * (WIDTH * HEIGHT));
			for (int i = 0; i < ageRepeat; i++)
			{
				if (i % 2 == 0)
				{
					x = Common.Random.Next(WIDTH);
					y = Common.Random.Next(HEIGHT);
				}
				else
				{
					switch (Common.Random.Next(8))
					{
						case 0: { x--; y--; break; }
						case 1: { y--; break; }
						case 2: { x++; y--; break; }
						case 3: { x--; break; }
						case 4: { x++; break; }
						case 5: { x--; y++; break; }
						case 6: { y++; break; }
						default: { x++; y++; break; }
					}
					if (x < 0) x = 1;
					if (y < 0) y = 1;
					if (x >= WIDTH) x = WIDTH - 2;
					if (y >= HEIGHT) y = HEIGHT - 2;
				}
				
				bool special = TileIsSpecial(x, y);
				switch (_tiles[x, y].Type)
				{
					case Terrain.Forest: _tiles[x, y] = new Jungle(x, y, special); break;
					case Terrain.Swamp: _tiles[x, y] = new Grassland(x, y); break;
					case Terrain.Plains: _tiles[x, y] = new Hills(x, y, special); break;
					case Terrain.Tundra: _tiles[x, y] = new Hills(x, y, special); break;
					case Terrain.River: _tiles[x, y] = new Forest(x, y, special); break;
					case Terrain.Grassland1:
					case Terrain.Grassland2: _tiles[x, y] = new Forest(x, y, special); break;
					case Terrain.Jungle: _tiles[x, y] = new Swamp(x, y, special); break;
					case Terrain.Hills: _tiles[x, y] = new Mountains(x, y, special); break;
					case Terrain.Mountains:
						if ((x == 0 || _tiles[x - 1, y - 1].Type != Terrain.Ocean) &&
						    (y == 0 || _tiles[x + 1, y - 1].Type != Terrain.Ocean) &&
							(x == (WIDTH - 1) || _tiles[x + 1, y + 1].Type != Terrain.Ocean) &&
							(y == (HEIGHT - 1) || _tiles[x - 1, y + 1].Type != Terrain.Ocean))
						_tiles[x, y] = new Ocean(x, y, special);
						break;
					case Terrain.Desert: _tiles[x, y] = new Plains(x, y, special); break;
					case Terrain.Arctic: _tiles[x, y] = new Mountains(x, y, special); break;
				}
			}
		}
		
		private void CreateRivers()
		{
			Log("Map: Stage 6 - Create rivers");
			
			int rivers = 0;
			for (int i = 0; i < 256 && rivers < ((_climate + _landMass) * 2) + 6; i++)
			{
				ITile[,] tilesBackup = (ITile[,])_tiles.Clone();
				
				int riverLength = 0;
				int varA = Common.Random.Next(4) * 2;
				bool nearOcean = false;
				
				ITile tile = null;
				while (tile == null)
				{
					int x = Common.Random.Next(WIDTH);
					int y = Common.Random.Next(HEIGHT);
					if (_tiles[x, y].Type == Terrain.Hills) tile = _tiles[x, y];
				}
				do
				{
					_tiles[tile.X, tile.Y] = new River(tile.X, tile.Y);
					int varB = varA;
					int varC = Common.Random.Next(2);
					varA = (((varC - riverLength % 2) * 2 + varA) & 0x07);
					varB = 7 - varB;
					
					riverLength++;
					
					nearOcean = NearOcean(tile.X, tile.Y);
					switch (varA)
					{
						case 0:
						case 1: tile = _tiles[tile.X, tile.Y - 1]; break;
						case 2:
						case 3: tile = _tiles[tile.X + 1, tile.Y]; break;
						case 4:
						case 5: tile = _tiles[tile.X, tile.Y + 1]; break;
						case 6:
						case 7: tile = _tiles[tile.X - 1, tile.Y]; break;
					}
				}
				while (!nearOcean && (tile.GetType() != typeof(Ocean) && tile.GetType() != typeof(River) && tile.GetType() != typeof(Mountains)));
				
				if ((nearOcean || tile.Type == Terrain.River) && riverLength > 5)
				{
					rivers++;
					ITile[,] mapPart = this[tile.X - 3, tile.Y - 3, 7, 7];
					for (int x = 0; x < 7; x++)
					for (int y = 0; y < 7; y++)
					{
						if (mapPart[x, y] == null) continue;
						int xx = mapPart[x, y].X, yy = mapPart[x, y].Y;
						if (_tiles[xx, yy].Type == Terrain.Forest)
							_tiles[xx, yy] = new Jungle(xx, yy, TileIsSpecial(x, y));
					}
				}
				else
				{
					_tiles = (ITile[,])tilesBackup.Clone(); ;
				}
			}
		}
		
		private void CalculateContinentSize()
		{
			Log("Map: Calculate continent and ocean sizes");

			// BFS flood-fill: find all connected regions of same land/ocean type.
			// The map wraps horizontally but not vertically.
			var regions = new List<List<ITile>>();
			var visited = new bool[WIDTH, HEIGHT];

			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				if (visited[x, y]) continue;

				var region = new List<ITile>();
				var queue = new Queue<(int x, int y)>();
				bool isOcean = _tiles[x, y].IsOcean;

				queue.Enqueue((x, y));
				visited[x, y] = true;

				while (queue.Count > 0)
				{
					var (cx, cy) = queue.Dequeue();
					region.Add(_tiles[cx, cy]);

					// 4-connected neighbours; X wraps, Y clamps
					(int dx, int dy)[] neighbours = { (0,-1), (0,1), (-1,0), (1,0) };
					foreach (var (dx, dy) in neighbours)
					{
						int nx = (cx + dx + WIDTH) % WIDTH;
						int ny = cy + dy;
						if (ny < 0 || ny >= HEIGHT) continue;
						if (visited[nx, ny]) continue;
						if (_tiles[nx, ny].IsOcean != isOcean) continue;
						visited[nx, ny] = true;
						queue.Enqueue((nx, ny));
					}
				}

				regions.Add(region);
			}

			// Clear all IDs first
			foreach (ITile tile in AllTiles())
				tile.ContinentId = 15;

			// Assign IDs 1–14 to the 14 largest land regions; everything else stays 15
			var landRegions = regions
				.Where(r => !r[0].IsOcean)
				.OrderByDescending(r => r.Count)
				.ToList();

			for (int i = 0; i < Math.Min(14, landRegions.Count); i++)
			{
				byte id = (byte)(i + 1);
				foreach (ITile tile in landRegions[i])
					tile.ContinentId = id;
			}
		}
		
		private void CreatePoles()
		{
			Log("Map: Creating poles");
			
			for (int x = 0; x < WIDTH; x++)
			foreach (int y in new int[] { 0, (HEIGHT - 1) })
			{
				_tiles[x, y] = new Arctic(x, y, false);
			}
			
			for (int i = 0; i < (WIDTH / 4); i++)
			foreach (int y in new int[] { 0, 1, (HEIGHT - 2), (HEIGHT - 1) })
			{
				int x = Common.Random.Next(WIDTH);
				_tiles[x, y] = new Tundra(x, y, false);
			}
		}
		
		private void PlaceHuts()
		{
			Log("Map: Placing goody huts");
			
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				if (_tiles[x, y].Type == Terrain.Ocean) continue;
				_tiles[x, y].Hut = TileHasHut(x, y);
			}
		}

		private void CalculateLandValue()
		{
			Log("Map: Calculating land value");
			
			// This code is a translation of Darkpanda's forum post here: http://forums.civfanatics.com/showthread.php?t=498532
			// Comments are pasted from the original forum thread to make the code more readable.
			
			// map squares for which the land value is calculated are in the range [2,2] - [77,47]
			for (int y = 2; y < HEIGHT - 2; y++)
			for (int x = 2; x < WIDTH - 2; x++)
			{
				// initial value is 0
				_tiles[x, y].LandValue = 0;
				
				// If the square's terrain type is not Plains, Grassland or River, then its land value is 0
				if (!TileIsType(_tiles[x, y], Terrain.Plains, Terrain.Grassland1, Terrain.Grassland2, Terrain.River)) continue;
				
				// for each 'city square' neighbouring the map square (i.e. each square following the city area pattern,
				// including the map square itself, so totally 21 'neighbours'), compute the following neighbour value (initially 0):
				int landValue = 0;
				for (int yy = -2; yy <= 2; yy++)
				for (int xx = -2; xx <= 2; xx++)
				{
					// Skip the corners of the square to create a city area pattern
					if (Math.Abs(xx) == 2 && Math.Abs(yy) == 2) continue;
					
					// initial value is 0
					int val = 0;
					
					ITile tile = _tiles[x + xx, y + yy];
					if (tile.Special && TileIsType(tile, Terrain.Grassland1, Terrain.Grassland2, Terrain.River))
					{
						// If the neighbour square type is Grassland special or River special, add 2,
						// then add the non-special Grassland or River terrain type score to the neighbour value
						val += 2;
						if (tile.Type == Terrain.River)
							val += (new River()).LandScore;
						else
							val += (new Grassland()).LandScore;
					}
					else
					{
						// Else add neighbour's terrain type score to the neighbour value
						val += tile.LandScore;
					}
					
					// If the neighbour square is in the map square inner circle, i.e. one of the 8 neighbours immediatly
					// surrounding the map square, then multiply the neighbour value by 2
					if (Math.Abs(xx) <= 1 && Math.Abs(yy) <= 1 && (xx != 0 || yy != 0)) val *= 2;
					
					// If the neighbour square is the North square (relative offset 0,-1), then multiply the neighbour value by 2 ;
					// note: I actually think that this is a bug, and that the intention was rather to multiply by 2 if the 'neighbour'
					// was the central map square itself... the actual CIV code for this is to check if the 'neighbour index' is '0';
					// the neighbour index is used to retrieve the neighbour's relative offset coordinates (x,y) from the central square,
					// and the central square itself is actually the last in the list (index 20), the first one (index 0) being
					// the North neighbour; another '7x7 neighbour pattern' table found in CIV code does indeed set the central square
					// at index 0, and this why I believe ths is a programmer's mistake...
					if (xx == 0 && yy == -1) val *= 2;
					
					// Add the neighbour's value to the map square total value and loop to next neighbour
					landValue += val;
				}
				
				// After all neighbours are processed, if the central map square's terrain type is non-special Grassland or River,
				// subtract 16 from total land value
				if (!_tiles[x, y].Special && TileIsType(_tiles[x, y], Terrain.Grassland1, Terrain.River)) landValue -= 16;
				
				landValue -= 120; // Substract 120 (0x78) from the total land value,
				bool negative = (landValue < 0); // and remember its sign
				landValue = Math.Abs(landValue); // Set the land value to the absolute land value (i.e. negate it if it is negative)
				landValue /= 8; // Divide the land value by 8
				if (negative) landValue = 1 - landValue; // If the land value was negative 3 steps before, then negate the land value and add 1
				
				// Adjust the land value to the range [1..15]
				if (landValue < 1) landValue = 1;
				if (landValue > 15) landValue = 15;
				
				landValue /= 2; // Divide the land value by 2
				landValue += 8; // And finally, add 8 to the land value
				_tiles[x, y].LandValue = (byte)landValue;
			}
		}
		
		private void GenerateThread()
		{
			Log("Generating map (Land Mass: {0}, Temperature: {1}, Climate: {2}, Age: {3})", _landMass, _temperature, _climate, _age);
			
			_tiles = new ITile[WIDTH, HEIGHT];
			
			int[,] elevation = GenerateLandMass();
			int[,] latitude = TemperatureAdjustments();
			MergeElevationAndLatitude(elevation, latitude);
			ClimateAdjustments();
			AgeAdjustments();
			CreateRivers();
			
			CalculateContinentSize();
			CreatePoles();
			PlaceHuts();
			CalculateLandValue();
			
			Ready = true;
			Log("Map: Ready");
		}
		
		public void Generate(int landMass = 1, int temperature = 1, int climate = 1, int age = 1, int width = 80, int height = 50)
		{
			if (Ready || _tiles != null)
			{
				Log("ERROR: Map is already load{0}/generat{0}", (Ready ? "ed" : "ing"));
				return;
			}

			_width = width;
			_height = height;
			_landMass = landMass;
			_temperature = temperature;
			_climate = climate;
			_age = age;

			Task.Run(() => GenerateThread());
		}
	}
}