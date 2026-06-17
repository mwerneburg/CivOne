// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Leaders;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

using Democratic = CivOne.Governments.Democracy;

using static CivOne.Enums.DevelopmentLevel;

namespace CivOne
{
	internal partial class AI : BaseInstance
	{
		public Player Player { get; }
		public ILeader Leader => Player.Civilization.Leader;

		// The city this civ is currently marshalling forces to attack.
		private City? _attackTarget;

		// War-state tracking for peace initiatives.
		private int _turnsAtWar      = 0;
		private int _peacetimeCities = 0; // city count when we were last at peace

		// Grievance-demand cooldown: turn on which the last GrievancePack was issued.
		internal int LastGrievanceTurn = -50;

		internal void Move(IUnit unit)
		{
			if (Player != unit.Owner) return;

			string gotoStr = unit.Goto.IsEmpty ? "empty" : $"({unit.Goto.X},{unit.Goto.Y})";
			//Log($"[AI.Move] {Player.LeaderName}(P{Game.PlayerNumber(Player)}) {unit.GetType().Name} ({unit.X},{unit.Y}) ML={unit.MovesLeft} PM={unit.PartMoves} Moving={unit.Moving} Goto={gotoStr}");

			if (unit.Owner == 0)
			{
				BarbarianMove(unit);
				return;
			}
			
			if (unit is Settlers && Player.Civilization is Olvir)
			{
				ITile tile = unit.Tile;

				// Place an Olvir improvement on the current tile if it's unimproved land.
				if (!tile.IsOcean && tile.City is null
				    && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
				{
					Game.OlvirImprovements[(tile.X, tile.Y)] = OlvirImprovementFor(tile);
					unit.Goto = System.Drawing.Point.Empty;
					unit.SkipTurn();
					return;
				}

				// Navigate to the next unimproved site near an Olvir city.
				if (unit.Goto.IsEmpty)
				{
					ITile? next = BestOlvirImproveSite(unit);
					if (next is not null && (next.X != tile.X || next.Y != tile.Y))
						unit.Goto = new System.Drawing.Point(next.X, next.Y);
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? step = Common.GotoStep(unit);
					if (step is null) { unit.Goto = System.Drawing.Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(step.X - unit.X, step.Y - unit.Y))
					{
						unit.Goto = System.Drawing.Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
				unit.SkipTurn();
				return;
			}

			if (unit is Settlers)
			{
				ITile tile = unit.Tile;

				// Any habitable land tile is a valid city site — Desert, Hills, Jungle etc.
				// are all legal in Civ 1. Restricting to Grassland/Plains was causing settlers
				// to mill endlessly after the new arid-interior map generation.
				bool validCity = (tile.City is null) && (
					(!tile.IsOcean && !(tile is Arctic) && !(tile is Mountains)) ||
					(tile.IsOcean && Player.HasAdvance<AquaticColonization>()));
				bool validIrrigation = (tile is Grassland || tile is River || tile is Plains || tile is Desert) && (tile.City is null) && (!tile.Mine) && (!tile.Irrigation)
					&& tile.CrossTiles().Any(x => x.Irrigation || x is River || x is Swamp || (x.IsOcean && Map.Instance.IsFreshwaterAt(x.X, x.Y)));
				bool validMine = (tile is Mountains || tile is Hills) && (tile.City is null) && (!tile.Mine) && (!tile.Irrigation);
				// Mirror Settlers.BuildRoad's eligibility checks: a brand-new road on a River
				// tile requires Bridge Building. Without this guard the AI loops indefinitely
				// (validRoad → enqueue BuildRoad → silent fail → SkipTurn → repeat).
				bool canNewRoadHere = (!tile.Road && !tile.RailRoad)
					&& (!(tile is River) || Player.HasAdvance<BridgeBuilding>());
				bool validRoad = (tile.City is null) && !tile.TransportTube && (
					canNewRoadHere ||
					(tile.Road && !tile.RailRoad && Player.HasAdvance<RailRoad>()) ||
					(tile.RailRoad && Player.HasAdvance<TransitConduit>()));
				bool validCanopy = Player.HasAdvance<CanopyCultivation>() && (tile is Forest || tile is Jungle) && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y));
				bool validAquafarm = Player.HasAdvance<BioplexEngineering>() && !tile.IsOcean && tile.GetBorderTiles().Any(t => t.IsOcean) && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y));
				int nearestCity = 255;
				int nearestOwnCity = 255;

				City[] cities = Game.GetCities();
				if (cities.Any()) nearestCity = cities.Min(x => Common.DistanceToTile(x.X, x.Y, tile.X, tile.Y));
				City[] ownCities = cities.Where(x => x.Owner == unit.Owner).ToArray();
				if (ownCities.Any()) nearestOwnCity = ownCities.Min(x => Common.DistanceToTile(x.X, x.Y, tile.X, tile.Y));

				// If Goto is already set the AI previously committed to a better site — honour
				// that commitment and don't detour to found a city at the current tile.
				if (unit.Goto.IsEmpty)
				{
					if (validCity && nearestCity > 3)
					{
						DecisionLogger.LogSettlerAction(unit, "found");
						GameTask.Enqueue(Orders.FoundCity(unit as Settlers));
						unit.SkipTurn();
						return;
					}
					// Expand improvement radius: secondary cities surrounded by desert can't
					// get irrigation chains started unless settlers work tiles up to 5 tiles out.
					else if (nearestOwnCity <= 5)
					{
						bool tileAlreadyClaimed = (unit as Settlers)?.IsTileClaimed(tile.X, tile.Y) ?? false;
						if (!tileAlreadyClaimed)
						{
							// Post-contact improvements take priority when available.
						if (validCanopy)  { DecisionLogger.LogSettlerAction(unit, "canopy");   (unit as Settlers)?.BuildCanopyArray(); unit.SkipTurn(); return; }
						if (validAquafarm) { DecisionLogger.LogSettlerAction(unit, "aquafarm"); (unit as Settlers)?.BuildAquafarm();  unit.SkipTurn(); return; }

						var improvementChoice = ChooseSettlerImprovement(unit, validRoad, validIrrigation, validMine, nearestOwnCity);
							switch (improvementChoice)
							{
								case SettlerImprovement.Road:
									if (validRoad) { DecisionLogger.LogSettlerAction(unit, "road"); GameTask.Enqueue(Orders.BuildRoad(unit)); unit.SkipTurn(); return; }
									break;
								case SettlerImprovement.Irrigation:
									if (validIrrigation) { DecisionLogger.LogSettlerAction(unit, "irrigate"); GameTask.Enqueue(Orders.BuildIrrigation(unit)); unit.SkipTurn(); return; }
									break;
								case SettlerImprovement.Mine:
									if (validMine) { DecisionLogger.LogSettlerAction(unit, "mine"); GameTask.Enqueue(Orders.BuildMines(unit)); unit.SkipTurn(); return; }
									break;
							}
						}
					}

					ITile? best = BestSettleSite(unit);
					if (best is not null && (best.X != unit.X || best.Y != unit.Y))
					{
						unit.Goto = new Point(best.X, best.Y);
					}
					else if (best is null && ownCities.Any())
					{
						// No expansion site found — drift toward the nearest own city rather
						// than milling in empty terrain indefinitely.
						City home = ownCities.OrderBy(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y)).First();
						if (Common.DistanceToTile(home.X, home.Y, unit.X, unit.Y) > 2)
							unit.Goto = new Point(home.X, home.Y);
					}
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
				unit.SkipTurn();
				return;
			}

