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
using System.IO;
using System.Linq;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne
{
	public partial class Game : BaseInstance
	{
		public static void LoadGame(string sveFile, string mapFile)
		{
			if (_instance != null)
			{
				Log("ERROR: Game instance already exists");
				return;
			}

			using (IGameData adapter = SaveDataAdapter.Load(File.ReadAllBytes(sveFile)))
			{
				if (!adapter.ValidData)
				{
					Log("SaveDataAdapter failed to load game");
					return;
				}

				Map.Instance.LoadMap(mapFile, adapter.RandomSeed);
				_instance = new Game(adapter);
				WLTKNotifications.Clear();
				_instance.LoadProductionQueues(sveFile);
				Log($"Game instance loaded (difficulty: {_instance._difficulty}, competition: {_instance._competition}");
			}
		}

		public void Save(string sveFile, string mapFile)
		{
			using (IGameData gameData = new SaveDataAdapter())
			{
				gameData.GameTurn = _gameTurn;
				gameData.GlobalWarmingCount = GlobalWarmingCount;
				gameData.HumanPlayer = (ushort)PlayerNumber(HumanPlayer);
				gameData.RandomSeed = Map.Instance.SaveMap(mapFile);
				gameData.Difficulty = (ushort)_difficulty;
				gameData.ActiveCivilizations = _players.Select(x => (x.Civilization is Barbarian) || (x.Cities.Any(c => c.Size > 0) || GetUnits().Any(u => x == u.Owner))).ToArray();
				gameData.CivilizationIdentity = _players.Select(x => (byte)(x.Civilization.Id > 7 ? 1 : 0)).ToArray();
				gameData.CurrentResearch = HumanPlayer.CurrentResearch?.Id ?? 0;
				byte[][] discoveredAdvanceIDs = new byte[_players.Length][];
				for (int p = 0; p < _players.Length; p++)
					discoveredAdvanceIDs[p] = _players[p].Advances.Select(x => x.Id).ToArray();
				gameData.DiscoveredAdvanceIDs = discoveredAdvanceIDs;
				gameData.LeaderNames = _players.Select(x => x.LeaderName).ToArray();
				gameData.CivilizationNames = _players.Select(x => x.TribeNamePlural).ToArray();
				gameData.CitizenNames = _players.Select(x => x.TribeName).ToArray();
				gameData.CityNames = CityNames;
				gameData.PlayerGold = _players.Select(x => x.Gold).ToArray();
				gameData.ResearchProgress = _players.Select(x => x.Science).ToArray();
				gameData.TaxRate = _players.Select(x => (ushort)x.TaxesRate).ToArray();
				gameData.ScienceRate = _players.Select(p => (ushort)p.ScienceRate).ToArray();
				gameData.StartingPositionX = _players.Select(x => (ushort)x.StartX).ToArray();
				gameData.Government = _players.Select(x => (ushort)x.Government.Id).ToArray();
				ushort[] diplomacyOut = new ushort[8 * 8];
				for (int i = 0; i < _players.Length; i++)
				for (int j = 0; j < _players.Length; j++)
				{
					if (i == j) continue;
					diplomacyOut[i * 8 + j] = _players[i].IsAtWar(_players[j]) ? (ushort)0x2 : (ushort)0x0;
				}
				gameData.Diplomacy = diplomacyOut;
				gameData.Cities = _cities.GetCityData().ToArray();
				gameData.Units = _players.Select(p => _units.Where(u => p == u.Owner).GetUnitData().ToArray()).ToArray();
				ushort[] wonders = Enumerable.Repeat(ushort.MaxValue, 22).ToArray();
				for (byte i = 0; i < _cities.Count(); i++)
				foreach (IWonder wonder in _cities[i].Wonders)
				{
					wonders[wonder.Id] = i;
				}
				gameData.Wonders = wonders;
				bool[][,] visibility = new bool[_players.Length][,];
				for (int p = 0; p < visibility.Length; p++)
				{
					visibility[p] = new bool[80, 50];
					for (int xx = 0; xx < 80; xx++)
					for (int yy = 0; yy < 50; yy++)
					{
						if (!_players[p].Visible(xx, yy)) continue;
						visibility[p][xx, yy] = true;
					}
				}
				gameData.TileVisibility = visibility;
				ushort[] firstDiscovery = new ushort[72];
				foreach (byte key in _advanceOrigin.Keys)
					firstDiscovery[key] = _advanceOrigin[key];
				gameData.AdvanceFirstDiscovery = firstDiscovery;
				gameData.GameOptions = new bool[]
				{
					InstantAdvice,
					false,
					EndOfTurn,
					Animations,
					Sound,
					EnemyMoves,
					CivilopediaText,
					Palace
				};
				gameData.NextAnthologyTurn = _anthologyTurn;
				gameData.OpponentCount = (ushort)(_players.Length - 2);
				gameData.ReplayData = _replayData.ToArray();
				File.WriteAllBytes(sveFile, gameData.GetBytes());
			}
			SaveProductionQueues(sveFile);
		}

		private static string QueueFilePath(string sveFile) =>
			Path.Combine(Path.GetDirectoryName(sveFile), Path.GetFileNameWithoutExtension(sveFile) + ".civ1q");

		private void SaveProductionQueues(string sveFile)
		{
			var citiesWithQueues = _cities.Where(c => c.ProductionQueue.Count > 0).ToList();
			bool hasSpaceshipData = SpaceshipLaunchTurn.Any(t => t != 0);
			bool hasFutureTechData = _players.Any(p => p.FutureTechs > 0);
			if (citiesWithQueues.Count == 0 && !hasSpaceshipData && !hasFutureTechData) return;

			using (var bw = new BinaryWriter(File.Create(QueueFilePath(sveFile))))
			{
				bw.Write((byte)citiesWithQueues.Count);
				foreach (City city in citiesWithQueues)
				{
					bw.Write(city.X);
					bw.Write(city.Y);
					bw.Write((byte)city.ProductionQueue.Count);
					foreach (IProduction item in city.ProductionQueue)
						bw.Write(item.GetType().Name);
				}
				// 0xFF marker, 8×int32 spaceship turns, 8×int32 future tech counts
				bw.Write((byte)0xFF);
				foreach (int t in SpaceshipLaunchTurn)  bw.Write(t);
				foreach (int t in SpaceshipArrivalTurn) bw.Write(t);
				for (int i = 0; i < _players.Length; i++) bw.Write(_players[i].FutureTechs);
				for (int i = _players.Length; i < 8; i++) bw.Write(0);
			}
		}

		private void LoadProductionQueues(string sveFile)
		{
			string path = QueueFilePath(sveFile);
			if (!File.Exists(path)) return;

			try
			{
				using (var br = new BinaryReader(File.OpenRead(path)))
				{
					int cityCount = br.ReadByte();
					for (int i = 0; i < cityCount; i++)
					{
						byte x = br.ReadByte();
						byte y = br.ReadByte();
						int queueLen = br.ReadByte();
						City city = _cities.FirstOrDefault(c => c.X == x && c.Y == y);
						for (int q = 0; q < queueLen; q++)
						{
							string typeName = br.ReadString();
							if (city == null) continue;
							IProduction item = Reflect.GetProduction()
							    .FirstOrDefault(p => p.GetType().Name == typeName);
							if (item != null) city.EnqueueProduction(item);
						}
					}

					// 0xFF marker, spaceship turns, future tech counts
					if (br.BaseStream.Position < br.BaseStream.Length && br.ReadByte() == 0xFF)
					{
						for (int i = 0; i < 8; i++) SpaceshipLaunchTurn[i]  = br.ReadInt32();
						for (int i = 0; i < 8; i++) SpaceshipArrivalTurn[i] = br.ReadInt32();
						for (int i = 0; i < 8 && br.BaseStream.Position < br.BaseStream.Length; i++)
						{
							int ft = br.ReadInt32();
							if (i < _players.Length) _players[i].SetFutureTechs(ft);
						}
					}
				}
			}
			catch (Exception)
			{
				// Silently discard corrupt or version-mismatched queue files.
			}
		}

		private Game(IGameData gameData)
		{
			_difficulty = gameData.Difficulty;
			_competition = (gameData.OpponentCount + 1);
			_players = new Player[_competition + 1];
			_cities = new List<City>();
			_units = new List<IUnit>();

			ushort[] advanceFirst = gameData.AdvanceFirstDiscovery;
			bool[][,] visibility = gameData.TileVisibility;
			for (int i = 0; i < _players.Length; i++)
			{
				ICivilization[] civs = Common.Civilizations.Where(c => c.PreferredPlayerNumber == i).ToArray();
				ICivilization civ = civs[gameData.CivilizationIdentity[i] % civs.Length];
				Player player = (_players[i] = new Player(civ, gameData.LeaderNames[i], gameData.CitizenNames[i], gameData.CivilizationNames[i]));
				player.Destroyed += PlayerDestroyed;
				player.Gold = gameData.PlayerGold[i];
				player.Science = gameData.ResearchProgress[i];
				player.Government = Reflect.GetGovernments().FirstOrDefault(x => x.Id == gameData.Government[i]);

				player.TaxesRate = gameData.TaxRate[i];
				player.LuxuriesRate = 10 - gameData.ScienceRate[i] - player.TaxesRate;
				player.StartX = (short)gameData.StartingPositionX[i];
				
				// Set map visibility
				for (int xx = 0; xx < 80; xx++)
				for (int yy = 0; yy < 50; yy++)
				{
					if (!visibility[i][xx, yy]) continue;
					if (i == 0 && Map[xx, yy].Hut) Map[xx, yy].Hut = false;
					player.Explore(xx, yy, 0);
				}

				byte[] advanceIds = gameData.DiscoveredAdvanceIDs[i];
				Common.Advances.Where(x => advanceIds.Any(id => x.Id == id)).ToList().ForEach(x =>
				{
					player.AddAdvance(x, false);
					if (advanceFirst[x.Id] != player.Civilization.Id) return;
					SetAdvanceOrigin(x, player);
				});
			}

			// Load war/peace state from the Diplomacy matrix (0x2 = at war)
			ushort[] diplomacy = gameData.Diplomacy;
			for (int i = 0; i < _players.Length; i++)
			for (int j = 0; j < _players.Length; j++)
			{
				if (i == j) continue;
				if (diplomacy[i * 8 + j] == 0x2)
					_players[i].SetAtWar((byte)j, true);
			}

			GameTurn = gameData.GameTurn;
			CityNames = gameData.CityNames;
			HumanPlayer = _players[gameData.HumanPlayer];
			HumanPlayer.CurrentResearch = Common.Advances.FirstOrDefault(a => a.Id == gameData.CurrentResearch);
		
			_anthologyTurn = gameData.NextAnthologyTurn;
			GlobalWarmingCount = gameData.GlobalWarmingCount;

			Dictionary<byte, City> cityList = new Dictionary<byte, City>();
			foreach (CityData cityData in gameData.Cities)
			{
				City city = new City(cityData.Owner)
				{
					X = cityData.X,
					Y = cityData.Y,
					NameId = cityData.NameId,
					Size = cityData.ActualSize,
					Food = cityData.Food,
					Shields = cityData.Shields
				};
				city.SetProduction(cityData.CurrentProduction);
				city.SetResourceTiles(cityData.ResourceTiles);
				
				// Set city buildings
				foreach (byte buildingId in cityData.Buildings)
				{
					city.AddBuilding(Common.Buildings.First(b => b.Id == buildingId));
				}

				// Set city wonders
				foreach (IWonder wonder in Common.Wonders)
				{
					if (gameData.Wonders[wonder.Id] != cityData.Id) continue;
					city.AddWonder(wonder);
				}
				
				_cities.Add(city);

				foreach (byte fortifiedUnit in cityData.FortifiedUnits)
				{
					IUnit unit = CreateUnit((UnitType)fortifiedUnit, city.X, city.Y);
					unit.Status = (byte)(1 << 3);
					unit.Owner = city.Owner;
					unit.SetHome(city);
					_units.Add(unit);

				/*if (city.IsInDisorder)
				{
					city.WasInDisorder = true;
				}*/

				}

				cityList.Add(cityData.Id, city);
			}

			UnitData[][] unitData = gameData.Units;
			for (byte p = 0; p < 8; p++)
			{
				if (!gameData.ActiveCivilizations[p]) continue;
				foreach (UnitData data in unitData[p])
				{
					IUnit unit = CreateUnit((UnitType)data.TypeId, data.X, data.Y);
					if (unit == null) continue;
					unit.Status = data.Status;
					unit.Owner = p;
					unit.PartMoves = (byte)(data.RemainingMoves % 3);
					unit.MovesLeft = (byte)((data.RemainingMoves - unit.PartMoves) / 3);
					if (data.GotoX != 0xFF) unit.Goto = new Point(data.GotoX, data.GotoY);
					if (cityList.ContainsKey(data.HomeCityId))
					{
						unit.SetHome(cityList[data.HomeCityId]);
					}
					_units.Add(unit);
				}
			}

			_replayData.AddRange(gameData.ReplayData);

			// Game Settings
			InstantAdvice = (Settings.InstantAdvice == GameOption.On);
			EndOfTurn = (Settings.EndOfTurn == GameOption.On);
			Animations = (Settings.Animations != GameOption.Off);
			Sound = (Settings.Sound != GameOption.Off);
			EnemyMoves = (Settings.EnemyMoves != GameOption.Off);
			CivilopediaText = (Settings.CivilopediaText != GameOption.Off);
			Palace = (Settings.Palace != GameOption.Off);

			bool[] options = gameData.GameOptions;
			if (Settings.InstantAdvice == GameOption.Default) InstantAdvice = options[0];
			if (Settings.EndOfTurn == GameOption.Default) EndOfTurn = options[2];
			if (Settings.Animations == GameOption.Default) Animations = options[3];
			if (Settings.Sound == GameOption.Default) Sound = options[4];
			if (Settings.EnemyMoves == GameOption.Default) EnemyMoves = options[5];
			if (Settings.CivilopediaText == GameOption.Default) CivilopediaText = options[6];
			if (Settings.Palace == GameOption.Default) Palace = options[7];

			_currentPlayer = gameData.HumanPlayer;
			for (int i = 0; i < _units.Count(); i++)
			{
				if (_units[i].Owner != gameData.HumanPlayer || _units[i].Busy) continue;
				_activeUnit = i;
				if (_units[i].MovesLeft > 0) break;
			}
		}
	}
}