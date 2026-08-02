// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

// Common.cs
//
// Whoever stripped all the comments from this thing clearly hated us. 8)
//
// Regarding the rendering faiclities in this file:
//  - not about rendering primitives
//  - is about screen lifetime and screen stacking

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Screens;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne
{
	internal class Common
	{
		private static Resources Resources => Resources.Instance;
		private static IRuntime Runtime => RuntimeHandler.Runtime;
		private static Settings Settings => Settings.Instance;
		private static void Log(string text, params object[] parameters) => RuntimeHandler.Runtime.Log(text, parameters);

		public static Random Random = new Random((int)DateTime.Now.Ticks);
		
		public static IAdvance[] Advances = Reflect.GetAdvances().ToArray();
		public static IBuilding[] Buildings = Reflect.GetBuildings().ToArray();
		public static IWonder[] Wonders = Reflect.GetWonders().ToArray();
		// Lazily cached like Advances/Buildings/Wonders above; the old per-access
		// Reflect.GetCivilizations() re-instantiated every civilization each call.
		// Safe to share now that Player no longer mutates Leader.Name on its instance.
		private static ICivilization[] _civilizations = null!;
		public static ICivilization[] Civilizations => _civilizations ??= Reflect.GetCivilizations().ToArray();
		// Slots 0–7: original civs (0=Barbarians). Slots 8–15: reserved for narrative factions (Olvir=8, Others=9).
		// Slots 16–19: extra capacity for max-competition games (NewGame caps at 17 civs; with barbarians + Olvir that's 19 slots).
		//
		// City banners are drawn QUARTERED (Icons.City) using ColourLight as the primary and
		// BannerSecondary as the accent, so the per-slot *pair* is the identity — every pair
		// below is unique even where a primary colour repeats (only ~9 bright palette colours
		// exist for 20 slots). All primaries are now bright/visible (palette indices 7..17), so
		// no civ shows as a near-black sliver on the minimap or in reports the way the old
		// table did (e.g. slot 3/16 were both near-black; slot 0 and 9 were identical 16/4).
		// ColourDark is left as the original dark shades — the Intelligence Report uses it as a
		// button background with ColourLight text on top, which needs the dark contrast.
		public static byte[] ColourLight     = [16, 11, 13, 14, 17, 15, 16, 11, 13, 14, 17, 15, 16, 11, 13, 14, 17, 15, 12,  7];
		// Slot 0 (Barbarians) is deliberately red/red: it's a fixed, single slot with no other
		// civ to disambiguate it from, so it stays solid red (classic Civ) rather than red/white.
		public static byte[] BannerSecondary = [16, 17, 14, 13, 16, 11, 17, 15, 16, 15, 13, 14, 13, 14, 17, 16, 15, 16, 17, 16];
		public static byte[] ColourDark      = [ 4,  7,  2,  1, 10,  3,  4,  8,  5,  4,  3,  4,  4,  3,  5,  3,  1, 10,  3,  4];
		
		internal static IEnumerable<string> AllCityNames => Civilizations.Select(x => x.CityNames).SelectMany(x => x);

		private static List<IScreen> _screens = new();
		internal static IScreen[] Screens => _screens.ToArray();

		internal static bool HasAttribute<T>(object checkObject) where T : Attribute
		{
			if (checkObject is null)
				return false;
			return Attribute.IsDefined(checkObject.GetType(), typeof(T));
		}

		public static IScreen TopScreen
		{
			get
			{
				if (_screens.Any(x => HasAttribute<Modal>(x)))
					return _screens.Last(x => HasAttribute<Modal>(x));
				return _screens.LastOrDefault();
			}
		}

		public static MouseCursor MouseCursor
		{
			get
			{
				if (TopScreen is null)
					return MouseCursor.None;
				return TopScreen.Cursor;
			}
		}

		public static Palette DefaultPalette
		{
			get
			{
				GamePlay gamePlay = GamePlay;
				if (gamePlay is not null)
					return gamePlay.MainPalette.Copy();
				return Resources["SP257"].Palette.Copy();
			}
		}

		public static GamePlay GamePlay => (GamePlay)_screens.FirstOrDefault(x => x is GamePlay);

		public static Palette GamePlayPalette => GamePlay.Palette.Copy();

		internal static void SetRandomSeedFromName(string name)
		{
			short number = 0;
			foreach (byte charByte in name)
			{
				number += charByte;
			}
			SetRandomSeed(number);
		}
		
		internal static void SetRandomSeed(short seed) => Random = new Random(seed);
		
		internal static void AddScreen(IScreen screen) => _screens.Add(screen);
		
		internal static void DestroyScreen(IScreen screen)
		{
			screen.Dispose();
			_screens.Remove(screen);
		}
		
		internal static bool HasScreenType<T>() where T : IScreen => _screens.Any(x => x is T);
		
		internal static string CaptureFilename
		{
			get
			{
				for (int i = 1; i < 99999; i++)
				{
					string filename = Path.Combine(Settings.Instance.CaptureDirectory, $"capture{i:00000}.gif");
					if (File.Exists(filename)) continue;
					return filename;
				}
				
				Log("Error: Capture folder is full.");
				return null!;
			}
		}
		
		private static bool _reloadSettings;
		internal static bool ReloadSettings
		{
			get
			{
				if (_reloadSettings)
				{
					_reloadSettings = false;
					return true;
				}
				return false;
			}
			set
			{
				_reloadSettings = value;
			}
		}

		internal static string NumberSeperator(int number)
		{
			string input = number.ToString();
			input = input.PadLeft(3 - (input.Length % 3) + input.Length, '0');
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < input.Length; i++)
			{
				if (sb.Length > 0 && i % 3 == 0) sb.Append(',');
				sb.Append(input[i]);
			}
			return sb.ToString().TrimStart(['0', ',']);
		}

		public static ushort YearToTurn(int year)
		{
			if (year < -4000) return 0;
			if (year < 1000) return (ushort)Math.Floor(((double)year + 4000) / 20);
			if (year < 1500) return (ushort)Math.Floor(((double)year + 1500) / 10);
			if (year < 1750) return (ushort)Math.Floor(((double)year) / 5);
			if (year < 1850) return (ushort)Math.Floor(((double)year - 1050) / 2);
			return (ushort)(year - 1450);
		}
		
		public static int TurnToYear(ushort turn)
		{
			if (turn < 200) return -(200 - turn) * 20;
			else if (turn == 200) return 1;
			else if (turn < 250) return (turn - 200) * 20;
			else if (turn < 300) return ((turn - 250) * 10) + 1000;
			else if (turn < 350) return ((turn - 300) * 5) + 1500;
			else if (turn < 400) return ((turn - 350) * 2) + 1750;
			return (turn - 400) + 1850;
		}
		
		public static string YearString(ushort turn, bool zeroAd = false)
		{
			int year = TurnToYear(turn);
			if (zeroAd && year == 1) year = 0;
			if (year < 0)
				return $"{-year} BC";
			return $"{year} AD";
		}

		public static string DifficultyName(int difficuly)
		{
			switch (difficuly)
			{
				case 1: return "Lord";
				case 2: return "Prince";
				case 3: return "King";
				case 4: return "Emperor";
				case 5: return "Deity";
				default: return "Chief";
			}
		}

		internal static int CitizenGroup(Citizen citizen)
		{
			int output = (int)citizen;
			output -= (output % 2);
			output /= 2;
			if (output > 3) output = 3;
			return output;
		}
		
		public static bool InCityRange(int x1, int y1, int x2, int y2) => new Rectangle(x2 - 2, y2 - 2, 5, 5).IntersectsWith(new Rectangle(x1, y1, 1, 1));
		
		public static int DistanceToTile(int x1, int y1, int x2, int y2) => Math.Max(Math.Min(Math.Abs(x2 - x1), Map.WIDTH - Math.Abs(x2 - x1)), Math.Abs(y2 - y1));

		// A* pathfinder for GoTo orders. Returns the next tile to move into, or null if unreachable.
		// Cost units: railroad=1, road=3, terrain=Movement*9 (max 18 for hills/forest).
		public static ITile? GotoStep(IUnit unit) => GotoStep(unit, unit.Goto.X, unit.Goto.Y);

		// A committed route, kept between calls so walking it costs one search instead of one
		// per step. The 2026-08-03 run measured 63,632 Diplomat moves at 28 ms each with only
		// 1.7% of that in target selection: the cost was re-planning the whole journey every
		// turn and throwing all but the first step away.
		//
		// Keyed weakly by unit, so a disbanded unit's plan is collected with it and a loaded
		// save starts empty (new unit objects) rather than inheriting a stale route.
		private sealed class PathPlan
		{
			public int GoalX, GoalY;
			public int StartX, StartY;
			public int[] Steps = System.Array.Empty<int>();   // encoded, start excluded, goal included
			public int At = -1;                                // index of the unit's current tile; -1 = at Start
		}
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IUnit, PathPlan> _plans = new();

		public static ITile? GotoStep(IUnit unit, int gx, int gy)
		{
			long __p = TurnMetrics.Now;
			bool __found = false;
			try
			{
				ITile? reused = CachedStep(unit, gx, gy);
				if (reused is not null)
				{
					__found = true;
					TurnMetrics.AddBucket("path:Hit", __p);
					return reused;
				}
				long __m = TurnMetrics.Now;
				ITile? r = GotoStepInner(unit, gx, gy);
				TurnMetrics.AddBucket("path:Miss", __m);
				__found = r is not null;
				return r;
			}
			finally { TurnMetrics.AddPathfind(__p, __found); }
		}

		// Returns the next step off a still-valid plan, or null to force a fresh search.
		// Null is always safe here: the caller falls through to GotoStepInner, which lays a
		// new plan. Every doubt is resolved by returning null.
		private static ITile? CachedStep(IUnit unit, int gx, int gy)
		{
			if (!_plans.TryGetValue(unit, out PathPlan plan)) return null;
			if (plan.GoalX != gx || plan.GoalY != gy) return null;

			int w = Map.WIDTH;
			int here = unit.Y * w + unit.X;
			int expected = plan.At < 0 ? plan.StartY * w + plan.StartX : plan.Steps[plan.At];

			if (here != expected)
			{
				// The unit took the step we handed it (the normal case, and the one that
				// repeats within a turn for a unit with more than one move point).
				if (plan.At + 1 < plan.Steps.Length && here == plan.Steps[plan.At + 1]) plan.At++;
				// Anywhere else means something moved the unit off plan — boarded a transport,
				// bounced off a failed attack, teleported by a hut. Re-plan.
				else return null;
			}

			int nextIndex = plan.At + 1;
			if (nextIndex >= plan.Steps.Length) return null;   // already at the goal
			int next = plan.Steps[nextIndex];
			int nx = next % w, ny = next / w;
			ITile tile = Map.Instance[nx, ny];
			if (tile is null) return null;

			// The one staleness that matters. Terrain the planner costed can change (a road
			// gets built, making some other route cheaper) but that only costs optimality, and
			// the plan expires on arrival anyway. A unit moving onto our next tile is different:
			// a non-combat unit refuses to enter it, so the step is one the unit will not take.
			// Mirrors the `blocked` test in GotoStepInner, goal-tile exemption included.
			if (!(nx == gx && ny == gy) && tile.City is null && StepBlocked(unit, tile)) return null;

			return tile;
		}

		// The `blocked` predicate of GotoStepInner, for a single tile. Kept beside the two
		// clauses it mirrors — if the blocking rule there changes, this must change with it.
		private static bool StepBlocked(IUnit unit, ITile tile)
		{
			bool nonCombat = unit is Diplomat || unit is Caravan
			              || unit is Settlers || unit is HydroEngineer;
			Player moverPlayer = Game.Instance.GetPlayer(unit.Owner);
			foreach (IUnit u in tile.Units)
			{
				if (u is null || u.Owner == unit.Owner) continue;
				if (nonCombat) return true;
				if (u.Owner != 0 && !moverPlayer.IsAtWar(Game.Instance.GetPlayer(u.Owner))) return true;
			}
			return false;
		}

		private static ITile? GotoStepInner(IUnit unit, int gx, int gy)
		{
			int sx = unit.X, sy = unit.Y;
			if (sx == gx && sy == gy) return null;

			var map = Map.Instance;
			int w = Map.WIDTH, h = Map.HEIGHT;

			// Continent feasibility check for land units: if source and destination tiles
			// belong to different named continents (ID 1–14), there's no land path between
			// them and A* would just exhaust the open set. Short-circuit to avoid an
			// expensive futile search. ContinentId 15 ("misc") is conservatively allowed —
			// it covers tiny islands and polar bands where the check would be unreliable.
			if (unit.Class == UnitClass.Land)
			{
				ITile src = map[sx, sy];
				ITile dst = map[gx, gy];
				if (src is not null && dst is not null
				    && src.ContinentId >= 1 && src.ContinentId <= 14
				    && dst.ContinentId >= 1 && dst.ContinentId <= 14
				    && src.ContinentId != dst.ContinentId)
					return null;
			}

			var gScore = new Dictionary<int, int>();
			var cameFrom = new Dictionary<int, int>();
			// open set as a binary min-heap keyed on (f, euclidSq-to-goal). Primary: lowest
			// f; tiebreak: lowest squared Euclidean distance to goal (favours the straight
			// path) — same ordering the old linear min-scan used. netstandard2.0 has no
			// System.Collections.Generic.PriorityQueue, so this is a compact array-backed
			// heap (HeapPush/HeapPop). Each entry carries the g it was queued with, for O(1)
			// lazy staleness detection — replacing the O(n) scan-for-min that made this A*
			// O(n²) on large searches (the ~5s late-game unit-move spike).
			var open = new List<(int f, int euclid, int pos, int g)>();
			bool HeapLess((int f, int euclid, int pos, int g) a, (int f, int euclid, int pos, int g) b)
				=> a.f < b.f || (a.f == b.f && a.euclid < b.euclid);
			void HeapPush((int f, int euclid, int pos, int g) item)
			{
				open.Add(item);
				int i = open.Count - 1;
				while (i > 0)
				{
					int parent = (i - 1) / 2;
					if (!HeapLess(open[i], open[parent])) break;
					(open[i], open[parent]) = (open[parent], open[i]);
					i = parent;
				}
			}
			(int f, int euclid, int pos, int g) HeapPop()
			{
				var top = open[0];
				int last = open.Count - 1;
				open[0] = open[last];
				open.RemoveAt(last);
				int n = open.Count, i = 0;
				while (true)
				{
					int l = 2 * i + 1, r = 2 * i + 2, best = i;
					if (l < n && HeapLess(open[l], open[best])) best = l;
					if (r < n && HeapLess(open[r], open[best])) best = r;
					if (best == i) break;
					(open[i], open[best]) = (open[best], open[i]);
					i = best;
				}
				return top;
			}

			// Scale the heuristic to match actual step costs so A* stays directed.
			// Land units can use railroad (cost=1); sea/air minimum is one ocean step (cost=9).
			int minStepCost = (unit.Class == UnitClass.Land) ? 1 : 9;

			// The mover's player, for the war-aware occupancy check below.
			Player moverPlayer = Game.Instance.GetPlayer(unit.Owner);
			// Units that cannot fight must route AROUND every foreign unit, at war or not.
			// For a combat unit an enemy tile is a target, so it deliberately does not
			// block — but a Diplomat, Caravan, Settlers or Hydro Engineer refuses to
			// enter it (BaseUnit.Confront returns false for exactly these four), so the
			// planner would hand it a path whose next step it will not take. It walks up
			// to the enemy stack and stops there, re-deciding the identical move every
			// turn: the diplomat that never reaches the city it was sent to. The comment
			// at BaseUnit.cs:271 has flagged this gap for a while — this closes it at the
			// planner instead of only guarding at the boundary.
			bool nonCombat = unit is Diplomat || unit is Caravan
			              || unit is Settlers || unit is HydroEngineer;

			// A foreign unit blocks the tile only when we're at PEACE with its owner —
			// a peaceful neighbour's stack you cannot trespass through. At war (and
			// against barbarians, always hostile) an enemy unit's tile is a target to
			// attack into, not a wall to route around, so it does not block.
			bool Blocks(byte owner) => owner != unit.Owner
				&& (nonCombat
				    || (owner != 0 && !moverPlayer.IsAtWar(Game.Instance.GetPlayer(owner))));

			// One of the mover's own cities is a hub on its transport network: it
			// always carries a road, and carries rail once its side has the RailRoad
			// advance. Without this the rail bonus breaks at every city (the City tile
			// has no RailRoad flag set), so A* costs a through-city step as bare
			// terrain and detours around the player's own cities to stay on open rail.
			// Foreign cities grant no bonus — you can't run your trains through them.
			bool OwnCity(ITile t) => t.City is not null && t.City.Owner == unit.Owner;
			bool moverHasRail = Game.Instance.GetPlayer(unit.Owner).HasAdvance<RailRoad>();
			bool RailAt(ITile t) => t.RailRoad || t.TransportTube
				|| (OwnCity(t) && moverHasRail);
			bool RoadAt(ITile t) => t.Road || t.RailRoad || t.TransportTube || OwnCity(t);

			// Unit occupancy as a per-tile bitmask of owners, built once per search.
			// ITile.Units routes to Game.GetUnits(x, y), which scans EVERY unit in the
			// game, sorts the matches and allocates an array. A* consulted it once per
			// neighbour examined plus up to eight more times per tile for ZOC, making
			// each search O(nodes × units). At ~1,700 units that came to ~100ms per
			// path and 80% of the late-game turn. Owner is a byte index, so the whole
			// set of units on a tile collapses to one int.
			var occupancy = new Dictionary<int, int>();
			int seenOwners = 0;
			foreach (IUnit u in Game.Instance.GetUnits())
			{
				if (u is null || u.X < 0 || u.X >= w || u.Y < 0 || u.Y >= h) continue;
				int key = u.Y * w + u.X;
				occupancy.TryGetValue(key, out int mask);
				occupancy[key] = mask | (1 << u.Owner);
				seenOwners |= 1 << u.Owner;
			}
			int selfBit = 1 << unit.Owner;
			// Resolve Blocks() once per owner actually on the map rather than per edge.
			int blockingMask = 0;
			for (int o = 0; o < 32; o++)
				if ((seenOwners & (1 << o)) != 0 && Blocks((byte)o)) blockingMask |= 1 << o;
			int OwnerMask(int x, int y) => occupancy.TryGetValue(y * w + x, out int m) ? m : 0;

			// Zone of control: a tile is under enemy ZOC if any of its eight
			// neighbours holds a foreign unit. Memoised — each tile's status is
			// computed once per search rather than per visiting edge.
			var zocCache = new Dictionary<int, bool>();
			bool InZoc(int x, int y)
			{
				int key = Encode(x, y);
				if (zocCache.TryGetValue(key, out bool cached)) return cached;
				bool result = false;
				for (int ddy = -1; ddy <= 1 && !result; ddy++)
				for (int ddx = -1; ddx <= 1 && !result; ddx++)
				{
					if (ddx == 0 && ddy == 0) continue;
					int ax = (x + ddx + w) % w, ay = y + ddy;
					if (ay < 0 || ay >= h) continue;
					ITile at = map[ax, ay];
					// A garrisoned city projects no ZOC — only units in the open do.
					if (at is not null && at.City is null && (OwnerMask(ax, ay) & ~selfBit) != 0)
						result = true;
				}
				zocCache[key] = result;
				return result;
			}

			int Encode(int x, int y) => y * w + x;
			int EuclidSq(int x1, int y1, int x2, int y2)
			{
				int ddx = Math.Min(Math.Abs(x2 - x1), w - Math.Abs(x2 - x1));
				int ddy = Math.Abs(y2 - y1);
				return ddx * ddx + ddy * ddy;
			}

			int startPos = Encode(sx, sy);
			gScore[startPos] = 0;
			HeapPush((DistanceToTile(sx, sy, gx, gy) * minStepCost, EuclidSq(sx, sy, gx, gy), startPos, 0));

			// Node budget. Land units get a continent short-circuit above, but sea units
			// have no equivalent: a ship ordered to a goal in an ocean basin its own water
			// does not connect to expands every reachable tile before admitting defeat, and
			// on a 320x200 map that is 64000 tiles. Measured on a turn-511 epic save: 71.5
			// seconds of pathfinding in 2146 calls, i.e. essentially all of it in a handful
			// of exhaustive futile searches, and 16 of the last 150 minutes of that game.
			//
			// 20000 is roughly a third of the map and far beyond what a real crossing costs
			// (a directed A* over 200 tiles expands a few thousand), so a genuine path is
			// not affected. Exceeding it returns null, which is the same "no route" answer
			// the search would have reached anyway — just sooner.
			int budget = 20000;

			while (open.Count > 0)
			{
				if (--budget < 0) return null;
				var node = HeapPop();
				int curPos = node.pos;
				// Lazy staleness: a shorter path to curPos was found after this entry was
				// queued, so a better copy is (or was) also in the heap. Skip this one.
				if (node.g > gScore[curPos]) continue;

				int cx = curPos % w, cy = curPos / w;
				if (cx == gx && cy == gy)
				{
					// Reconstruct the whole route, not just its first step, and keep it: the
					// remaining steps are the entire point of the plan cache above.
					var back = new List<int>();
					for (int cur = curPos; cur != startPos; )
					{
						back.Add(cur);
						if (!cameFrom.TryGetValue(cur, out int prev)) break;
						cur = prev;
					}
					back.Reverse();
					_plans.Remove(unit);
					_plans.Add(unit, new PathPlan
					{
						GoalX = gx, GoalY = gy,
						StartX = sx, StartY = sy,
						Steps = back.ToArray(),
						At = -1
					});
					return map[back[0] % w, back[0] / w];
				}

				for (int dy = -1; dy <= 1; dy++)
				for (int dx = -1; dx <= 1; dx++)
				{
					if (dx == 0 && dy == 0) continue;
					int nx = (cx + dx + w) % w;
					int ny = cy + dy;
					if (ny < 0 || ny >= h) continue;

					ITile tile = map[nx, ny];
					if (tile is null) continue;

					bool passable;
					if (unit.Class == UnitClass.Land)
						passable = !tile.IsOcean || tile.City is not null || tile.TransportTube;
					else if (unit.Class == UnitClass.Water)
						passable = tile.IsOcean || tile.City is not null;
					else
						passable = true;

					ITile fromTile = map[cx, cy];

					// Tiles held by a PEACEFUL neighbour's units are blocked — you cannot
					// trespass through them. Enemy (at-war) or barbarian tiles are NOT
					// blocked: at war their units are targets to advance on and attack,
					// which is the whole point of a war. The goal tile is exempt either way.
					bool blocked = tile.City is null
						&& (OwnerMask(nx, ny) & blockingMask) != 0;

					// Zone of control (Civ 1): may not move directly from one tile under
					// enemy ZOC to another tile under enemy ZOC. Exempt: air units, leaving
					// or entering one of the mover's own cities, and stepping onto a tile
					// that already holds a friendly unit. The goal tile is always allowed.
					//
					// Diplomat, Caravan and Explorer ignore ZOC entirely — BaseUnit.MoveTo
					// exempts them (BaseUnit.cs:523) and this must agree, or the planner
					// refuses to route a move the unit is perfectly entitled to make. The
					// approach to a foreign city is ZOC-to-ZOC by definition (its garrison
					// projects control over every neighbouring tile), so a Diplomat sent to
					// one got a null path, cleared its Goto and stalled a few tiles short —
					// every time, for the whole game.
					bool ignoresZoc = unit is Diplomat || unit is Caravan || unit is Explorer;
					bool zocBlocked = unit.Class != UnitClass.Air && !ignoresZoc
						&& !(fromTile.City is not null && fromTile.City.Owner == unit.Owner)
						&& !(tile.City is not null && tile.City.Owner == unit.Owner)
						&& (OwnerMask(nx, ny) & selfBit) == 0
						&& InZoc(cx, cy) && InZoc(nx, ny);

					// Always allow the goal tile (enemy cities handled by MoveTo/Confront)
					if ((!passable || blocked || zocBlocked) && !(nx == gx && ny == gy)) continue;

					int cost;
					if (RailAt(fromTile) && RailAt(tile))
						cost = 1;
					else if (RoadAt(fromTile) && RoadAt(tile))
						cost = 3;
					else
						cost = tile.Movement * 9;

					int tentativeG = node.g + cost;
					int nextPos = Encode(nx, ny);
					if (!gScore.TryGetValue(nextPos, out int existing) || tentativeG < existing)
					{
						gScore[nextPos] = tentativeG;
						cameFrom[nextPos] = curPos;
						HeapPush((tentativeG + DistanceToTile(nx, ny, gx, gy) * minStepCost, EuclidSq(nx, ny, gx, gy), nextPos, tentativeG));
					}
				}
			}

			return null;
		}

		public static byte BinaryReadByte(BinaryReader reader, int position)
		{
			if (reader.BaseStream.Position != position)
				reader.BaseStream.Seek(position, SeekOrigin.Begin);
			return reader.ReadByte();
		}
		
		public static ushort BinaryReadUShort(BinaryReader reader, int position)
		{
			if (reader.BaseStream.Position != position)
				reader.BaseStream.Seek(position, SeekOrigin.Begin);
			return reader.ReadUInt16();
		}
		
		public static byte[] BinaryReadBytes(BinaryReader reader, int position, int count)
		{
			if (reader.BaseStream.Position != position)
				reader.BaseStream.Seek(position, SeekOrigin.Begin);
			return reader.ReadBytes(count);
		}
		
		private static string[] BytesToArray(byte[] bytes, int maxLength)
		{
			List<string> output = new();
			StringBuilder sb = new StringBuilder();
			foreach (byte b in bytes)
			{
				sb.Append((char)b);
				if (sb.Length != maxLength) continue;
				
				output.Add(sb.ToString().Split((char)0)[0].Trim());
				sb.Clear();
			}
			
			return output.ToArray();
		}
		public static string[] BinaryReadStrings(BinaryReader reader, int position, int length, int itemLength)
		{
			if (reader.BaseStream.Position != position)
				reader.BaseStream.Seek(position, SeekOrigin.Begin);
			return BytesToArray(reader.ReadBytes(length), itemLength);
		}
		
		private static Palette _palette16 = null!;
		public static Palette GetPalette16
		{
			get
			{
				if (_palette16 is null)
				{
					byte[] shades = [0, 104, 183, 255];
					_palette16 = new Colour[]
					{
						Colour.Transparent,
						new Colour(shades[0], shades[0], shades[2]),
						new Colour(shades[0], shades[2], shades[0]),
						new Colour(shades[0], shades[2], shades[2]),
						new Colour(shades[2], shades[0], shades[0]),
						new Colour(shades[0], shades[0], shades[0]),
						new Colour(shades[2], shades[1], shades[0]),
						new Colour(shades[2], shades[2], shades[2]),
						new Colour(shades[1], shades[1], shades[1]),
						new Colour(shades[1], shades[1], shades[3]),
						new Colour(shades[1], shades[3], shades[1]),
						new Colour(shades[1], shades[3], shades[3]),
						new Colour(shades[3], shades[1], shades[1]),
						new Colour(shades[3], shades[1], shades[3]),
						new Colour(shades[3], shades[3], shades[1]),
						new Colour(shades[3], shades[3], shades[3]),
					};
				}
				return _palette16;
			}
		}

		private static Palette _palette256 = null!;

		// Drop the cached Free-mode palette so the next access re-reads palette.txt.
		public static void ReloadPalette256() => _palette256 = null!;

		// Free-mode 256-colour palette: prefer palette.txt in the working directory
		// (editable, matches asset-mode rendering), else the built-in procedural ramp.
		public static Palette GetPalette256
		{
			get
			{
				if (_palette256 is null)
					_palette256 = LoadPaletteFile() ?? BuildDefaultPalette256();
				return _palette256;
			}
		}

		private static readonly string PaletteFilePath =
			Path.Combine(Environment.CurrentDirectory, "palette.txt");

		private static Palette? LoadPaletteFile()
		{
			if (!File.Exists(PaletteFilePath)) return null;

			Palette p = new Palette(256);
			bool any = false;
			foreach (string raw in File.ReadAllLines(PaletteFilePath))
			{
				string line = raw.Trim();
				int hash = line.IndexOf('#');
				if (hash >= 0) line = line.Substring(0, hash).Trim();
				if (line.Length == 0) continue;

				string[] tok = line.Split((char[])[' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
				if (tok.Length < 4) continue;
				if (!int.TryParse(tok[0], out int i) || i < 0 || i > 255) continue;
				if (!int.TryParse(tok[1], out int r) || !int.TryParse(tok[2], out int g) || !int.TryParse(tok[3], out int b)) continue;
				p[i] = new Colour(r, g, b);
				any = true;
			}
			// Index 0 is always the transparency key (matches PicFile). Without this
			// the cursor and other index-0 backgrounds render as opaque black boxes.
			p[0] = Colour.Transparent;
			return any ? p : null;
		}

		private static Palette BuildDefaultPalette256()
		{
			Palette _palette256 = new Palette(256);
			{
					for (int i = 0; i < 256; i++)
					{
						if (i >= 16 && i < 32)
						{
							int ii = (i % 16);
							_palette256[i] = new Colour(254 - (ii * 16), 253 - (ii * 16), 252 - (ii * 16));
							continue;
						}
						if (i >= 32 && i < 40)
						{
							// Greens
							int ii = (i % 8);
							_palette256[i] = new Colour(0, 197 - (ii * 11), 80 - (ii * 7));
							continue;
						}
						if (i >= 40 && i < 42)
						{
							// Browns
							int ii = (i % 2);
							_palette256[i] = new Colour(128 + (ii * 16), 64 + (ii * 8), 0);
							continue;
						}
						if (i >= 42 && i < 48)
						{
							// Yellows
							int ii = (i + 2 % 6);
							_palette256[i] = new Colour(254 - (ii * 6), 245 - (ii * 6), 0);
							continue;
						}
						if (i >= 48 && i < 64)
						{
							int r = Convert.ToInt32((float)_palette16[i % 16].R * 0.7F);
							int g = Convert.ToInt32((float)_palette16[i % 16].G * 0.7F);
							int b = Convert.ToInt32((float)_palette16[i % 16].B * 0.7F);
							_palette256[i] = new Colour(r, g, b);
							continue;
						}
						if (i >= 64 && i < 80)
						{
							// Blues
							int ii = (i % 8);
							_palette256[i] = new Colour(0, 67 - (ii * 5), 211 - (ii * 9));
							continue;
						}
						_palette256[i] = GetPalette16[i % 16];
					}
				}
				return _palette256;
		}

		public static bool AllowSaveGame => Map.Instance.Ready;
	}
}