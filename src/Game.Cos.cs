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
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Persistence;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne
{
	public partial class Game : BaseInstance
	{
		// ── Save ────────────────────────────────────────────────────────────────

		public void SaveCos(string cosFile)
		{
			var playerCount = _players.Length;

			// Advance origin: advance-id → civilization-id (not player-index)
			var advanceOrigin = new Dictionary<int, int>();
			foreach (var kv in _advanceOrigin)
				advanceOrigin[kv.Key] = kv.Value;

			// Wonders per city: build a lookup of wonder.Id → city index
			var wonderCityIndex = new int[22];
			for (int i = 0; i < wonderCityIndex.Length; i++) wonderCityIndex[i] = -1;
			for (int i = 0; i < _cities.Count; i++)
				foreach (var w in _cities[i].Wonders)
					wonderCityIndex[w.Id] = i;

			// Cities
			var cities = new List<CosCity>();
			for (int ci = 0; ci < _cities.Count; ci++)
			{
				var city = _cities[ci];

				var tradeRoutes = city.TradeRoutes
					.Select(r => new CosTradeRoute { PartnerX = r.Partner.X, PartnerY = r.Partner.Y, Commodity = r.Commodity })
					.ToList();

				var wonders = Common.Wonders
					.Where(w => wonderCityIndex[w.Id] == ci)
					.Select(w => (int)w.Id)
					.ToArray();

				cities.Add(new CosCity
				{
					Id             = ci,
					X              = city.X,
					Y              = city.Y,
					NameId         = city.NameId,
					Owner          = city.Owner,
					Size           = city.Size,
					Food           = city.Food,
					Shields        = city.Shields,
					Production     = city.CurrentProduction?.GetType().Name,
					ProductionQueue = city.ProductionQueue.Select(p => p.GetType().Name).ToArray(),
					Buildings      = city.Buildings.Select(b => (int)b.Id).ToArray(),
					Wonders        = wonders,
					ResourceTiles  = city.GetResourceTiles().Select(b => (int)b).ToArray(),
					TradeRoutes    = tradeRoutes,
					WasInDisorder  = city.WasInDisorder  ? (bool?)true : null,
					WasWeLoveKing  = city.WasWeLoveKing  ? (bool?)true : null
				});
			}

			// City set used to identify home cities for units
			var cityByRef = _cities.Select((c, i) => (city: c, idx: i)).ToDictionary(t => t.city, t => t.idx);

			var units = new List<CosUnit>();
			foreach (var unit in _units)
			{
				int? gx = unit.Goto.IsEmpty ? (int?)null : unit.Goto.X;
				int? gy = unit.Goto.IsEmpty ? (int?)null : unit.Goto.Y;
				int homeCityId = unit.Home != null && cityByRef.TryGetValue(unit.Home, out var idx) ? idx : -1;

				int? buildRoad = null, buildIrr = null, buildMine = null, buildFort = null;
				if (unit is Settlers settlers)
				{
					if (settlers.BuildingRoad > 0)       buildRoad = settlers.BuildingRoad;
					if (settlers.BuildingIrrigation > 0) buildIrr  = settlers.BuildingIrrigation;
					if (settlers.BuildingMine > 0)       buildMine = settlers.BuildingMine;
					if (settlers.BuildingFortress > 0)   buildFort = settlers.BuildingFortress;
				}
				int? fuelLeft = null;
				if (unit is BaseUnitAir airUnit && airUnit.FuelLeft < airUnit.TotalFuel)
					fuelLeft = airUnit.FuelLeft;

				units.Add(new CosUnit
				{
					TypeId             = (int)unit.Type,
					X                  = unit.X,
					Y                  = unit.Y,
					Status             = unit.Status,
					MovesLeft          = unit.MovesLeft,
					PartMoves          = unit.PartMoves,
					Owner              = unit.Owner,
					GotoX              = gx,
					GotoY              = gy,
					HomeCityId         = homeCityId,
					BuildingRoad       = buildRoad,
					BuildingIrrigation = buildIrr,
					BuildingMine       = buildMine,
					BuildingFortress   = buildFort,
					FuelLeft           = fuelLeft
				});
			}

			// Players
			var players = new List<CosPlayer>();
			for (int p = 0; p < playerCount; p++)
			{
				var player = _players[p];
				var vis = new bool[Map.WIDTH, Map.HEIGHT];
				for (int x = 0; x < Map.WIDTH; x++)
				for (int y = 0; y < Map.HEIGHT; y++)
					vis[x, y] = player.Visible(x, y);

				players.Add(new CosPlayer
				{
					CivilizationId   = player.Civilization.Id,
					LeaderName       = player.LeaderName,
					CitizenName      = player.TribeName,
					CivilizationName = player.TribeNamePlural,
					Gold             = player.Gold,
					Science          = player.Science,
					TaxRate          = player.TaxesRate,
					ScienceRate      = player.ScienceRate,
					StartX           = player.StartX,
					GovernmentId     = player.Government?.Id ?? 0,
					Advances         = player.Advances.Select(a => (int)a.Id).ToArray(),
					FutureTechs      = player.FutureTechs,
					AtWarWith        = Enumerable.Range(0, playerCount)
				                   .Where(j => j != p && _players[p].IsAtWar(_players[j]))
				                   .ToArray(),
					Visibility       = PackVisibility(vis)
				});
			}

			// Replay data
			var replay = _replayData.Select(r =>
			{
				switch (r)
				{
					case ReplayData.CivilizationDestroyed cd:
						return new CosReplayEntry { Type = "CivilizationDestroyed", Turn = r.Turn, DestroyedId = cd.DestroyedId, DestroyedById = cd.DestroyedById };
					case ReplayData.CityBuilt cb:
						return new CosReplayEntry { Type = "CityBuilt", Turn = r.Turn, OwnerId = cb.OwnerId, CityId = cb.CityId, CityNameId = cb.CityNameId, X = cb.X, Y = cb.Y };
					case ReplayData.CityDestroyed cd2:
						return new CosReplayEntry { Type = "CityDestroyed", Turn = r.Turn, CityId = cd2.CityId, CityNameId = cd2.CityNameId, X = cd2.X, Y = cd2.Y };
					default:
						return null;
				}
			}).Where(e => e != null).ToList();

			string displayName = BuildDisplayName();
			var cos = new CosFile
			{
				Version = "1.0",
				Meta = new CosMeta { Name = displayName, Turn = (int)_gameTurn, Difficulty = _difficulty },
				Game = new CosGame
				{
					Turn          = _gameTurn,
					HumanPlayer   = PlayerNumber(HumanPlayer),
					Difficulty    = _difficulty,
					Competition   = _competition,
					AnthologyTurn = _anthologyTurn,
					CityNames     = CityNames,
					AdvanceOrigin = advanceOrigin,
					Options       = new CosOptions
					{
						InstantAdvice  = InstantAdvice,
						EndOfTurn      = EndOfTurn,
						Animations     = Animations,
						Sound          = Sound,
						EnemyMoves     = EnemyMoves,
						CivilopediaText = CivilopediaText,
						Palace         = Palace
					},
					SpaceshipLaunch      = SpaceshipLaunchTurn.ToArray(),
					SpaceshipArrival     = SpaceshipArrivalTurn.ToArray(),
					SpaceshipStructural  = SpaceshipStructural.ToArray(),
					SpaceshipComponent   = SpaceshipComponent.ToArray(),
					SpaceshipModule      = SpaceshipModule.ToArray(),
					ReplayData           = replay
				},
				Map     = Map.Instance.SaveToCos(),
				Players = players,
				Cities  = cities,
				Units   = units
			};

			File.WriteAllText(cosFile, CosSerializer.Serialize(cos));
		}

		// ── Load ────────────────────────────────────────────────────────────────

		public static bool LoadCos(string cosFile)
		{
			if (_instance != null)
			{
				Log("ERROR: Game instance already exists");
				return false;
			}
			try
			{
				var text = File.ReadAllText(cosFile);
				var cos  = CosSerializer.Deserialize(text);
				_instance = new Game(cos);
				WLTKNotifications.Clear();
				Log($"Game loaded from COS (difficulty: {_instance._difficulty}, competition: {_instance._competition})");
				return true;
			}
			catch (Exception ex)
			{
				Log($"LoadCos failed: {ex}");
				return false;
			}
		}

		private Game(CosFile cos)
		{
			var g = cos.Game;
			_difficulty  = g.Difficulty;
			_competition = g.Competition;
			_players     = new Player[_competition + 1];
			_cities      = new List<City>();
			_units       = new List<IUnit>();

			// Map must come first so tiles exist when cities set resource tiles
			Map.Instance.LoadFromCos(cos.Map);

			// Players
			var advanceFirst = g.AdvanceOrigin ?? new Dictionary<int, int>();
			for (int i = 0; i < _players.Length; i++)
			{
				var pd  = cos.Players[i];
				var civ = Common.Civilizations.FirstOrDefault(c => c.Id == pd.CivilizationId)
				          ?? Common.Civilizations.Where(c => c.PreferredPlayerNumber == i).First();

				var player = (_players[i] = new Player(civ, pd.LeaderName, pd.CitizenName, pd.CivilizationName));
				player.Destroyed += PlayerDestroyed;
				player.Gold         = (short)pd.Gold;
				player.Science      = (short)pd.Science;
				player.TaxesRate    = pd.TaxRate;
				player.LuxuriesRate = 10 - pd.ScienceRate - pd.TaxRate;
				player.StartX       = (short)pd.StartX;
				player.Government   = Reflect.GetGovernments().FirstOrDefault(gov => gov.Id == pd.GovernmentId);

				// Visibility
				if (!string.IsNullOrEmpty(pd.Visibility))
				{
					var vis = UnpackVisibility(pd.Visibility);
					for (int x = 0; x < Map.WIDTH; x++)
					for (int y = 0; y < Map.HEIGHT; y++)
					{
						if (!vis[x, y]) continue;
						if (i == 0 && Map[x, y].Hut) Map[x, y].Hut = false;
						player.Explore(x, y, 0);
					}
				}

				// Advances
				var advanceIds = pd.Advances ?? Array.Empty<int>();
				foreach (var adv in Common.Advances.Where(a => advanceIds.Contains(a.Id)))
				{
					player.AddAdvance(adv, false);
					if (advanceFirst.TryGetValue(adv.Id, out int civId) && civId == civ.Id)
						SetAdvanceOrigin(adv, player);
				}
			}

			// War state
			for (int i = 0; i < _players.Length; i++)
			{
				var warList = cos.Players[i].AtWarWith;
				if (warList == null) continue;
				foreach (int j in warList)
					_players[i].SetAtWar((byte)j, true);
			}

			// Future techs and human player
			for (int i = 0; i < _players.Length; i++)
				_players[i].SetFutureTechs(cos.Players[i].FutureTechs);

			GameTurn     = (ushort)g.Turn;
			CityNames    = g.CityNames;
			HumanPlayer  = _players[g.HumanPlayer];
			HumanPlayer.CurrentResearch = null; // set below after advances loaded
			_anthologyTurn = (ushort)g.AnthologyTurn;
			_currentPlayer = g.HumanPlayer;

			// Cities
			var cityById = new Dictionary<int, City>();
			for (int ci = 0; ci < (cos.Cities?.Count ?? 0); ci++)
			{
				var cd   = cos.Cities[ci];
				if (cd.X < 0 || cd.X >= Map.WIDTH || cd.Y < 0 || cd.Y >= Map.HEIGHT)
				{
					Log($"Skipping corrupt city id={cd.Id} at ({cd.X},{cd.Y}) — out of bounds for {Map.WIDTH}x{Map.HEIGHT} map");
					continue;
				}
				var city = new City((byte)cd.Owner)
				{
					X       = (byte)cd.X,
					Y       = (byte)cd.Y,
					NameId  = cd.NameId,
					Size    = (byte)cd.Size,
					Food    = cd.Food,
					Shields = cd.Shields
				};
				var prod = Reflect.GetProduction().FirstOrDefault(p => p.GetType().Name == cd.Production);
				if (prod != null) city.SetProduction(prod);
				city.SetResourceTiles(cd.ResourceTiles?.Select(b => (byte)b).ToArray() ?? Array.Empty<byte>());

				foreach (int bId in cd.Buildings ?? Array.Empty<int>())
					city.AddBuilding(Common.Buildings.FirstOrDefault(b => b.Id == bId));

				foreach (int wId in cd.Wonders ?? Array.Empty<int>())
				{
					var wonder = Common.Wonders.FirstOrDefault(w => w.Id == wId);
					if (wonder != null) city.AddWonder(wonder);
				}

				foreach (var q in cd.ProductionQueue ?? Array.Empty<string>())
				{
					var item = Reflect.GetProduction().FirstOrDefault(p => p.GetType().Name == q);
					if (item != null) city.EnqueueProduction(item);
				}

				foreach (int unitTypeId in cd.FortifiedUnits ?? Array.Empty<int>())
				{
					var unit = CreateUnit((UnitType)unitTypeId, city.X, city.Y);
					unit.Status = (byte)(1 << 3); // fortified
					unit.Owner  = city.Owner;
					unit.SetHome(city);
					_units.Add(unit);
				}

				city.WasInDisorder = cd.WasInDisorder ?? false;
				city.WasWeLoveKing = cd.WasWeLoveKing ?? false;
				cityById[cd.Id] = city;
				_cities.Add(city);
			}

			// Restore trade routes now that all cities exist
			for (int ci = 0; ci < (cos.Cities?.Count ?? 0); ci++)
			{
				var cd = cos.Cities[ci];
				if (cd.TradeRoutes == null || !cityById.TryGetValue(cd.Id, out var city)) continue;
				foreach (var tr in cd.TradeRoutes)
				{
					var partner = _cities.FirstOrDefault(c => c.X == tr.PartnerX && c.Y == tr.PartnerY);
					if (partner != null) city.AddTradeRoute(partner, tr.Commodity);
				}
			}

			// Units
			foreach (var ud in cos.Units ?? Enumerable.Empty<CosUnit>())
			{
				var unit = CreateUnit((UnitType)ud.TypeId, ud.X, ud.Y);
				if (unit == null) continue;
				unit.Status    = (byte)ud.Status;
				unit.Owner     = (byte)ud.Owner;
				unit.MovesLeft = (byte)ud.MovesLeft;
				unit.PartMoves = (byte)ud.PartMoves;
				if (ud.GotoX.HasValue) unit.Goto = new Point(ud.GotoX.Value, ud.GotoY ?? 0);
				if (ud.HomeCityId >= 0 && cityById.TryGetValue(ud.HomeCityId, out var homeCity))
					unit.SetHome(homeCity);
				if (unit is Settlers s && (ud.BuildingRoad > 0 || ud.BuildingIrrigation > 0 || ud.BuildingMine > 0 || ud.BuildingFortress > 0))
					s.SetBuildProgress(ud.BuildingRoad ?? 0, ud.BuildingIrrigation ?? 0, ud.BuildingMine ?? 0, ud.BuildingFortress ?? 0);
				if (unit is BaseUnitAir airU && ud.FuelLeft.HasValue)
					airU.FuelLeft = ud.FuelLeft.Value;
				_units.Add(unit);
			}

			// Spaceship
			if (g.SpaceshipLaunch != null)
				for (int i = 0; i < Math.Min(g.SpaceshipLaunch.Length, 8); i++)
					SpaceshipLaunchTurn[i] = g.SpaceshipLaunch[i];
			if (g.SpaceshipArrival != null)
				for (int i = 0; i < Math.Min(g.SpaceshipArrival.Length, 8); i++)
					SpaceshipArrivalTurn[i] = g.SpaceshipArrival[i];
			if (g.SpaceshipStructural != null)
				for (int i = 0; i < Math.Min(g.SpaceshipStructural.Length, 8); i++)
					SpaceshipStructural[i] = g.SpaceshipStructural[i];
			if (g.SpaceshipComponent != null)
				for (int i = 0; i < Math.Min(g.SpaceshipComponent.Length, 8); i++)
					SpaceshipComponent[i] = g.SpaceshipComponent[i];
			if (g.SpaceshipModule != null)
				for (int i = 0; i < Math.Min(g.SpaceshipModule.Length, 8); i++)
					SpaceshipModule[i] = g.SpaceshipModule[i];
			// Migrate: old COS saves stored SS parts as city buildings; convert and strip them.
			if (g.SpaceshipStructural == null && g.SpaceshipComponent == null && g.SpaceshipModule == null)
			{
				foreach (City city in _cities)
				{
					int p = city.Owner;
					SpaceshipStructural[p] += city.Buildings.Count(b => b is SSStructural);
					SpaceshipComponent[p]  += city.Buildings.Count(b => b is SSComponent);
					SpaceshipModule[p]     += city.Buildings.Count(b => b is SSModule);
					city.RemoveBuilding<SSStructural>();
					city.RemoveBuilding<SSComponent>();
					city.RemoveBuilding<SSModule>();
				}
			}

			// Replay data
			foreach (var re in g.ReplayData ?? Enumerable.Empty<CosReplayEntry>())
			{
				switch (re.Type)
				{
					case "CivilizationDestroyed":
						_replayData.Add(new ReplayData.CivilizationDestroyed(re.Turn, re.DestroyedId, re.DestroyedById));
						break;
					case "CityBuilt":
						_replayData.Add(new ReplayData.CityBuilt(re.Turn, (byte)re.OwnerId, re.CityId, re.CityNameId, re.X, re.Y));
						break;
					case "CityDestroyed":
						_replayData.Add(new ReplayData.CityDestroyed(re.Turn, re.CityId, re.CityNameId, re.X, re.Y));
						break;
				}
			}

			// Game options
			var opt = g.Options ?? new CosOptions();
			InstantAdvice  = (Settings.InstantAdvice  == GameOption.On)  || (Settings.InstantAdvice  == GameOption.Default && opt.InstantAdvice);
			EndOfTurn      = (Settings.EndOfTurn      == GameOption.On)  || (Settings.EndOfTurn      == GameOption.Default && opt.EndOfTurn);
			Animations     = (Settings.Animations     != GameOption.Off) && (Settings.Animations     != GameOption.Default || opt.Animations);
			Sound          = (Settings.Sound          != GameOption.Off) && (Settings.Sound          != GameOption.Default || opt.Sound);
			EnemyMoves     = (Settings.EnemyMoves     != GameOption.Off) && (Settings.EnemyMoves     != GameOption.Default || opt.EnemyMoves);
			CivilopediaText= (Settings.CivilopediaText!= GameOption.Off) && (Settings.CivilopediaText!= GameOption.Default || opt.CivilopediaText);
			Palace         = (Settings.Palace         != GameOption.Off) && (Settings.Palace         != GameOption.Default || opt.Palace);

			// Active unit
			for (int i = 0; i < _units.Count; i++)
			{
				if (_units[i].Owner != g.HumanPlayer || _units[i].Busy) continue;
				_activeUnit = i;
				if (_units[i].MovesLeft > 0) break;
			}
		}

		// ── Helpers ─────────────────────────────────────────────────────────────

		private string BuildDisplayName()
		{
			var human = HumanPlayer;
			string year = Common.YearString(_gameTurn);
			string rank = Common.DifficultyName(_difficulty);
			return $"{rank} {human.LeaderName}, {human.TribeNamePlural} / {year}";
		}

		private static string PackVisibility(bool[,] vis)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			var bytes = new byte[(w * h + 7) / 8];
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				int idx = y * w + x;
				if (vis[x, y]) bytes[idx >> 3] |= (byte)(1 << (idx & 7));
			}
			return Convert.ToBase64String(bytes);
		}

		private static bool[,] UnpackVisibility(string b64)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			var bytes = Convert.FromBase64String(b64);
			var vis   = new bool[w, h];
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
			{
				int idx = y * w + x;
				vis[x, y] = (bytes[idx >> 3] & (1 << (idx & 7))) != 0;
			}
			return vis;
		}
	}
}
