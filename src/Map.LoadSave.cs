#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.IO;
using System.Threading.Tasks;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;
using CivOne.IO;
using CivOne.Tiles;

namespace CivOne
{
	public partial class Map
	{
		private void LoadMap(Bytemap bitmap)
		{
			_tiles = new ITile[WIDTH, HEIGHT];
			
			for (int x = 0; x < WIDTH; x++)
			for (int y = 0; y < HEIGHT; y++)
			{
				ITile tile;
				bool special = TileIsSpecial(x, y);
				switch (bitmap[x, y])
				{
					case 2: tile = new Forest(x, y, special); break;
					case 3: tile = new Swamp(x, y, special); break;
					case 6: tile = new Plains(x, y, special); break;
					case 7: tile = new Tundra(x, y, special); break;
					case 9: tile = new River(x, y); break;
					case 10: tile = new Grassland(x, y); break;
					case 11: tile = new Jungle(x, y, special); break;
					case 12: tile = new Hills(x, y, special); break;
					case 13: tile = new Mountains(x, y, special); break;
					case 14: tile = new Desert(x, y, special); break;
					case 15: tile = new Arctic(x, y, special); break;
					default: tile = new Ocean(x, y, special); break;
				}
				_tiles[x, y] = tile;
			}
		}
		
		public void LoadMap(string filename, int randomSeed)
		{
			Log("Map: Loading {0} - Random seed: {1}", filename, randomSeed);
			_terrainMasterWord = randomSeed;
			
			using (Bytemap bitmap = Resources[filename].Bitmap)
			{
				_tiles = new ITile[WIDTH, HEIGHT];
				
				LoadMap(bitmap);
				PlaceHuts();
				CalculateLandValue();
				
				// Load improvement layer
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					if (_tiles[x, y].IsOcean) continue;
					byte b = bitmap[x, y + (HEIGHT * 2)];
					// 0x01 = CITY ?
					_tiles[x, y].Irrigation = (b & 0x02) > 0;
					_tiles[x, y].Mine = (b & 0x04) > 0;
					_tiles[x, y].Road = (b & 0x08) > 0;
					_tiles[x, y].Pollution = (b & 0x10) > 0;
				}

				// Load improvement layer 2
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					if (_tiles[x, y].IsOcean) continue;
					byte b = bitmap[x, y + (HEIGHT * 3)];
					_tiles[x, y].RailRoad = (b & 0x01) > 0;
				}
				