			if (unit is HydroEngineer)
			{
				ITile tile = unit.Tile;
				HydroEngineer hydro = (HydroEngineer)unit;

				if (tile.IsOcean && tile.City is null)
				{
					int nearestCity = 255;
					City[] allCities = Game.GetCities();
					if (allCities.Any()) nearestCity = allCities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y));

					int nearestOwnCity = 255;
					City[] ownCities = allCities.Where(c => c.Owner == unit.Owner).ToArray();
					if (ownCities.Any()) nearestOwnCity = ownCities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y));

					if (nearestCity > 4)
					{
						DecisionLogger.LogSettlerAction(unit, "found-floating");
						hydro.FoundFloatingCity();
						unit.SkipTurn();
						return;
					}

					if (nearestOwnCity <= 3 && Player.HasAdvance<BioplexEngineering>() && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
					{
						DecisionLogger.LogSettlerAction(unit, "sea-aquafarm");
						hydro.BuildSeaAquafarm();
						unit.SkipTurn();
						return;
					}

					if (nearestOwnCity <= 3 && !tile.TransportTube)
					{
						DecisionLogger.LogSettlerAction(unit, "sea-tube");
						hydro.BuildSeaTube();
						unit.SkipTurn();
						return;
					}
				}

				// Drift toward open ocean: find a deep ocean tile beyond any city's working radius.
				if (unit.Goto.IsEmpty)
				{
					ITile? dest = BestFloatingSite(unit);
					if (dest is not null) unit.Goto = new Point(dest.X, dest.Y);
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
				unit.SkipTurn();
				return;
			}

			if (unit is Militia || unit is Phalanx || unit is Musketeers || unit is Riflemen || unit is MechInf)
			{
				// Trim excess defenders in cities (per-city cap of 4)
				while (unit.Tile.City is not null && unit.Tile.Units.Count(x => x is Militia || x is Phalanx || x is Musketeers || x is Riflemen || x is MechInf) > 4)
				{
					IUnit? disband = null;
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Militia)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Phalanx)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Musketeers)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Riflemen)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is MechInf)) is not null) { Game.DisbandUnit(disband); continue; }
					break; // unit itself is the only remaining candidate — leave it alone
				}

				// Chieftain: militia explore toward fog-of-war instead of fortifying immediately,
				// but only if the city still has another defender (or the unit is already in the field).
				bool lastCityDefender = unit.Tile.City is not null
					&& unit.Tile.Units.Count(u => u.Role == UnitRole.Defense) <= 1;
				if (Game.Difficulty == 0 && unit is Militia && !lastCityDefender)
				{
					if (unit.Goto.IsEmpty)
					{
						ITile? dest = BestExploreTile(unit);
						if (dest is not null) unit.Goto = new Point(dest.X, dest.Y);
					}
					if (!unit.Goto.IsEmpty)
					{
						ITile? next = Common.GotoStep(unit);
						if (next is null) { unit.Goto = Point.Empty; unit.Fortify = true; return; }
						if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y)) unit.SkipTurn();
						return;
					}
				}

				unit.Fortify = true;
			}
			else
			{
				// Stale-Goto guard for Caravans and Diplomats. If the unit was saved with
				// a target that is now unreachable (peaceful neighbour moved in, war state
				// changed, etc.), AssignMission's FirstStepReachable check wouldn't re-fire
				// because Goto is non-empty — the unit would loop in MoveTo failures until
				// the circuit breaker. Clear the cached target here so AssignMission below
				// re-picks via FirstStepReachable on this very tick.
				if ((unit is Caravan || unit is Diplomat) && !unit.Goto.IsEmpty)
				{
					ITile? step = Common.GotoStep(unit, unit.Goto.X, unit.Goto.Y);
					bool reachable = step is not null;
					if (reachable && step!.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					                                  && Game.GetPlayer(u.Owner) is Player puStale
					                                  && !Player.IsAtWar(puStale))) reachable = false;
					if (reachable && step!.City is not null && step!.City.Owner != unit.Owner && step!.City.Owner != 0
					    && Game.GetPlayer(step.City.Owner) is Player pcStale
					    && pcStale.Civilization is not Civilizations.Barbarian
					    && !Player.IsAtWar(pcStale)) reachable = false;
					if (!reachable) unit.Goto = Point.Empty;
				}

				// Land unit just disembarked — still on an ocean tile. Step straight to land.
				if (unit.Class == UnitClass.Land && unit.Tile.IsOcean)
				{
					ITile land = unit.Tile.GetBorderTiles()
					    .Where(t => t is not null && !t.IsOcean && !t.Units.Any(u => u.Owner != unit.Owner))
					    .OrderBy(t => t.Units.Any() ? 0 : 1) // prefer our own units already there
					    .FirstOrDefault()
					    ?? unit.Tile.GetBorderTiles().FirstOrDefault(t => t is not null && !t.IsOcean);
					if (land is not null)
						unit.MoveTo(land.X - unit.X, land.Y - unit.Y);
					else
						unit.SkipTurn();
					return;
				}

				// Assign a mission if the unit is idle (sets unit.Goto)
				if (unit.Goto.IsEmpty) AssignMission(unit);

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null)
					{
						// No land path — try boarding an adjacent friendly transport
						if (unit.Role == UnitRole.LandAttack)
						{
							byte own = (byte)Game.PlayerNumber(Player);
							ITile boardTile = unit.Tile.GetBorderTiles()
							    .FirstOrDefault(t => t is not null && t.IsOcean
							        && t.Units.Any(u => u.Owner == own && u is IBoardable)
							        && t.Units.Where(u => u is IBoardable).Sum(u => (u as IBoardable)!.Cargo)
							           > t.Units.Count(u => u.Class == UnitClass.Land));
							if (boardTile is not null)
							{
								if (!unit.MoveTo(boardTile.X - unit.X, boardTile.Y - unit.Y))
									unit.SkipTurn();
								return;
							}
						}
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						return;
					}

					// Don't let a GoTo move initiate war with a civilization at peace. Barbarians
					// (Owner 0) are always implicitly hostile — never in _warWith but always
					// fair game. Without the explicit exemption a single Barbarian Settler can
					// freeze an attack stack of dozens, since each unit treats the Settler as
					// "peaceful blocked" and skips turn.
					{
						Player? nextCityOwner = (next.City is not null && next.City.Owner != unit.Owner) ? Game.GetPlayer(next.City.Owner) : null;
						bool peacefulBlock =
							next.Units.Any(u => {
								if (u.Owner == unit.Owner) return false;
								if (u.Owner == 0) return false;            // Barbarian unit: always attackable
								Player p = Game.GetPlayer(u.Owner);
								return p is not null && !Player.IsAtWar(p);
							})
							|| (nextCityOwner is not null && nextCityOwner.Civilization is not Civilizations.Barbarian && !Player.IsAtWar(nextCityOwner));
						if (peacefulBlock)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}
					}

					if (next.Units.Any(x => x.Owner != unit.Owner))
					{
						if (unit.Role == UnitRole.Civilian || unit.Role == UnitRole.Settler)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}

						if (unit.Role == UnitRole.Transport && Common.Random.Next(0, 100) < 67)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}

						// Staged assault: units committed to a designated target don't back down.
						bool stagedAssault = _attackTarget is not null && next.City == _attackTarget;
						if (!stagedAssault
						    && unit.Attack < next.Units.Select(x => x.Defense).Max()
						    && Common.Random.Next(0, 100) < 50)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}
					}

                    if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
                    {
                        HandleMovementFailure(unit, next);
                        return;
                    }
					return;
				}
				unit.SkipTurn();
				return;
			}
		}

		internal void ChooseResearch()
		{
			if (Player.CurrentResearch is not null) return;

			IAdvance[] available = Player.AvailableResearch.ToArray();
			if (available.Length == 0) return;

			StrategyStance stance = GetStance();
			int[] weights = available.Select(a => AdvanceWeight(a, stance)).ToArray();
			int total = weights.Sum();

			int roll = Common.Random.Next(total);
			int cumulative = 0;
			for (int i = 0; i < available.Length; i++)
			{
				cumulative += weights[i];
				if (roll < cumulative)
				{
					Player.CurrentResearch = available[i];
					Log($"AI: {Player.LeaderName} of the {Player.TribeNamePlural} starts researching {Player.CurrentResearch.Name}.");
					return;
				}
			}

			// Fallback (weights should always sum > 0, but be safe)
			Player.CurrentResearch = available[Common.Random.Next(available.Length)];
			Log($"AI: {Player.LeaderName} of the {Player.TribeNamePlural} starts researching {Player.CurrentResearch.Name}.");
		}

		internal void CityProduction(City city)
		{
			if (city is null || city.Size == 0 || city.Tile is null || Player != city.Owner) return;

			// Stalled city: no net production. Rerunning the full plan every turn just
			// thrashes the queue and spams the journal. Ensure a cheap defender exists
			// and leave everything else alone until income recovers.
			if (city.ShieldIncome <= 0)
			{
				int defenders = city.Tile.Units.Count(u => u.Role == UnitRole.Defense);
				if (defenders < 1)
					city.SetProduction(BestDefender());
				return;
			}

			city.ClearProductionQueue();
			var stance = GetStance();
			var plan = Game.Difficulty == 0
				? PlanChieftain(city, stance)
				: PlanProduction(city, stance);
			city.SetProduction(plan[0]);
			DecisionLogger.LogCityProduction(city, plan[0], stance.ToString());
			for (int i = 1; i < plan.Count; i++)
				city.EnqueueProduction(plan[i]);
		}

		private static Dictionary<Player, AI> _instances = new();
		internal static AI Instance(Player player)
		{
			if (_instances.ContainsKey(player))
				return _instances[player];
			_instances.Add(player, new AI(player));
			return _instances[player];
		}

		private AI(Player player)
		{
			Player = player;
		}
	}
}