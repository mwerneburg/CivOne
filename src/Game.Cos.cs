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
			var playerCount = _players.Count;

			// Advance origin: advance-id → civilization-id (not player-index)
			var advanceOrigin = new Dictionary<int, int>();
			foreach (var kv in _advanceOrigin)
				advanceOrigin[kv.Key] = kv.Value;

			// Wonders per city: build a lookup of wonder.Id → city index
			var wonderCityIndex = new int[Common.Wonders.Length > 0 ? Common.Wonders.Max(w => w.Id) + 1 : 1];
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
					OriginalOwner  = city.OriginalOwner != city.Owner ? (int?)city.OriginalOwner : null,
					Size           = city.Size,
					Food           = city.Food,
					Shields        = city.Shields,
					Production     = city.CurrentProduction?.GetType().Name,
					ProductionQueue = city.ProductionQueue.Select(p => p.GetType().Name).ToArray(),
					Buildings      = city.Buildings.Select(b => (int)b.Id).ToArray(),
					Wonders        = wonders,
					ResourceTiles  = city.GetResourceTiles().Select(b => (int)b).ToArray(),
					TradeRoutes    = tradeRoutes,
					DisorderTurns  = city.DisorderTurns  > 0 ? (int?)city.DisorderTurns : null,
					WasWeLoveKing  = city.WasWeLoveKing  ? (bool?)true : null,
					TechStolen     = city.TechStolen     ? (bool?)true : null
				});
			}

			// City set used to identify home cities for units
			var cityByRef = _cities.Select((c, i) => (city: c, idx: i)).ToDictionary(t => t.city, t => t.idx);

			var units = new List<CosUnit>();
			foreach (var unit in _units)
			{
				int? gx = unit.Goto.IsEmpty ? (int?)null : unit.Goto.X;
				int? gy = unit.Goto.IsEmpty ? (int?)null : unit.Goto.Y;
				int homeCityId = unit.Home is not null && cityByRef.TryGetValue(unit.Home, out var idx) ? idx : -1;

				int? buildRoad = null, buildIrr = null, buildMine = null, buildFort = null;
				int? roadToX = null, roadToY = null;
				if (unit is Settlers settlers)
				{
					if (settlers.BuildingRoad > 0)       buildRoad = settlers.BuildingRoad;
					if (settlers.BuildingIrrigation > 0) buildIrr  = settlers.BuildingIrrigation;
					if (settlers.BuildingMine > 0)       buildMine = settlers.BuildingMine;
					if (settlers.BuildingFortress > 0)   buildFort = settlers.BuildingFortress;
					if (!settlers.RoadTo.IsEmpty) { roadToX = settlers.RoadTo.X; roadToY = settlers.RoadTo.Y; }
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
					RoadToX            = roadToX,
					RoadToY            = roadToY,
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
					CurrentResearch  = player.CurrentResearch?.Id,
					FutureTechs      = player.FutureTechs,
					MilestoneScore   = player.MilestoneScore,
					AtWarWith        = Enumerable.Range(0, playerCount)
				                   .Where(j => j != p && _players[p].IsAtWar(_players[j]))
				                   .ToArray(),
					Embassies        = Enumerable.Range(0, playerCount)
				                   .Where(j => j != p && _players[p].HasEmbassy(_players[j]))
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
					case ReplayData.CityCaptured cc:
						return new CosReplayEntry { Type = "CityCaptured", Turn = r.Turn, OwnerId = cc.NewOwnerId, CityId = cc.CityId, CityNameId = cc.CityNameId, X = cc.X, Y = cc.Y };
					case ReplayData.CityDestroyed cd2:
						return new CosReplayEntry { Type = "CityDestroyed", Turn = r.Turn, CityId = cd2.CityId, CityNameId = cd2.CityNameId, X = cd2.X, Y = cd2.Y };
					case ReplayData.WonderBuilt wb:
						return new CosReplayEntry { Type = "WonderBuilt", Turn = r.Turn, OwnerId = wb.OwnerId, X = wb.X, Y = wb.Y, WonderName = wb.WonderName };
					case ReplayData.TechDiscovered td:
						return new CosReplayEntry { Type = "TechDiscovered", Turn = r.Turn, OwnerId = td.OwnerId, TechName = td.TechName };
					case ReplayData.UnitBuilt ub:
						return new CosReplayEntry { Type = "UnitBuilt", Turn = r.Turn, OwnerId = ub.OwnerId, UnitName = ub.UnitName };
					case ReplayData.BuildingBuilt bb:
						return new CosReplayEntry { Type = "BuildingBuilt", Turn = r.Turn, OwnerId = bb.OwnerId, BuildingName = bb.BuildingName };
					default:
						return null;
				}
			}).Where(e => e is not null).ToList();

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
						Palace         = Palace,
						Circuses       = Circuses,
					Barricades     = Barricades
					},
					SpaceshipLaunch      = SpaceshipLaunchTurn.ToArray(),
					SpaceshipArrival     = SpaceshipArrivalTurn.ToArray(),
					SpaceshipStructural  = SpaceshipStructural.ToArray(),
					SpaceshipComponent   = SpaceshipComponent.ToArray(),
					SpaceshipModule      = SpaceshipModule.ToArray(),
					FirstExplorer        = PackFirstExplorer(FirstExplorer),
					MapRevealedNotified  = MapRevealedNotified,
					SETISignalTurn          = SETISignalTurn,
					SETISignalReceived      = SETISignalReceived,
					VisitorArchetype        = (int)VisitorType,
					TauCetiEscalationTurn   = TauCetiEscalationTurn,
					ProbeDispatched         = ProbeDispatched,
					ProbeDispatchTurn       = ProbeDispatchTurn,
					ProbeInterimPhase       = ProbeInterimPhase,
					ProbeGrantedAdvanceIds  = ProbeGrantedAdvanceIds.Length > 0 ? ProbeGrantedAdvanceIds : null,
					ProbeOutcomeTier        = ProbeOutcomeTier,
					OlvirArrivalTurn        = OlvirArrivalTurn,
					OlvirImprovements       = OlvirImprovements.Count > 0
					                          ? OlvirImprovements.Select(kv => new[] { kv.Key.x, kv.Key.y, (int)kv.Value }).ToList()
					                          : null,
					DomeAssignments         = DomeAssignments.Select(kv => new[] { (int)kv.Key, (int)kv.Value }).ToList(),
					DomeVictoryFired        = _domeVictoryFired,
					ScoreHistory         = _scoreHistory.Select(s => s.ToList()).ToList(),
					ReplayData           = replay,
					Transmissions        = Transmissions.Select(t => new CosTransmission { Type = t.Type, Year = t.Year }).ToList()
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
			if (_instance is not null)
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
				DecisionLogger.BeginGame();
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
			int slotCount = cos.Players.Count;
			_players  = new List<Player>(Enumerable.Repeat<Player>(null, slotCount));
			_cities   = new List<City>();
			_units    = new List<IUnit>();
			SpaceshipLaunchTurn  = new int[slotCount];
			SpaceshipArrivalTurn = new int[slotCount];
			SpaceshipStructural  = new int[slotCount];
			SpaceshipComponent   = new int[slotCount];
			SpaceshipModule      = new int[slotCount];

			// Map must come first so tiles exist when cities set resource tiles
			Map.Instance.LoadFromCos(cos.Map);
			Map.Instance.CalculateContinentSize(); // not persisted in save — recompute from tile topology

			// Players
			var advanceFirst = g.AdvanceOrigin ?? new Dictionary<int, int>();
			for (int i = 0; i < _players.Count; i++)
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
				if (pd.CurrentResearch.HasValue)
					player.CurrentResearch = Common.Advances.FirstOrDefault(a => a.Id == pd.CurrentResearch.Value);
			}

			// War state
			for (int i = 0; i < _players.Count; i++)
			{
				var warList = cos.Players[i].AtWarWith;
				if (warList is null) continue;
				foreach (int j in warList)
					_players[i].SetAtWar((byte)j, true);
			}

			// Embassies
			for (int i = 0; i < _players.Count; i++)
			{
				var embassyList = cos.Players[i].Embassies;
				if (embassyList is null) continue;
				foreach (int j in embassyList.Where(j => j >= 0 && j < _players.Count && _players[j] is not null))
					_players[i].EstablishEmbassy(_players[j]);
			}

			// Future techs, milestone scores, and human player
			for (int i = 0; i < _players.Count; i++)
			{
				_players[i].SetFutureTechs(cos.Players[i].FutureTechs);
				_players[i].SetMilestoneScore(cos.Players[i].MilestoneScore ?? 0);
			}

			MapRevealedNotified   = g.MapRevealedNotified;
			SETISignalTurn        = g.SETISignalTurn;
			SETISignalReceived    = g.SETISignalReceived;
			VisitorType           = (VisitorArchetype)g.VisitorArchetype;
			TauCetiEscalationTurn = g.TauCetiEscalationTurn;
			ProbeDispatched         = g.ProbeDispatched;
			ProbeDispatchTurn       = g.ProbeDispatchTurn;
			ProbeInterimPhase       = g.ProbeInterimPhase;
			ProbeGrantedAdvanceIds  = g.ProbeGrantedAdvanceIds ?? System.Array.Empty<int>();
			ProbeOutcomeTier        = g.ProbeOutcomeTier;
			OlvirArrivalTurn        = g.OlvirArrivalTurn;
			if (g.OlvirImprovements is not null)
				foreach (var triple in g.OlvirImprovements)
					if (triple.Length == 3)
						OlvirImprovements[(triple[0], triple[1])] = (Enums.OlvirImprovementType)triple[2];
			_domeVictoryFired     = g.DomeVictoryFired;
			if (g.DomeAssignments is not null)
				foreach (var pair in g.DomeAssignments)
					if (pair.Length == 2)
						DomeAssignments[(byte)pair[0]] = (Enums.Wonder)pair[1];
			if (g.Transmissions is not null)
				foreach (var t in g.Transmissions)
					Transmissions.Add(new TransmissionRecord { Type = t.Type, Year = t.Year });
			if (g.ScoreHistory is not null)
				foreach (var entry in g.ScoreHistory)
					_scoreHistory.Add(entry.ToArray());

			// Exploration credits
			if (!string.IsNullOrEmpty(g.FirstExplorer))
			{
				FirstExplorer = UnpackFirstExplorer(g.FirstExplorer);
				for (int x = 0; x < Map.WIDTH; x++)
				for (int y = 0; y < Map.HEIGHT; y++)
				{
					byte pIdx = FirstExplorer[x, y];
					if (pIdx < _players.Count)
						_players[pIdx].ExplorationCredits++;
				}
			}

			GameTurn     = (ushort)g.Turn;
			// Ensure a pending SETI signal fires at least 5 full turns from the loaded state,
			// preventing a save made mid-countdown from firing sooner than expected on reload.
			if (SETISignalTurn > 0)
				SETISignalTurn = Math.Max(SETISignalTurn, (uint)(_gameTurn + 5));
			if (TauCetiEscalationTurn > 0 && !ProbeDispatched)
				TauCetiEscalationTurn = Math.Max(TauCetiEscalationTurn, (uint)(_gameTurn + 5));
			CityNames    = g.CityNames;
			// Extend CityNames if this save pre-dates a civilization addition (e.g. Olvir).
			{
				string[] canonical = Common.AllCityNames.ToArray();
				if (CityNames.Length < canonical.Length)
				{
					var extended = new string[canonical.Length];
					CityNames.CopyTo(extended, 0);
					for (int i = CityNames.Length; i < canonical.Length; i++)
						extended[i] = canonical[i];
					CityNames = extended;
				}
			}
			HumanPlayer  = _players[g.HumanPlayer];
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
					X             = (byte)cd.X,
					Y             = (byte)cd.Y,
					NameId        = cd.NameId,
					OriginalOwner = (byte)(cd.OriginalOwner ?? cd.Owner),
					Size          = (byte)cd.Size,
					Food          = cd.Food,
					Shields       = cd.Shields
				};
				var prod = Reflect.GetProduction().FirstOrDefault(p => p.GetType().Name == cd.Production);
				if (prod is not null) city.SetProduction(prod);
				city.SetResourceTiles(cd.ResourceTiles?.Select(b => (byte)b).ToArray() ?? Array.Empty<byte>());

				foreach (int bId in cd.Buildings ?? Array.Empty<int>())
					city.AddBuilding(Common.Buildings.FirstOrDefault(b => b.Id == bId));

				foreach (int wId in cd.Wonders ?? Array.Empty<int>())
				{
					var wonder = Common.Wonders.FirstOrDefault(w => w.Id == wId);
					if (wonder is not null) city.AddWonder(wonder);
				}

				foreach (var q in cd.ProductionQueue ?? Array.Empty<string>())
				{
					var item = Reflect.GetProduction().FirstOrDefault(p => p.GetType().Name == q);
					if (item is not null) city.EnqueueProduction(item);
				}

				foreach (int unitTypeId in cd.FortifiedUnits ?? Array.Empty<int>())
				{
					var unit = CreateUnit((UnitType)unitTypeId, city.X, city.Y);
					unit.Status = (byte)(1 << 3); // fortified
					unit.Owner  = city.Owner;
					unit.SetHome(city);
					_units.Add(unit);
				}

				city.DisorderTurns = cd.DisorderTurns ?? ((cd.WasInDisorder ?? false) ? 1 : 0);
				city.WasWeLoveKing = cd.WasWeLoveKing ?? false;
				city.TechStolen    = cd.TechStolen    ?? false;
				cityById[cd.Id] = city;
				_cities.Add(city);
			}

			// Restore trade routes now that all cities exist
			for (int ci = 0; ci < (cos.Cities?.Count ?? 0); ci++)
			{
				var cd = cos.Cities[ci];
				if (cd.TradeRoutes is null || !cityById.TryGetValue(cd.Id, out var city)) continue;
				foreach (var tr in cd.TradeRoutes)
				{
					var partner = _cities.FirstOrDefault(c => c.X == tr.PartnerX && c.Y == tr.PartnerY);
					if (partner is not null) city.AddTradeRoute(partner, tr.Commodity);
				}
			}

			// Units
			foreach (var ud in cos.Units ?? Enumerable.Empty<CosUnit>())
			{
				var unit = CreateUnit((UnitType)ud.TypeId, ud.X, ud.Y);
				if (unit is null) continue;
				unit.Status    = (byte)ud.Status;
				unit.Owner     = (byte)ud.Owner;
				unit.MovesLeft = (byte)ud.MovesLeft;
				unit.PartMoves = (byte)ud.PartMoves;
				if (ud.GotoX.HasValue) unit.Goto = new Point(ud.GotoX.Value, ud.GotoY ?? 0);
				if (ud.HomeCityId >= 0 && cityById.TryGetValue(ud.HomeCityId, out var homeCity))
					unit.SetHome(homeCity);
				if (unit is Settlers s)
				{
					if (ud.BuildingRoad > 0 || ud.BuildingIrrigation > 0 || ud.BuildingMine > 0 || ud.BuildingFortress > 0)
						s.SetBuildProgress(ud.BuildingRoad ?? 0, ud.BuildingIrrigation ?? 0, ud.BuildingMine ?? 0, ud.BuildingFortress ?? 0);
					if (ud.RoadToX.HasValue) s.RoadTo = new Point(ud.RoadToX.Value, ud.RoadToY ?? 0);
				}
				if (unit is BaseUnitAir airU && ud.FuelLeft.HasValue)
					airU.FuelLeft = ud.FuelLeft.Value;
				_units.Add(unit);
			}

			// Spaceship
			if (g.SpaceshipLaunch is not null)
				for (int i = 0; i < Math.Min(g.SpaceshipLaunch.Length, _players.Count); i++)
					SpaceshipLaunchTurn[i] = g.SpaceshipLaunch[i];
			if (g.SpaceshipArrival is not null)
				for (int i = 0; i < Math.Min(g.SpaceshipArrival.Length, _players.Count); i++)
					SpaceshipArrivalTurn[i] = g.SpaceshipArrival[i];
			if (g.SpaceshipStructural is not null)
				for (int i = 0; i < Math.Min(g.SpaceshipStructural.Length, _players.Count); i++)
					SpaceshipStructural[i] = g.SpaceshipStructural[i];
			if (g.SpaceshipComponent is not null)
				for (int i = 0; i < Math.Min(g.SpaceshipComponent.Length, _players.Count); i++)
					SpaceshipComponent[i] = g.SpaceshipComponent[i];
			if (g.SpaceshipModule is not null)
				for (int i = 0; i < Math.Min(g.SpaceshipModule.Length, _players.Count); i++)
					SpaceshipModule[i] = g.SpaceshipModule[i];
			// Migrate: old COS saves stored SS parts as city buildings; convert and strip them.
			if (g.SpaceshipStructural is null && g.SpaceshipComponent is null && g.SpaceshipModule is null)
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
					case "CityCaptured":
						_replayData.Add(new ReplayData.CityCaptured(re.Turn, re.CityId, re.CityNameId, re.X, re.Y, (byte)re.OwnerId));
						break;
					case "CityDestroyed":
						_replayData.Add(new ReplayData.CityDestroyed(re.Turn, re.CityId, re.CityNameId, re.X, re.Y));
						break;
					case "WonderBuilt":
						_replayData.Add(new ReplayData.WonderBuilt(re.Turn, (byte)re.OwnerId, re.WonderName ?? "", re.X, re.Y));
						break;
					case "TechDiscovered":
						_replayData.Add(new ReplayData.TechDiscovered(re.Turn, (byte)re.OwnerId, re.TechName ?? ""));
						break;
					case "UnitBuilt":
						_replayData.Add(new ReplayData.UnitBuilt(re.Turn, (byte)re.OwnerId, re.UnitName ?? ""));
						break;
					case "BuildingBuilt":
						_replayData.Add(new ReplayData.BuildingBuilt(re.Turn, (byte)re.OwnerId, re.BuildingName ?? ""));
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
			Circuses       = opt.Circuses   ?? true;
			Barricades     = opt.Barricades ?? true;

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

		private static string PackFirstExplorer(byte[,] fe)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			var bytes = new byte[w * h];
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				bytes[y * w + x] = fe[x, y];
			return Convert.ToBase64String(bytes);
		}

		private static byte[,] UnpackFirstExplorer(string b64)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			var bytes = Convert.FromBase64String(b64);
			var fe = new byte[w, h];
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				fe[x, y] = (y * w + x < bytes.Length) ? bytes[y * w + x] : (byte)255;
			return fe;
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
