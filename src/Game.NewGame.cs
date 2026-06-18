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
using CivOne.Advances;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne
{
	public partial class Game : BaseInstance
	{
		// Real-world centroids for each civ's historical heartland. Used on Earth
		// maps (FixedStartPositions == true) to anchor each civ near its home —
		// Malians in West Africa, Mongols on the steppe, Iroquois on the Great
		// Lakes — instead of scaling the legacy 80×50 StartX/StartY (which were
		// calibrated for a custom Civ I projection and land in random spots on
		// our equirectangular Epic Earth: Mali at 36/30 lands in the South
		// Atlantic and spirals into Brazil before finding habitable land).
		//
		// Coordinates are (latitude, longitude) in degrees; converted to tile
		// coords via equirectangular projection. Civs not in the table fall
		// through to the StartX/StartY path, then random placement. Olvir is
		// deliberately omitted — it's a story-arc civ placed by other logic.
		private static readonly System.Collections.Generic.Dictionary<Civilization, (double lat, double lon)> EarthCentroids = new()
		{
			{ Civilization.Romans,      ( 42.0,   12.0) },
			{ Civilization.Babylonians, ( 33.0,   44.0) },
			{ Civilization.Germans,     ( 51.0,   10.0) },
			{ Civilization.Egyptians,   ( 26.0,   31.0) },
			{ Civilization.Americans,   ( 39.0,  -98.0) },
			{ Civilization.Greeks,      ( 38.0,   23.0) },
			{ Civilization.Indians,     ( 22.0,   78.0) },
			{ Civilization.Russians,    ( 55.0,   38.0) },
			{ Civilization.Zulus,       (-29.0,   31.0) },
			{ Civilization.French,      ( 47.0,    2.0) },
			{ Civilization.Aztecs,      ( 19.0,  -99.0) },
			{ Civilization.Chinese,     ( 35.0,  113.0) },
			{ Civilization.English,     ( 52.0,   -1.0) },
			{ Civilization.Mongols,     ( 47.0,  105.0) },
			{ Civilization.Japanese,    ( 36.0,  138.0) },
			{ Civilization.Persians,    ( 32.0,   53.0) },
			{ Civilization.Lakota,      ( 44.0, -100.0) },
			{ Civilization.Arabs,       ( 24.0,   45.0) },
			{ Civilization.Khmer,       ( 12.0,  105.0) },
			{ Civilization.Malians,     ( 17.0,   -4.0) },
			{ Civilization.Inca,        (-13.0,  -72.0) },
			{ Civilization.Ottomans,    ( 39.0,   35.0) },
			{ Civilization.Ethiopians,  (  9.0,   39.0) },
			{ Civilization.Iroquois,    ( 43.0,  -76.0) },
		};

		private void AddStartingUnits(byte player)
		{
			// Translated from this post by darkpanda, might contain errors:
			// http://forums.civfanatics.com/showthread.php?p=12895306&highlight=starting+position#post12895306
			//
			// Map-scale knobs: the original separation/continent thresholds were tuned
			// for an 80-wide map. On Epic-size Earth (320×200) a 10-tile minimum
			// distance puts every civ on the same continent. Scale by Map.WIDTH/80 so
			// the same proportional separation applies at any size; clamp to >=1 so
			// 40-wide Tiny maps don't degenerate to zero.
			int mapScale = Math.Max(1, Map.WIDTH / 80);
			int baseSeparation = 10 * mapScale;
			int baseContinentFloor = 32 * mapScale * mapScale;  // area-proportional
			int loopCounter = 0;
			while (loopCounter++ < 2000)
			{
				// Choose a map square randomly
				int x = Common.Random.Next(0, Map.WIDTH);
				int y = Common.Random.Next(2, Map.HEIGHT - 2);
				// Prefer the real-world centroid when we have one and the map is
				// Earth-shaped; otherwise fall back to the legacy 80×50 StartX/StartY.
				// 255 is the sentinel for "no fixed position" — fall through to random.
				int civStartX = _players[player].Civilization.StartX;
				int civStartY = _players[player].Civilization.StartY;
				Civilization civKey = (Civilization)_players[player].Civilization.Id;
				bool useCentroid = Map.FixedStartPositions && GameTurn == 0
				                && EarthCentroids.ContainsKey(civKey);
				bool useFixed = Map.FixedStartPositions && GameTurn == 0
				             && civStartX != 255 && civStartY != 255;
				if (useCentroid)
				{
					// Equirectangular projection — same one used by build_earth_map.py
					// and EnsureFreshwaterReachability, so coordinates match the map.
					var (lat, lon) = EarthCentroids[civKey];
					x = (int)(((lon + 180.0) / 360.0) * Map.WIDTH);
					y = (int)(((90.0 - lat)  / 180.0) * Map.HEIGHT);
					if (x < 0) x = 0; if (x >= Map.WIDTH)  x = Map.WIDTH  - 1;
					if (y < 2) y = 2; if (y >= Map.HEIGHT - 2) y = Map.HEIGHT - 3;

					bool Habitable(ITile t) => t is not null && !t.IsOcean
					                        && !(t is Mountains) && !(t is Arctic);
					if (!Habitable(Map[x, y]))
					{
						bool found = false;
						for (int r = 1; r <= 20 && !found; r++)
						for (int dy = -r; dy <= r && !found; dy++)
						for (int dx = -r; dx <= r && !found; dx++)
						{
							if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
							int nx = (x + dx + Map.WIDTH) % Map.WIDTH;
							int ny = y + dy;
							if (ny < 2 || ny >= Map.HEIGHT - 2) continue;
							if (!Habitable(Map[nx, ny])) continue;
							x = nx; y = ny;
							found = true;
						}
					}
					if (Map[x, y].Hut) Map[x, y].Hut = false;
				}
				else if (useFixed)
				{
					// Per-civ StartX/StartY are calibrated for the 80×50 classic Earth.
					// Scale to the actual map width so Epic (320×200) at 4× lands roughly
					// where the historical map placed each civ — Russia in Russia, Mali in
					// West Africa, etc. — instead of multiplying the random-placement
					// scorer's odds of dropping the Russians next door to Timbuktu.
					x = (civStartX * Map.WIDTH)  / 80;
					y = (civStartY * Map.HEIGHT) / 50;
					if (x < 0) x = 0; if (x >= Map.WIDTH)  x = Map.WIDTH  - 1;
					if (y < 2) y = 2; if (y >= Map.HEIGHT - 2) y = Map.HEIGHT - 3;

					// Spiral outward from the scaled point looking for a habitable tile.
					// Some coastal civs at 80×50 scale into ocean on the procedural Epic
					// (sea-level threshold differences), and the original placement might
					// also be impassable Mountain/Arctic. Search up to a 20-tile radius.
					bool Habitable(ITile t) => t is not null && !t.IsOcean
					                        && !(t is Mountains) && !(t is Arctic);
					if (!Habitable(Map[x, y]))
					{
						bool found = false;
						for (int r = 1; r <= 20 && !found; r++)
						for (int dy = -r; dy <= r && !found; dy++)
						for (int dx = -r; dx <= r && !found; dx++)
						{
							if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // ring only
							int nx = (x + dx + Map.WIDTH) % Map.WIDTH;
							int ny = y + dy;
							if (ny < 2 || ny >= Map.HEIGHT - 2) continue;
							if (!Habitable(Map[nx, ny])) continue;
							x = nx; y = ny;
							found = true;
						}
					}
					if (Map[x, y].Hut) Map[x, y].Hut = false;
				}
				else
				{
					ITile tile = Map[x, y];
					if (tile is null) continue;

					if (tile.IsOcean) continue; // Is it an ocean tile?
					if (tile.Hut) continue; // Is there a hut on this tile?
					if (_units.Any(u => u.X == x && u.Y == y)) continue; // Is there already a unit on this exact tile? (|| would reject whole rows/columns and starve placement)
					if (tile.LandValue < (12 - (loopCounter / 32))) continue; // Is the land value high enough?
					if (_cities.Any(c => Common.DistanceToTile(x, y, c.X, c.Y) < (baseSeparation - (loopCounter / 64)))) continue; // Distance to other cities
					if (_units.Any(u => (u is Settlers) && Common.DistanceToTile(x, y, u.X, u.Y) < (baseSeparation - (loopCounter / 64)))) continue; // Distance to other settlers
					if (Map.ContinentTiles(tile.ContinentId).Count(t => Map.TileIsType(t, Terrain.Plains, Terrain.Grassland1, Terrain.Grassland2, Terrain.River)) < (baseContinentFloor - (GameTurn / 16))) continue; // Check buildable tiles on continent
					
					// After 0 AD, don't spawn a Civilization on a continent that already contains cities.
					if (Common.TurnToYear(GameTurn) >= 0 && Map.ContinentTiles(tile.ContinentId).Any(t => t.City is not null)) continue;
					
					Log(loopCounter.ToString());
				}
				
				// Starting position found, add Settlers
				IUnit unit = CreateUnit(UnitType.Settlers, x, y)!;
				unit.Owner = player;
				_units.Add(unit);

				_players[player].StartX = (short)x;
				return;
			}

			// Fallback: the constrained search above can still exhaust its 2000 tries on
			// crowded or fragmented maps. A civ left without a settler has no city and no
			// unit, so IsDestroyed() wipes it on turn 1 — recorded as "destroyed by
			// Barbarians" (Game.PlayerDestroyed uses player 0 as the placeholder killer).
			// That was the real cause of NPC civs vanishing in the opening rounds. Place
			// the settler on the best habitable, unoccupied land tile so every civ enters
			// the game; spacing from existing settlers is preferred but not required.
			{
				bool Habitable(ITile t) => t is not null && !t.IsOcean && !(t is Mountains) && !(t is Arctic);
				ITile? best = null;
				int bestScore = int.MinValue;
				for (int yy = 2; yy < Map.HEIGHT - 2; yy++)
				for (int xx = 0; xx < Map.WIDTH; xx++)
				{
					ITile t = Map[xx, yy];
					if (!Habitable(t) || t.City is not null) continue;
					if (_units.Any(u => u.X == xx && u.Y == yy)) continue;
					int nearest = _units.Where(u => u is Settlers)
						.Select(u => Common.DistanceToTile(xx, yy, u.X, u.Y))
						.DefaultIfEmpty(999).Min();
					int score = t.LandValue + Math.Min(nearest, 24);
					if (score > bestScore) { bestScore = score; best = t; }
				}
				if (best is not null)
				{
					IUnit unit = CreateUnit(UnitType.Settlers, best.X, best.Y)!;
					unit.Owner = player;
					_units.Add(unit);
					_players[player].StartX = (short)best.X;
				}
			}
		}

		private void CalculateHandicap(byte player)
		{
			// Translated drom this post by Gowron:
			// http://forums.civfanatics.com/showthread.php?t=494994
			
			// All Handicap values start from 0.
			byte handicap = 0;
			IUnit startUnit = _units.Where(u => u.Owner == player).FirstOrDefault();
			if (startUnit is null) return;
			int x = startUnit.X, y = startUnit.Y;

			ITile[] continent = Map.ContinentTiles(Map[x, y].ContinentId).ToArray();
			IUnit[] unitsOnContinent = _units.Where(u => continent.Any(c => (c.X == u.X && c.Y == u.Y))).ToArray();
			
			if (unitsOnContinent.Count() == 0)
			{
				// Add +4 if the civ does not share its land mass with any other civs.
				handicap += 4;
			}
			else if (unitsOnContinent.Select(u => Common.DistanceToTile(x, y, u.X, u.Y)).Min() >= 20)
			{
				// If that is not the case, then add +2 if the nearest civ on the same continent is 20 or more squares away.
				handicap += 2;
			}
			else if (unitsOnContinent.Select(u => Common.DistanceToTile(x, y, u.X, u.Y)).Min() >= 10)
			{
				// Add +1 instead if the nearest civ on the same continent is 10-19 squares away.
				handicap += 1;
			}

			// Check the terrain of the starting position and the 8 adjacent map squares.
			if (Map[x, y].GetBorderTiles().Count(t => (t is River)) >= 1)
			{
				// Add +2 if there's at least one river square among them.
				handicap += 2;
			}
			else if (Map[x, y].GetBorderTiles().Count(t => (t is Grassland)) >= 3)
			{
				// If that is not the case, then add +1 if there are 3 or more grassland squares among them.
				handicap += 1;
			}

			if (continent.Count() >= 200)
			{
				// Add +2 if the civ starts on a continent that covers at least 200 map squares.
				handicap += 2;
			}
			else if (continent.Count() >= 100)
			{
				// If that is not the case, then add +1 if the civ starts on a continent that covers at least 100 map squares.
				handicap += 1;
			}

			_players[player].Handicap = handicap;
		}

		private void ApplyBonus(byte player)
		{
			byte bonus = (byte)(_players.Max(p => p.Handicap) - _players[player].Handicap);
			IUnit startUnit = _units.Where(u => u.Owner == player).FirstOrDefault();
			if (startUnit is null) return;
			int x = startUnit.X, y = startUnit.Y;

			if (bonus >= 4)
			{
				// If the Bonus value of the civ is 4 or higher, then the civ is granted an extra Settlers unit, for a total of two Settlers units.
				// In this case, the Bonus value is reduced by 3 afterwards.
				IUnit unit = CreateUnit(UnitType.Settlers, x, y)!;
				unit.Owner = player;
				_units.Add(unit);

				bonus -= 3;
			}

			// If the Bonus value is (still) greater than zero, then the civ gains a number of technologies equal to the Bonus value.
			while (bonus > 0)
			{
				IAdvance[] available = _players[player].AvailableResearch.ToArray();
				int advanceId = Common.Random.Next(0, 72);
				for (int i = 0; i < 1000; i++)
				{
					if (!available.Any(a => a.Id == (advanceId + i) % 72)) continue;
					IAdvance advance = available.First(a => a.Id == (advanceId + i) % 72);
					SetAdvanceOrigin(advance, null);
					_players[player].AddAdvance(advance, false);
					break;
				}
				bonus--;
			}
		}
		
		public static void CreateGame(int difficulty, int competition, ICivilization tribe, string? leaderName = null, string? tribeName = null, string? tribeNamePlural = null)
		{
			if (_instance is not null)
			{
				Log("ERROR: Game instance already exists");
				return;
			}
			_instance = new Game(difficulty, competition, tribe, leaderName, tribeName, tribeNamePlural);
			WLTKNotifications.Clear();
			DecisionLogger.BeginGame();

			foreach (IUnit unit in _instance._units)
			{
				unit.Explore();
			}
		}

		private Game(int difficulty, int competition, ICivilization tribe, string? leaderName, string? tribeName, string? tribeNamePlural)
		{
			// New-game setup sequence:
			//   1. Apply game options (animations, sound, advice level, etc.).
			//   2. Assign player slots: the human player goes into tribe.PreferredPlayerNumber;
			//      each AI slot picks randomly from civs that share that PreferredPlayerNumber
			//      (Olvir is excluded — it only enters via the post-contact story arc).
			//   3. Place starting Settlers for each AI player (AddStartingUnits).
			//   4. Score each starting position (CalculateHandicap) and distribute bonus
			//      units/techs to players who drew weaker positions (ApplyBonus).
			_difficulty = difficulty;
			_competition = competition;
			Log("Game instance created (difficulty: {0}, competition: {1})", _difficulty, _competition);

			InstantAdvice = (Settings.InstantAdvice == GameOption.On || (Settings.InstantAdvice == GameOption.Default && difficulty == 0));
			EndOfTurn = (Settings.EndOfTurn == GameOption.On);
			Animations = (Settings.Animations != GameOption.Off);
			EnemyMoves = (Settings.EnemyMoves != GameOption.Off);
			CivilopediaText = (Settings.CivilopediaText != GameOption.Off);

			_cities = new List<City>();
			_units = new List<IUnit>();
			int slotCount = competition + 1;
			_players = new List<Player>(Enumerable.Repeat<Player>(null!, slotCount));
			SpaceshipLaunchTurn  = new int[slotCount];
			SpaceshipArrivalTurn = new int[slotCount];
			SpaceshipStructural  = new int[slotCount];
			SpaceshipComponent   = new int[slotCount];
			SpaceshipModule      = new int[slotCount];
			for (int i = 0; i <= competition; i++)
			{
				if (i == tribe.PreferredPlayerNumber)
				{
					_players[i] = new Player(tribe, leaderName, tribeName, tribeNamePlural);
					_players[i].Destroyed += PlayerDestroyed;
					HumanPlayer = _players[i];
					if (difficulty == 0)
					{
						// Chieftain starts with 50 Gold
						HumanPlayer.Gold = 50;
					}
					Log("- Player {0} is {1} of the {2} (human)", i, _players[i].LeaderName, _players[i].TribeNamePlural);
					continue;
				}
				
				ICivilization[] civs = Common.Civilizations.Where(civ => civ.PreferredPlayerNumber == i && !(civ is Civilizations.Olvir)).ToArray();
				int r = Common.Random.Next(civs.Length);
				
				_players[i] = new Player(civs[r]);
				_players[i].Destroyed += PlayerDestroyed;
				
				Log("- Player {0} is {1} of the {2}", i, _players[i].LeaderName, _players[i].TribeNamePlural);
			}
			
			Log("Adding starting units...");
			for (byte i = 1; i <= competition; i++)
			{
				AddStartingUnits(i);
			}

			Log("Calculate players handicap...");
			for (byte i = 1; i <= competition; i++)
			{
				CalculateHandicap(i);
			}

			Log("Apply players bonus...");
			for (byte i = 1; i <= competition; i++)
			{
				ApplyBonus(i);
			}

			GameTurn = 0;

			// Number of turns to next antholoy needs to be checked
			_anthologyTurn = (ushort)Common.Random.Next(1, 128);
		}
	}
}