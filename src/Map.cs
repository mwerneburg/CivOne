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
		private ITile[,] _tiles = null!;
		private bool[,] _freshwater = null!;

		// Flood-fill every connected ocean region.  Any region whose tile count is
		// at or below LAKE_MAX is a freshwater lake — too small to be the open sea.
		// Swamp tiles are also freshwater (wetlands).  This size-based approach is
		// robust against the polar rows being Arctic in a loaded save, and against
		// bays that happen to have a thin channel to the global ocean.
		internal void ComputeFreshwaterLakes()
		{
			_freshwater = new bool[WIDTH, HEIGHT];

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

			// The largest connected ocean region is the main ocean; all smaller
			// regions are enclosed inland lakes regardless of their absolute size.
			int mainOceanId   = 0;
			int mainOceanSize = 0;
			for (int id = 1; id < sizes.Count; id++)
			{
				if (sizes[id] > mainOceanSize) { mainOceanSize = sizes[id]; mainOceanId = id; }
			}

			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				int id = label[x, y];
				if (id > 0 && id != mainOceanId)
					_freshwater[x, y] = true;
				else if (_tiles[x, y].Type == Terrain.Swamp)
					_freshwater[x, y] = true;
			}
		}

		// Returns true if (x,y) is a freshwater source: enclosed lake or swamp.
		internal bool IsFreshwaterAt(int x, int y) => _freshwater is not null && _freshwater[x, y];

		// Freshwater reachability pass: ensures no settleable land tile is more than
		// `maxDryDistance` tiles (4-connected) from a water source. Sources: Ocean,
		// River, lakes/swamps (via _freshwater). For each cluster of land too far
		// from water, plant a single River tile at the worst-affected location and
		// re-update local distances. Repeats until every land tile is in range.
		//
		// Targets the playtest failure mode where large interior regions (e.g.
		// Patagonia, central Australia on Epic Earth) have no water source and a
		// Settler must walk dozens of turns to find a foundable site.
		//
		// Mountains and Arctic are excluded — those tiles aren't candidates for
		// settlement anyway, so leaving them dry doesn't hurt playability.
		internal void EnsureFreshwaterReachability(int maxDryDistance = 6)
		{
			int[,] dist = new int[WIDTH, HEIGHT];
			var queue = new Queue<(int x, int y)>();
			int[] ddx = { 0, 0, -1, 1 };
			int[] ddy = { -1, 1, 0, 0 };

			bool IsWater(int x, int y)
				=> _tiles[x, y].IsOcean || _tiles[x, y].Type == Terrain.River || _freshwater[x, y];

			bool IsSettleableLand(int x, int y)
			{
				Terrain t = _tiles[x, y].Type;
				return !_tiles[x, y].IsOcean
					&& t != Terrain.River && !_freshwater[x, y]
					&& t != Terrain.Mountains && t != Terrain.Arctic;
			}

			// Seed multi-source BFS from every water tile.
			for (int y = 0; y < HEIGHT; y++)
			for (int x = 0; x < WIDTH; x++)
			{
				if (IsWater(x, y)) { dist[x, y] = 0; queue.Enqueue((x, y)); }
				else dist[x, y] = int.MaxValue;
			}
			while (queue.Count > 0)
			{
				var (cx, cy) = queue.Dequeue();
				for (int d = 0; d < 4; d++)
				{
					int nx = (cx + ddx[d] + WIDTH) % WIDTH;
					int ny = cy + ddy[d];
					if (ny < 0 || ny >= HEIGHT) continue;
					if (dist[nx, ny] <= dist[cx, cy] + 1) continue;
					dist[nx, ny] = dist[cx, cy] + 1;
					queue.Enqueue((nx, ny));
				}
			}

			// Greedy: plant a river at the worst dry tile and carve a short stub
			// trending toward water, until no dry tile remains out of range.
			// Stub length is capped (maxStubLength) so a single oasis doesn't carve
			// half a continent into river; the stub stops early if it reaches any
			// existing water tile (joining a real river/coast looks natural).
			const int maxStubLength = 3;
			int planted = 0, oasisCount = 0, safetyCap = 500;
			while (oasisCount < safetyCap)
			{
				int wx = -1, wy = -1, wd = maxDryDistance;
				for (int y = 0; y < HEIGHT; y++)
				for (int x = 0; x < WIDTH; x++)
				{
					if (!IsSettleableLand(x, y)) continue;
					if (dist[x, y] > wd) { wd = dist[x, y]; wx = x; wy = y; }
				}
				if (wx < 0) break;

				var chain = new List<(int x, int y)> { (wx, wy) };
				_tiles[wx, wy] = new River(wx, wy);
				int curX = wx, curY = wy;
				for (int step = 0; step < maxStubLength; step++)
				{
					int bestNx = -1, bestNy = -1, bestDist = int.MaxValue;
					bool reachedWater = false;
					for (int d = 0; d < 4; d++)
					{
						int nx = (curX + ddx[d] + WIDTH) % WIDTH;
						int ny = curY + ddy[d];
						if (ny < 0 || ny >= HEIGHT) continue;
						if (IsWater(nx, ny)) { reachedWater = true; break; }
						Terrain nt = _tiles[nx, ny].Type;
						if (nt == Terrain.Mountains || nt == Terrain.Arctic) continue;
						if (dist[nx, ny] < bestDist) { bestDist = dist[nx, ny]; bestNx = nx; bestNy = ny; }
					}
					if (reachedWater || bestNx < 0) break;
					_tiles[bestNx, bestNy] = new River(bestNx, bestNy);
					chain.Add((bestNx, bestNy));
					curX = bestNx; curY = bestNy;
				}
				planted += chain.Count;
				oasisCount++;

				// Local BFS from every planted tile to refresh neighbour distances.
				var local = new Queue<(int, int)>();
				foreach (var (cx, cy) in chain) { dist[cx, cy] = 0; local.Enqueue((cx, cy)); }
				while (local.Count > 0)
				{
					var (cx, cy) = local.Dequeue();
					for (int d = 0; d < 4; d++)
					{
						int nx = (cx + ddx[d] + WIDTH) % WIDTH;
						int ny = cy + ddy[d];
						if (ny < 0 || ny >= HEIGHT) continue;
						if (dist[nx, ny] <= dist[cx, cy] + 1) continue;
						dist[nx, ny] = dist[cx, cy] + 1;
						local.Enqueue((nx, ny));
					}
				}
			}
			Log("EnsureFreshwaterReachability: {0} oases ({1} river tiles total)", oasisCount, planted);
		}

		// Maritime fresh water: narrow, ocean-girt land — islands like New Zealand,
		// Madagascar, and Japan, the Indonesian/Philippine archipelagos, and thin
		// peninsulas like Kamchatka — is always within a few tiles of the coast, so
		// EnsureFreshwaterReachability (which counts salt ocean as "water") never
		// plants on it. But irrigation needs FRESH water — river, lake, or swamp,
		// not sea (see Settlers.BuildIrrigation) — so those landmasses end up
		// unfarmable. This pass guarantees that settleable land within coastDistance
		// of the ocean is also within maxDryDistance of fresh water, planting small
		// river oases where it isn't. Deep continental interiors, far from any coast,
		// keep their natural aridity. Idempotent: a map already in range gets none.
		internal void EnsureMaritimeFreshwater(int maxDryDistance = 6, int coastDistance = 6)
		{
			int[] ddx = { 0, 0, -1, 1 };
			int[] ddy = { -1, 1, 0, 0 };

			bool IsFresh(int x, int y)
				=> _tiles[x, y].Type == Terrain.River || _freshwater[x, y];

			bool IsSettleableLand(int x, int y)
			{
				Terrain t = _tiles[x, y].Type;
				return !_tiles[x, y].IsOcean
					&& t != Terrain.River && !_freshwater[x, y]
					&& t != Terrain.Mountains && t != Terrain.Arctic;
			}

			// Multi-source BFS distance fields. Both seed only their source tiles, so
			// only finite-distance tiles are ever dequeued — no MaxValue arithmetic.
			int[,] BfsDistance(System.Func<int, int, bool> isSource)
			{
				int[,] dist = new int[WIDTH, HEIGHT];
				var q = new Queue<(int x, int y)>();
				for (int y = 0; y < HEIGHT; y++)
				for (int x = 0; x < WIDTH; x++)
				{
					if (isSource(x, y)) { dist[x, y] = 0; q.Enqueue((x, y)); }
					else dist[x, y] = int.MaxValue;
				}
				while (q.Count > 0)
				{
					var (cx, cy) = q.Dequeue();
					for (int d = 0; d < 4; d++)
					{
						int nx = (cx + ddx[d] + WIDTH) % WIDTH;
						int ny = cy + ddy[d];
						if (ny < 0 || ny >= HEIGHT) continue;
						if (dist[nx, ny] <= dist[cx, cy] + 1) continue;
						dist[nx, ny] = dist[cx, cy] + 1;
						q.Enqueue((nx, ny));
					}
				}
				return dist;
			}

			int[,] coast = BfsDistance((x, y) => _tiles[x, y].IsOcean);
			int[,] fresh = BfsDistance(IsFresh);

			// Greedy: plant at the maritime land tile farthest from fresh water,
			// biased toward the landmass interior (highest coast distance) so the
			// water lands inland rather than on the shoreline. A short stub trends
			// toward existing fresh water if any is in reach (joins a real river);
			// on a fresh-less island it is a lone source. Refresh distances, repeat.
			const int maxStubLength = 3;
			int planted = 0, oasisCount = 0, safetyCap = 500;
			while (oasisCount < safetyCap)
			{
				int wx = -1, wy = -1, wFresh = maxDryDistance, wCoast = -1;
				for (int y = 0; y < HEIGHT; y++)
				for (int x = 0; x < WIDTH; x++)
				{
					if (!IsSettleableLand(x, y) || coast[x, y] > coastDistance) continue;
					int f = fresh[x, y];
					if (f <= maxDryDistance) continue; // already in range of fresh water
					if (f > wFresh || (f == wFresh && coast[x, y] > wCoast))
					{
						wFresh = f; wCoast = coast[x, y]; wx = x; wy = y;
					}
				}
				if (wx < 0) break;

				var chain = new List<(int x, int y)> { (wx, wy) };
				_tiles[wx, wy] = new River(wx, wy);
				int curX = wx, curY = wy;
				for (int step = 0; step < maxStubLength; step++)
				{
					int bestNx = -1, bestNy = -1, bestFresh = int.MaxValue;
					bool reachedFresh = false;
					for (int d = 0; d < 4; d++)
					{
						int nx = (curX + ddx[d] + WIDTH) % WIDTH;
						int ny = curY + ddy[d];
						if (ny < 0 || ny >= HEIGHT) continue;
						if (IsFresh(nx, ny)) { reachedFresh = true; break; }
						Terrain nt = _tiles[nx, ny].Type;
						if (_tiles[nx, ny].IsOcean || nt == Terrain.Mountains || nt == Terrain.Arctic) continue;
						if (fresh[nx, ny] < bestFresh) { bestFresh = fresh[nx, ny]; bestNx = nx; bestNy = ny; }
					}
					if (reachedFresh || bestNx < 0) break;
					_tiles[bestNx, bestNy] = new River(bestNx, bestNy);
					chain.Add((bestNx, bestNy));
					curX = bestNx; curY = bestNy;
				}
				planted += chain.Count;
				oasisCount++;

				var local = new Queue<(int, int)>();
				foreach (var (cx, cy) in chain) { fresh[cx, cy] = 0; local.Enqueue((cx, cy)); }
				while (local.Count > 0)
				{
					var (cx, cy) = local.Dequeue();
					for (int d = 0; d < 4; d++)
					{
						int nx = (cx + ddx[d] + WIDTH) % WIDTH;
						int ny = cy + ddy[d];
						if (ny < 0 || ny >= HEIGHT) continue;
						if (fresh[nx, ny] <= fresh[cx, cy] + 1) continue;
						fresh[nx, ny] = fresh[cx, cy] + 1;
						local.Enqueue((nx, ny));
					}
				}
			}
			Log("EnsureMaritimeFreshwater: {0} oases ({1} river tiles total)", oasisCount, planted);
		}

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

		// ContinentId space: 0 = unset (a freshly constructed tile before assignment),
		// 1..254 = a real landmass numbered by descending size, 255 = "misc".
		//
		// This was 1..14 with 15 as misc — Civ 1's fixed 80x50 map has nothing like fourteen
		// meaningful landmasses, so the cap never bound. On 320x200 it left dozens of islands
		// unnumbered, and unnumbered means invisible to anything that reasons about land
		// reachability: AI land-attack targeting skipped those cities entirely, and a Diplomat
		// or Caravan standing on one could never pick a target at all, its own island included.
		// Observed as an unmolested size-11 city on an island while the mainland was overrun.
		//
		// ContinentId is a byte and is NOT persisted (recomputed on load), so widening the
		// range costs nothing in save compatibility.
		public const byte MiscContinent = 255;

		// A landmass the game will reason about. Excludes both the unset default and misc,
		// which is the distinction every call site needs and several used to spell by hand.
		public static bool NamedContinent(byte id) => id != 0 && id != MiscContinent;

		// Water counterpart of MiscContinent / NamedContinent. A ship's reachability
		// question ("can I sail there at all") is the same question a land unit answers
		// for free from ContinentId, and answering it with A* costs a full flood of the
		// ocean when the answer is no — measured at 29ms against 28us for a successful
		// path. These give sea units the same one-byte oracle.
		public const byte MiscOcean = 255;
		public static bool NamedOcean(byte id) => id != 0 && id != MiscOcean;

		// Set when a tile flips between land and ocean. ContinentId is computed once
		// by CalculateContinentSize and then stored on the tile — and ChangeTileType
		// builds a NEW tile object without carrying it over, so any land/ocean flip
		// leaves the map's continent topology stale.
		//
		// That matters because Common.GotoStep short-circuits land pathfinding when
		// source and destination are on different continents. After global warming
		// drowns a land bridge, both fragments still carry the ORIGINAL id, the
		// short-circuit does not fire, and A* explores every reachable tile before
		// failing — the most expensive outcome there is, repeated every turn for
		// every unit trying to cross. Loading a save hid this, because Game.Cos
		// recomputes continents on load; the cost crept back as warming continued.
		private bool _continentsDirty;

		internal void RecalculateContinentsIfDirty()
		{
			if (!_continentsDirty) return;
			_continentsDirty = false;
			CalculateContinentSize();

			// The freshwater map has to move with the coastline. Global warming does two
			// things to a drowning world: it converts wet tiles to Swamp and it floods
			// low-lying land, and both of those CREATE irrigation sources — swamps are
			// freshwater by definition here, and any enclosed body of water that is not the
			// main ocean is a lake. But _freshwater was computed once at map generation and
			// only ever rebuilt on load, so every lake warming carved out stayed unregistered
			// and no settler could irrigate beside it. The same save reloaded would suddenly
			// allow it, which is the giveaway.
			//
			// Cheap enough to ride along here: this method already no-ops unless land and
			// ocean actually swapped somewhere, which is exactly when the lakes change.
			ComputeFreshwaterLakes();
		}

		public void ChangeTileType(int x, int y, Terrain type)
		{
			bool wasOcean = _tiles[x, y].IsOcean;
			bool special = TileIsSpecial(x, y);
			bool road = _tiles[x, y].Road;
			bool railRoad = _tiles[x, y].RailRoad;
			byte continentId = _tiles[x, y].ContinentId;
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
			// Land/ocean flip invalidates continent topology — see _continentsDirty.
			if (_tiles[x, y].IsOcean != wasOcean) { _continentsDirty = true; return; }
			// Otherwise the topology is unchanged and the id carries over. This is not
			// cosmetic: the replacement tile object would otherwise default to continent 0,
			// and AI.LandReachable compares continent ids — so every tile a settler CONVERTED
			// (drained swamp, cleared jungle or forest) and every tile global warming retyped
			// became permanently unroutable, in a countryside that grows more converted with
			// every century.
			_tiles[x, y].ContinentId = continentId;
		}
		
		private int ModGrid(int x, int y) => (x % 4) * 4 + (y % 4);
		
		private bool TileIsSpecial(int x, int y)
		{
			if (y < 2 || y > (HEIGHT - 3)) return false;
			return ModGrid(x, y) == ((x / 4) * 13 + (y / 4) * 11 + _terrainMasterWord) % 16;
		}
		
		public IEnumerable<ITile> ContinentTiles(int continentId) => AllTiles().Where(t => t.ContinentId == continentId);
		
		// Walk the city list, not the map. Same set — a city is on the continent its own
		// tile is on — but this is O(cities) instead of O(map tiles). The old form scanned
		// all 64,000 tiles of a 320x200 board, and ComputeCitizens calls it twice per city
		// per turn (J.S. Bach and Michelangelo), which was ~57M tile visits a turn at 443
		// cities and by far the largest cost in the game. Left lazy: every caller uses
		// Any(), so the scan now short-circuits as well.
		public IEnumerable<City> ContentCities(int continentId) =>
			Game.Instance.GetCities().Where(c => c.Tile is not null && c.Tile.ContinentId == continentId);
		
		public ITile this[int x, int y]
		{
			get
			{
				if (y < 0 || y >= HEIGHT) return null!;
				
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
		
		private static Map _instance = null!;
		public static Map Instance
		{
			get
			{
				if (_instance is null)
					_instance = new Map();
				return _instance;
			}
		}

		// Preview-tool overrides for world-generation knobs. Each <=0 means "use the
		// existing formula"; the MapPreview screen reads its config and sets these before
		// calling Generate(). Never set during normal gameplay.
		internal int PreviewNumSeeds         = 0;
		internal int PreviewSeedSeparation   = 0;  // min tiles between distinct continent labels during growth
		internal int PreviewRiverTarget      = 0;
		internal int PreviewRiverSeparation  = 0;  // exclusion radius between river mouths
		internal int PreviewRiverMinLength   = 0;  // accept rivers at least this long

		// Tear down the singleton so MapPreview can re-roll. Map.Generate refuses to run
		// twice on the same instance (see Map.Generate.cs:989); resetting forces a fresh one.
		internal static void ResetForPreview()
		{
			_instance = null!;
		}

		private Map()
		{
			// Hut placement (TileHasHut at Map.Generate.cs:140) takes _terrainMasterWord
			// modulo 50 — so historically only 16 of the 50 possible hut patterns ever
			// surfaced because Next(16) caps the input range. Widen so every game gets
			// independent hut layouts. Earth (Classic) still overrides via LoadMap's
			// randomSeed parameter for save/replay determinism.
			_terrainMasterWord = Common.Random.Next(int.MaxValue);
			Ready = false;

			Log("Map instance created");
		}
	}
}