				// Remove huts
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					if (!_tiles[x, y].Hut) continue;
					byte b = bitmap[x + (WIDTH * 2), y];
					_tiles[x, y].Hut = (b == 0);
				}
			}
			
			ComputeFreshwaterLakes();
			Ready = true;
			Log("Map: Ready");
		}

		public ushort SaveMap(string filename)
		{
			Log($"Map: Saving {filename} - Random seed: {_terrainMasterWord}");

			using (Bytemap bitmap = Resources["SP299"].Bitmap)
			{
				// Save terrainlayer
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					byte b;
					switch (_tiles[x, y].Type)
					{
						case Terrain.Forest: b = 2; break;
						case Terrain.Swamp: b = 3; break;
						case Terrain.Plains: b = 6; break;
						case Terrain.Tundra: b = 7; break;
						case Terrain.River: b = 9; break;
						case Terrain.Grassland1:
						case Terrain.Grassland2: b = 10; break;
						case Terrain.Jungle: b = 11; break;
						case Terrain.Hills: b = 12; break;
						case Terrain.Mountains: b = 13; break;
						case Terrain.Desert: b = 14; break;
						case Terrain.Arctic: b = 15; break;
						default: b = 1; break; // Ocean
					}
					bitmap[x, y] = b;
				}

				// Save improvement layer
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					byte b = 0;
					if (!_tiles[x, y].IsOcean)
					{
						if (_tiles[x, y].City is not null) b |= 0x01;
						if (_tiles[x, y].Irrigation) b |= 0x02;
						if (_tiles[x, y].Mine) b |= 0x04;
						if (_tiles[x, y].Road) b |= 0x08;
						if (_tiles[x, y].Pollution) b |= 0x10;
					}
					bitmap[x, y + (HEIGHT * 2)] = b;
					bitmap[x + (WIDTH * 1), y + (HEIGHT * 2)] = b; // Visibility layer
				}

				// Save improvement layer 2
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					byte b = (!_tiles[x, y].IsOcean && _tiles[x, y].RailRoad) ? (byte)0x01 : (byte)0x00;
					bitmap[x, y + (HEIGHT * 3)] = b;
					bitmap[x + (WIDTH * 1), y + (HEIGHT * 3)] = b; // Visibility layer
				}

				// Save explored layer
				for (int x = 0; x < WIDTH; x++)
				for (int y = 0; y < HEIGHT; y++)
				{
					bitmap[x + (WIDTH * 2), y] = _tiles[x, y].Visited;
				}

				using (Picture picture = new Picture(bitmap, Resources["SP299"].Palette))
				{
					PicFile picFile = new PicFile(picture)
					{
						HasPalette256 = false
					};
					using (BinaryWriter bw = new BinaryWriter(File.Open(filename, FileMode.Create)))
					{
						bw.Write(picFile.GetBytes());
					}
					return (ushort)_terrainMasterWord;
				}
			}
		}
		
		private void LoadMapThread()
		{
			Log("Map: Loading MAP.PIC");
			
			using (Bytemap bitmap = Resources["MAP"].Bitmap)
			{
				LoadMap(bitmap);
			}
			
			CreatePoles();
			PlaceHuts();
			CalculateLandValue();
			
			Ready = true;
			Log("Map: Ready");
		}
		
		// Load a procedurally-built Earth from a simple binary file. Format:
		//   bytes  0..3   ASCII magic "CIVE"
		//   byte   4      version (currently 1)
		//   bytes  5..7   reserved (zero)
		//   bytes  8..11  width  (uint32 little-endian)
		//   bytes 12..15  height (uint32 little-endian)
		//   bytes 16..    width*height terrain codes (0=Ocean, 2=Forest, 3=Swamp,
		//                 6=Plains, 7=Tundra, 9=River, 10=Grassland, 11=Jungle,
		//                 12=Hills, 13=Mountains, 14=Desert, 15=Arctic — same
		//                 byte values as MAP.PIC for consistency with LoadMap).
		// Path defaults to ~/Library/Application Support/CivOne/earth_epic.bin so
		// the offline build_earth_map.py script can land its output where the
		// MapPreview screen looks for it. Sets _width/_height *before* tile
		// construction so the static dimensions match the loaded grid.
		internal bool LoadEarthBin(string path)
		{
			if (!File.Exists(path))
			{
				Log("LoadEarthBin: file not found {0}", path);
				return false;
			}
			byte[] data;
			try { data = File.ReadAllBytes(path); }
			catch (System.Exception e) { Log("LoadEarthBin: read failed {0}", e.Message); return false; }

			if (data.Length < 16 || data[0] != (byte)'C' || data[1] != (byte)'I' || data[2] != (byte)'V' || data[3] != (byte)'E')
			{
				Log("LoadEarthBin: bad magic header");
				return false;
			}
			int w = data[8] | (data[9] << 8) | (data[10] << 16) | (data[11] << 24);
			int h = data[12] | (data[13] << 8) | (data[14] << 16) | (data[15] << 24);
			if (w <= 0 || h <= 0 || data.Length < 16 + w * h)
			{
				Log("LoadEarthBin: dimensions ({0}x{1}) inconsistent with payload {2}", w, h, data.Length);
				return false;
			}

			_width = w;
			_height = h;
			_tiles = new ITile[w, h];

			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				bool special = TileIsSpecial(x, y);
				ITile tile;
				switch (data[16 + y * w + x])
				{
					case 2:  tile = new Forest(x, y, special); break;
					case 3:  tile = new Swamp(x, y, special); break;
					case 6:  tile = new Plains(x, y, special); break;
					case 7:  tile = new Tundra(x, y, special); break;
					case 9:  tile = new River(x, y); break;
					case 10: tile = new Grassland(x, y); break;
					case 11: tile = new Jungle(x, y, special); break;
					case 12: tile = new Hills(x, y, special); break;
					case 13: tile = new Mountains(x, y, special); break;
					case 14: tile = new Desert(x, y, special); break;
					case 15: tile = new Arctic(x, y, special); break;
					default: tile = new Ocean(x, y, special); break;
				}
				_tiles[x, y] = tile;
			}

			// Mirror LoadMapThread's post-pass so a loaded Earth behaves like an
			// engine-generated one: poles enforced, freshwater lakes detected,
			// continent labels computed (used by AI strategy), huts placed.
			CreatePoles();
			ComputeFreshwaterLakes();
			EnsureFreshwaterReachability();
			EnsureMaritimeFreshwater();
			CalculateContinentSize();
			PlaceHuts();
			CalculateLandValue();

			Ready = true;
			Log("LoadEarthBin: ready ({0}x{1})", w, h);
			return true;
		}

		// Resolved path for the procedural-Earth binary. Search order:
		//   1. User data directory (~/Library/Application Support/CivOne/data/ on macOS)
		//      — where design/build_earth_map.py writes by default, so users tinkering
		//      with sea level / rivers automatically override the bundled copy.
		//   2. <executable_dir>/resources/earth_epic.bin — for installed builds that
		//      ship the resource alongside the binary.
		//   3. <executable_dir>/../../../../../resources/earth_epic.bin — for source
		//      builds running from runtime/sdl/bin/{Debug,Release}/net10.0/ (5 levels
		//      up to the repo root, then into resources/).
		// If nothing exists, returns the user-dir path so the missing-file error message
		// points the user at the location they can write to.
		public static string EarthEpicPath
		{
			get
			{
				string userPath = Path.Combine(Settings.Instance.DataDirectory, "earth_epic.bin");
				if (File.Exists(userPath)) return userPath;
				string execDir = System.AppContext.BaseDirectory;
				string[] candidates = {
					Path.Combine(execDir, "resources", "earth_epic.bin"),
					Path.GetFullPath(Path.Combine(execDir, "..", "..", "..", "..", "..", "resources", "earth_epic.bin")),
				};
				foreach (string c in candidates)
					if (File.Exists(c)) return c;
				return userPath;
			}
		}

		// EARTH (EPIC) load path used by the new-game menu. Unlike LoadMap (which loads
		// the original 80×50 MAP.PIC with fixed civ starting coordinates), this loads
		// a 320×200 procedural Earth and lets the engine's normal new-game placement
		// pick start positions — the civilization records don't have Epic-scale start
		// coordinates anyway. Synchronous; the file is tiny (~64KB) and the load
		// finishes well within a frame.
		public bool LoadEarthEpic()
		{
			if (Ready || _tiles is not null)
			{
				Log("ERROR: Map is already load{0}/generat{0}", (Ready ? "ed" : "ing"));
				return false;
			}

			_landMass = -1;
			_temperature = -1;
			_climate = -1;
			_age = -1;
			// Honour the per-civ StartX/StartY records. They're calibrated for the
			// 80×50 MAP.PIC classic Earth, but Game.NewGame.cs:AddStartingUnits scales
			// them by Map.WIDTH/80 (so 4× on Epic) and falls back to a spiral search
			// for habitable land when the scaled tile lands in ocean or mountain. Net
			// effect on Epic Earth: civs spawn approximately where the historical 80×50
			// map placed them — Russia in Russia, Mali in West Africa, etc.
			FixedStartPositions = true;

			return LoadEarthBin(EarthEpicPath);
		}

		public void LoadMap()
		{
			if (Ready || _tiles is not null)
			{
				Log("ERROR: Map is already load{0}/generat{0}", (Ready ? "ed" : "ing"));
				return;
			}

			_landMass = -1;
			_temperature = -1;
			_climate = -1;
			_age = -1;
			FixedStartPositions = true;

			Task.Run(() => LoadMapThread());
		}
	}
}