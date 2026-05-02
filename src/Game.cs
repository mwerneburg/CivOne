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
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.IO;
using CivOne.Screens;
using CivOne.Screens.Reports;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne
{
	public partial class Game : BaseInstance
	{
		private readonly int _difficulty, _competition;
		private readonly Player[] _players;
		private readonly List<City> _cities;
		private readonly List<IUnit> _units;
		private readonly Dictionary<byte, byte> _advanceOrigin = new Dictionary<byte, byte>();
		private readonly List<ReplayData> _replayData = new List<ReplayData>();

		// [0]=barbarians, [1-7]=civs; 0 = not yet launched
		internal readonly int[] SpaceshipLaunchTurn = new int[8];
		internal readonly int[] SpaceshipArrivalTurn = new int[8];
		
		internal readonly string[] CityNames = Common.AllCityNames.ToArray();
		
		private int _currentPlayer = 0;
		private int _activeUnit;
		private bool _activeUnitExplicit = false;
		private readonly HashSet<IUnit> _waitingUnits = new HashSet<IUnit>();

		// True for a land unit sitting on a non-city tile with a boardable ship —
		// it is effectively cargo and should not be prompted for orders.
		private static bool IsAboard(IUnit unit)
		{
			if (unit.Class != UnitClass.Land) return false;
			ITile tile = unit.Tile;
			if (tile == null || tile.City != null) return false;
			return tile.Units.Any(u => u is IBoardable);
		}

		private ushort _anthologyTurn = 0;

		public bool Animations { get; set; }
		public bool Sound { get; set; }
		public bool CivilopediaText { get; set; }
		public bool EndOfTurn { get; set; }
		public bool InstantAdvice { get; set; }

		public bool EnemyMoves { get; set; }
		public bool Palace { get; set; }

		public void SetAdvanceOrigin(IAdvance advance, Player player)
		{
			if (_advanceOrigin.ContainsKey(advance.Id))
				return;
			byte playerNumber = 0;
			if (player != null)
				playerNumber = PlayerNumber(player);
			_advanceOrigin.Add(advance.Id, playerNumber);
		}
		public bool GetAdvanceOrigin(IAdvance advance, Player player)
		{
			if (_advanceOrigin.ContainsKey(advance.Id))
				return (_advanceOrigin[advance.Id] == PlayerNumber(player));
			return false;
		}

		public int Difficulty => _difficulty;

		public bool HasUpdate => false;
		
		private ushort _gameTurn;
		internal ushort GameTurn
		{
			get
			{
				return _gameTurn;
			}
			set
			{
				_gameTurn = value;
				Log($"Turn {_gameTurn}: {GameYear}");
				if (_anthologyTurn >= _gameTurn)
				{
					//TODO: Show anthology
					_anthologyTurn = (ushort)(_gameTurn + 20 + Common.Random.Next(40));
				}
			}
		}
		
		internal string GameYear => Common.YearString(GameTurn);
		
		internal Player HumanPlayer { get; set; }
		
		internal Player CurrentPlayer => _players[_currentPlayer];

		internal ReplayData[] GetReplayData() => _replayData.ToArray();
		internal T[] GetReplayData<T>() where T : ReplayData => _replayData.Where(x => x is T).Select(x => (x as T)).ToArray();

		private void PlayerDestroyed(object sender, EventArgs args)
		{
			Player player = (sender as Player);

			ICivilization destroyed = player.Civilization;
			ICivilization destroyedBy = Game.CurrentPlayer.Civilization;
			if (destroyedBy == destroyed) destroyedBy = Game.GetPlayer(0).Civilization;

			_replayData.Add(new ReplayData.CivilizationDestroyed(_gameTurn, destroyed.Id, destroyedBy.Id));

			if (player.IsHuman)
			{
				// TODO: Move Game Over code here
				return;
			}

			// Before 0 AD, respawn destroyed AI civs using their alternate civilization variant.
			// Each civ has a "buddy" with Id offset by 7 (e.g. Romans Id=1 <-> Mongols Id=8).
			// If the buddy hasn't already been destroyed this game, spawn it in the same player slot.
			if (!(destroyed is Barbarian) && Common.TurnToYear(_gameTurn) < 0)
			{
				byte playerSlot = (byte)destroyed.PreferredPlayerNumber;
				int buddyId = destroyed.Id >= 8 ? destroyed.Id - 7 : destroyed.Id + 7;
				bool buddyDestroyed = _replayData.OfType<ReplayData.CivilizationDestroyed>()
					.Any(rd => rd.DestroyedId == buddyId);
				ICivilization buddyCiv = Common.Civilizations.FirstOrDefault(c => c.Id == buddyId);
				if (!buddyDestroyed && buddyCiv != null)
				{
					_players[playerSlot] = new Player(buddyCiv);
					_players[playerSlot].Destroyed += PlayerDestroyed;
					AddStartingUnits(playerSlot);
				}
			}

			GameTask.Insert(Message.Advisor(Advisor.Defense, false, destroyed.Name, "civilization", "destroyed", $"by {destroyedBy.NamePlural}!"));
		}
		
		internal byte PlayerNumber(Player player)
		{
			byte i = 0;
			foreach (Player p in _players)
			{
				if (p == player)
					return i;
				i++;
			}
			return 0;
		}

		internal Player GetPlayer(byte number)
		{
			if (_players.Length < number)
				return null;
			return _players[number];
		}

		internal IEnumerable<Player> Players => _players;

		// mass_ht = comps×4 + mods×4 + str (in hundred-ton units)
		// flight_years = (4445 + mass_ht) / (100 × engines)  where engines = comps/2
		internal static float SpaceshipFlightYears(int structural, int component, int module)
		{
			int engines = Math.Max(1, component / 2);
			int massHt = component * 4 + module * 4 + structural;
			return (4445f + massHt) / (100f * engines);
		}

		internal static int SpaceshipStructuresNeeded(int component, int module)
		{
			int engines = component / 2;
			int modSets = module / 3;
			return 15 + Math.Max(0, engines - 2) * 4 + Math.Max(0, modSets - 1) * 4;
		}

		// Success: 70% base (1 engine), +6.67% per additional engine up to +20%,
		//          +10% per additional module set above 1, capped at 100%.
		internal static int SpaceshipSuccessPct(int component, int module)
		{
			int engines = component / 2;
			int modSets = module / 3;
			int engineBonus = Math.Min(20, (engines - 1) * 20 / 3);
			int moduleBonus = Math.Min(10, Math.Max(0, modSets - 1) * 10);
			return Math.Min(100, 70 + engineBonus + moduleBonus);
		}

		// Score contribution: hab_modules × 500 × success% / 100
		internal static int SpaceshipScore(int module, int component)
		{
			return module * 500 * SpaceshipSuccessPct(component, module) / 100;
		}

		private static int SpaceshipTravelTurns(int structural, int component, int module)
		{
			return Math.Max(1, (int)Math.Ceiling(SpaceshipFlightYears(structural, component, module)));
		}

		public void EndTurn()
		{
			_waitingUnits.Clear();
			_activeUnitExplicit = false;
			foreach (Player player in _players.Where(x => !(x.Civilization is Barbarian)))
			{
				player.IsDestroyed();
			}

			if (++_currentPlayer >= _players.Length)
			{
				_currentPlayer = 0;
				GameTurn++;
				// Check for spaceship launches (any player now has minimum parts)
				for (int p = 1; p < _players.Length; p++)
				{
					if (_players[p].IsDestroyed()) continue;
					int structural = _cities.Where(c => c.Owner == p).Sum(c => c.Buildings.Count(b => b is SSStructural));
					int component  = _cities.Where(c => c.Owner == p).Sum(c => c.Buildings.Count(b => b is SSComponent));
					int module     = _cities.Where(c => c.Owner == p).Sum(c => c.Buildings.Count(b => b is SSModule));
					// Minimum: 1 engine (2 comps), 1 module set (3 mods), sufficient structure
					int needed = SpaceshipStructuresNeeded(component, module);
					if (component < 2 || module < 3 || structural < needed) continue;
					if (SpaceshipLaunchTurn[p] != 0) continue;

					SpaceshipLaunchTurn[p] = _gameTurn;
					SpaceshipArrivalTurn[p] = _gameTurn + SpaceshipTravelTurns(structural, component, module);
					string eta = Common.YearString((ushort)SpaceshipArrivalTurn[p]);
					if (_players[p] == HumanPlayer)
					{
						PlaySound("wintune");
						GameTask.Enqueue(Message.Newspaper(null, "Our spaceship has", "launched!", $"Arrival: {eta}"));
					}
					else
					{
						GameTask.Enqueue(Message.Advisor(Advisor.Foreign, false,
							$"The {_players[p].TribeNamePlural}",
							"have launched a spaceship!",
							$"Arrival: {eta}"));
					}
				}

				// Check for spaceship arrivals
				int bestArrival = int.MaxValue;
				for (int p = 1; p < _players.Length; p++)
					if (SpaceshipArrivalTurn[p] > 0 && SpaceshipArrivalTurn[p] < bestArrival)
						bestArrival = SpaceshipArrivalTurn[p];

				if (bestArrival <= _gameTurn)
				{
					bool humanWins = SpaceshipArrivalTurn[PlayerNumber(HumanPlayer)] == bestArrival;
					if (humanWins)
					{
						PlaySound("wintune");
						GameTask.Enqueue(Message.Newspaper(null, "Our spaceship has", "reached Alpha Centauri!", $"Score: {HumanPlayer.Score}"));
						GameTask conquest;
						GameTask.Enqueue(conquest = Show.Screen<CivilizationScore>());
						conquest.Done += (s, a) => Runtime.Quit();
					}
					else
					{
						for (int p = 1; p < _players.Length; p++)
						{
							if (SpaceshipArrivalTurn[p] != bestArrival) continue;
							GameTask.Enqueue(Message.Newspaper(null, $"The {_players[p].TribeNamePlural}", "have reached", "Alpha Centauri!"));
							break;
						}
						GameTask.Enqueue(Turn.GameOver(HumanPlayer));
					}
					return;
				}

				// 2100 AD: game ends by score
				if (Common.TurnToYear(_gameTurn) >= 2100)
				{
					Player winner = _players
						.Where(p => !(p.Civilization is Barbarian) && !p.IsDestroyed())
						.OrderByDescending(p => p.Score)
						.ThenBy(p => p == HumanPlayer ? 0 : 1)
						.FirstOrDefault();

					if (winner == HumanPlayer)
					{
						PlaySound("wintune");
						GameTask.Enqueue(Message.Newspaper(null, "The year is 2100!", $"Your score: {HumanPlayer.Score}", "You lead the world!"));
						GameTask scoreTask;
						GameTask.Enqueue(scoreTask = Show.Screen<CivilizationScore>());
						scoreTask.Done += (s, a) => Runtime.Quit();
					}
					else
					{
						GameTask.Enqueue(Turn.GameOver(HumanPlayer));
					}
					return;
				}

				PerformAutoSave();

				IEnumerable<City> disasterCities = _cities.OrderBy(o => Common.Random.Next(0,1000)).Take(2).AsEnumerable();
				foreach (City city in disasterCities)
					city.Disaster();

				if (Barbarian.IsSeaSpawnTurn)
				{
					ITile tile = Barbarian.SeaSpawnPosition;
					if (tile != null)
					{
						foreach (UnitType unitType in Barbarian.SeaSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}
			}

			if (!_players.Any(x => Game.PlayerNumber(x) != 0 && x != Human && !x.IsDestroyed()))
			{
				PlaySound("wintune");

				GameTask conquest;
				GameTask.Enqueue(Message.Newspaper(null, "Your civilization", "has conquered", "the entire planet!"));
				GameTask.Enqueue(conquest = Show.Screen<Conquest>());
				conquest.Done += (s, a) => Runtime.Quit();
			}

			foreach (IUnit unit in _units.Where(u => u.Owner == _currentPlayer))
			{
				GameTask.Enqueue(Turn.New(unit));
			}
			foreach (City city in _cities.Where(c => c.Owner == _currentPlayer).ToArray())
			{
				GameTask.Enqueue(Turn.New(city));
			}
			GameTask.Enqueue(Turn.New(CurrentPlayer));

			if (CurrentPlayer != HumanPlayer) return;
			
			if (Game.InstantAdvice && (Common.TurnToYear(Game.GameTurn) == -3600 || Common.TurnToYear(Game.GameTurn) == -2800))
				GameTask.Enqueue(Message.Help("--- Civilization Note ---", TextFile.Instance.GetGameText("HELP/HELP1")));
			else if (Game.InstantAdvice && (Common.TurnToYear(Game.GameTurn) == -3200 || Common.TurnToYear(Game.GameTurn) == -2400))
				GameTask.Enqueue(Message.Help("--- Civilization Note ---", TextFile.Instance.GetGameText("HELP/HELP2")));
		}
		
		public void Update()
		{
			IUnit unit = ActiveUnit;
			if (CurrentPlayer == HumanPlayer)
			{
				if (unit != null && !unit.Goto.IsEmpty)
				{
					ITile next = Common.GotoStep(unit);
					if (next == null)
					{
						unit.Goto = Point.Empty;
						return;
					}
					unit.MoveTo(next.X - unit.X, next.Y - unit.Y);
					return;
				}
				return;
			}
			if (unit != null && (unit.MovesLeft > 0 || unit.PartMoves > 0))
			{
				GameTask.Enqueue(Turn.Move(unit));
				return;
			}
			GameTask.Enqueue(Turn.End());
		}

		internal int CityNameId(Player player)
		{
			ICivilization civilization = player.Civilization;
			ICivilization[] civilizations = Common.Civilizations;
			int startIndex = Enumerable.Range(1, civilization.Id - 1).Sum(i => civilizations[i].CityNames.Length);
			int spareIndex = Enumerable.Range(1, Common.Civilizations.Length - 1).Sum(i => civilizations[i].CityNames.Length);
			int[] used = _cities.Select(c => c.NameId).ToArray();
			int[] available = Enumerable.Range(0, CityNames.Length)
				.Where(i => !used.Contains(i))
				.OrderBy(i => (i >= startIndex && i < startIndex + civilization.CityNames.Length) ? 0 : 1)
				.ThenBy(i => (i >= spareIndex) ? 0 : 1)
				.ThenBy(i => i)
				.ToArray();
			if (player.CityNamesSkipped >= available.Length)
				return 0;
			return available[player.CityNamesSkipped];
		}

		internal City AddCity(Player player, int nameId, int x, int y)
		{
			if (_cities.Any(c => c.X == x && c.Y == y))
				return null;

			City city = new City(PlayerNumber(player))
			{
				X = (byte)x,
				Y = (byte)y,
				NameId = nameId,
				Size = 1
			};
			if (!_cities.Any(c => c.Size > 0 && c.Owner == city.Owner))
			{
				Palace palace = new Palace();
				palace.SetFree();
				city.AddBuilding(palace);
			}
			if ((Map[x, y] is Desert) || (Map[x, y] is Grassland) || (Map[x, y] is Hills) || (Map[x, y] is Plains) || (Map[x, y] is River))
			{
				Map[x, y].Irrigation = true;
			}
			if (!Map[x, y].RailRoad)
			{
				Map[x, y].Road = true;
			}
			_cities.Add(city);
			Game.UpdateResources(city.Tile);
			return city;
		}

		public void DestroyCity(City city)
		{
			foreach (IUnit unit in _units.Where(u => u.Home == city).ToArray())
				_units.Remove(unit);
			_cities.Remove(city);
			city.X = 255;
			city.Y = 255;
			city.Owner = 0;
		}
		
		internal City GetCity(int x, int y)
		{
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x-= Map.WIDTH;
			if (y < 0) return null;
			if (y >= Map.HEIGHT) return null;
			return _cities.Where(c => c.X == x && c.Y == y && c.Size > 0).FirstOrDefault();
		}
		
		internal static IUnit PeekUnit(UnitType type) => CreateUnit(type, 0, 0);

		private static IUnit CreateUnit(UnitType type, int x, int y)
		{
			IUnit unit;
			switch (type)
			{
				case UnitType.Settlers: unit = new Settlers(); break; 
				case UnitType.Militia: unit = new Militia(); break;
				case UnitType.Phalanx: unit = new Phalanx(); break;
				case UnitType.Legion: unit = new Legion(); break;
				case UnitType.Musketeers: unit = new Musketeers(); break;
				case UnitType.Riflemen: unit = new Riflemen(); break;
				case UnitType.Cavalry: unit = new Cavalry(); break;
				case UnitType.Knights: unit = new Knights(); break;
				case UnitType.Catapult: unit = new Catapult(); break;
				case UnitType.Cannon: unit = new Cannon(); break;
				case UnitType.Chariot: unit = new Chariot(); break;
				case UnitType.Armor: unit = new Armor(); break;
				case UnitType.MechInf: unit = new MechInf(); break;
				case UnitType.Artillery: unit = new Artillery(); break;
				case UnitType.Fighter: unit = new Fighter(); break;
				case UnitType.Bomber: unit = new Bomber(); break;
				case UnitType.Trireme: unit = new Trireme(); break;
				case UnitType.Sail: unit = new Sail(); break;
				case UnitType.Frigate: unit = new Frigate(); break;
				case UnitType.Ironclad: unit = new Ironclad(); break;
				case UnitType.Cruiser: unit = new Cruiser(); break;
				case UnitType.Battleship: unit = new Battleship(); break;
				case UnitType.Submarine: unit = new Submarine(); break;
				case UnitType.Carrier: unit = new Carrier(); break;
				case UnitType.Transport: unit = new Transport(); break;
				case UnitType.Nuclear: unit = new Nuclear(); break;
				case UnitType.Diplomat: unit = new Diplomat(); break;
				case UnitType.Caravan: unit = new Caravan(); break;
				default: return null;
			}
			unit.X = x;
			unit.Y = y;
			unit.MovesLeft = unit.Move;
			return unit;
		}

		public IUnit CreateUnit(UnitType type, int x, int y, byte owner, bool endTurn = false)
		{
			IUnit unit = CreateUnit((UnitType)type, x, y);
			if (unit == null) return null;

			unit.Owner = owner;
			if (unit.Class == UnitClass.Water)
			{
				Player player = GetPlayer(owner);
				if ((player.HasWonder<Lighthouse>() && !WonderObsolete<Lighthouse>()) ||
					(player.HasWonder<MagellansExpedition>() && !WonderObsolete<MagellansExpedition>()))
				{
					unit.MovesLeft++;
				}
			}
			if (endTurn)
				unit.SkipTurn();
			_instance._units.Add(unit);
			return unit;
		}
		
		internal IUnit[] GetUnits(int x, int y)
		{
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x-= Map.WIDTH;
			if (y < 0) return null;
			if (y >= Map.HEIGHT) return null;
			// Use the raw index field, not the ActiveUnit property, to avoid the
			// circular: ActiveUnit → IsAboard → tile.Units → GetUnits → ActiveUnit
			IUnit cur = (_activeUnit >= 0 && _activeUnit < _units.Count) ? _units[_activeUnit] : null;
			return _units.Where(u => u.X == x && u.Y == y).OrderBy(u => (u == cur) ? 0 : (u.Fortify || u.FortifyActive ? 1 : 2)).ToArray();
		}

		internal IUnit[] GetUnits() => _units.ToArray();

		internal void UpdateResources(ITile tile, bool ownerCities = true)
		{
			for (int relY = -3; relY <= 3; relY++)
			for (int relX = -3; relX <= 3; relX++)
			{
				if (tile[relX, relY] == null) continue;
				City city = tile[relX, relY].City;
				if (city == null) continue;
				if (!ownerCities && CurrentPlayer == city.Owner) continue;
				city.UpdateResources();
			}
		}

		public City[] GetCities() => _cities.ToArray();

		public IWonder[] BuiltWonders => _cities.SelectMany(c => c.Wonders).ToArray();

		public bool WonderBuilt<T>() where T : IWonder => BuiltWonders.Any(w => w is T);

		public bool WonderBuilt(IWonder wonder) => BuiltWonders.Any(w => w.Id == wonder.Id);

		public bool WonderObsolete<T>() where T : IWonder, new() => WonderObsolete(new T());

		public bool WonderObsolete(IWonder wonder) => (wonder.ObsoleteTech != null && _players.Any(x => x.HasAdvance(wonder.ObsoleteTech)));
		
		internal void PerformAutoSave()
		{
			try { SaveCos(Settings.Instance.AutoSavePath); }
			catch { }
		}

		public void UpgradeUnit(IUnit unit, UnitType targetType, int cost)
		{
			if (unit == null || !_units.Contains(unit)) return;
			Player player = GetPlayer(unit.Owner);
			if (player.Gold < cost) return;

			player.Gold -= (short)cost;

			IUnit upgraded = CreateUnit(targetType, unit.X, unit.Y);
			if (upgraded == null) return;
			upgraded.Owner   = unit.Owner;
			upgraded.Veteran = unit.Veteran;
			upgraded.SetHome(unit.Home);
			upgraded.SkipTurn();

			_units.Remove(unit);
			_units.Add(upgraded);
		}

		public void DisbandUnit(IUnit unit)
		{
			IUnit activeUnit = ActiveUnit;

			if (unit == null) return;
			if (!_units.Contains(unit)) return;
			if (unit.Tile is Ocean && unit is IBoardable)
			{
				int totalCargo = unit.Tile.Units.Where(u => u is IBoardable).Sum(u => (u as IBoardable).Cargo) - (unit as IBoardable).Cargo;
				while (unit.Tile.Units.Count(u => u.Class != UnitClass.Water) > totalCargo)
				{
					IUnit subUnit = unit.Tile.Units.First(u => u.Class != UnitClass.Water);
					subUnit.X = 255;
					subUnit.Y = 255;
					_units.Remove(subUnit);
				} 
			}
			unit.X = 255;
			unit.Y = 255;
			_units.Remove(unit);

			GetPlayer(unit.Owner).IsDestroyed();

			if (_units.Contains(activeUnit))
			{
				_activeUnit = _units.IndexOf(activeUnit);
			}
		}

		public void UnitWait()
		{
			if (_activeUnit < _units.Count)
				_waitingUnits.Add(_units[_activeUnit]);
			_activeUnit++;
		}
		
		public IUnit ActiveUnit
		{
			get
			{
				if (!_units.Any(u => u.Owner == _currentPlayer && !u.Busy && (!IsAboard(u) || _activeUnitExplicit)))
					return null;

				if (_activeUnit >= _units.Count)
					_activeUnit = 0;

				var cur = _units[_activeUnit];

				// Fast path: current unit is still valid.
				// Respect _activeUnitExplicit to allow a player-selected cargo unit through.
				if (cur.Owner == _currentPlayer && (cur.MovesLeft > 0 || cur.PartMoves > 0) && !cur.Sentry && !cur.Fortify && !_waitingUnits.Contains(cur) && (_activeUnitExplicit || !IsAboard(cur)))
					return cur;

				// Explicit flag only survives one fast-path miss; the scanning loop picks freely.
				_activeUnitExplicit = false;

				// Task busy — hold position
				if (GameTask.Any())
					return cur;

				// No movable units left this turn (waited units don't count here)
				if (!_units.Any(u => u.Owner == _currentPlayer && (u.MovesLeft > 0 || u.PartMoves > 0) && !u.Busy && !IsAboard(u)))
				{
					if (CurrentPlayer == HumanPlayer && !EndOfTurn && !GameTask.Any() && (Common.TopScreen is GamePlay))
						GameTask.Enqueue(Turn.End());
					return null;
				}

				// Advance to the next valid unit, skipping waited and aboard units.
				// If we wrap all the way around without finding one, the player has
				// waited every remaining unit — clear the queue and pick freely.
				int startIdx = _activeUnit;
				while (true)
				{
					_activeUnit++;
					if (_activeUnit >= _units.Count) _activeUnit = 0;

					var u = _units[_activeUnit];
					if (u.Owner == _currentPlayer && (u.MovesLeft > 0 || u.PartMoves > 0) && !u.Sentry && !u.Fortify && !_waitingUnits.Contains(u) && !IsAboard(u))
						break;

					if (_activeUnit == startIdx)
					{
						// Full lap with no candidate — release the wait queue
						_waitingUnits.Clear();
						while (_units[_activeUnit].Owner != _currentPlayer || (_units[_activeUnit].MovesLeft == 0 && _units[_activeUnit].PartMoves == 0) || _units[_activeUnit].Sentry || _units[_activeUnit].Fortify || IsAboard(_units[_activeUnit]))
						{
							_activeUnit++;
							if (_activeUnit >= _units.Count) _activeUnit = 0;
						}
						break;
					}
				}
				return _units[_activeUnit];
			}
			internal set
			{
				if (value == null || value.MovesLeft == 0 && value.PartMoves == 0)
					return;
				value.Busy = false;   // clears Sentry, Fortify, and FortifyActive
				_activeUnit = _units.IndexOf(value);
				_activeUnitExplicit = IsAboard(value);
			}
		}

		public IUnit MovingUnit => _units.FirstOrDefault(u => u.Moving);

		public static bool Started => (_instance != null);
		
		private static Game _instance;
		public static Game Instance
		{
			get
			{
				if (_instance == null)
				{
					Log("ERROR: Game instance does not exist");
				}
				return _instance;
			}
		}
	}
}