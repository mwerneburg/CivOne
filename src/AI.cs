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
		private City _attackTarget;

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
			
			if (unit is Settlers)
			{
				ITile tile = unit.Tile;

				bool validCity = (tile is Grassland || tile is River || tile is Plains) && (tile.City == null);
				bool validIrrigaton = (tile is Grassland || tile is River || tile is Plains || tile is Desert) && (tile.City == null) && (!tile.Mine) && (!tile.Irrigation) && tile.CrossTiles().Any(x => x.IsOcean || x is River || x.Irrigation);
				bool validMine = (tile is Mountains || tile is Hills) && (tile.City == null) && (!tile.Mine) && (!tile.Irrigation);
				bool validRoad = (tile.City == null) && tile.Road;
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
						GameTask.Enqueue(Orders.FoundCity(unit as Settlers));
						unit.SkipTurn();
						return;
					}
					else if (nearestOwnCity < 3)
					{
						switch (Common.Random.Next(nearestOwnCity < 1 ? 1 : 5 * nearestOwnCity))
						{
							case 0:
								if (validRoad)
								{
									GameTask.Enqueue(Orders.BuildRoad(unit));
									return;
								}
								break;
							case 1:
								if (validIrrigaton)
								{
									GameTask.Enqueue(Orders.BuildIrrigation(unit));
									return;
								}
								break;
							case 2:
								if (validMine)
								{
									GameTask.Enqueue(Orders.BuildMines(unit));
									return;
								}
								break;
						}
					}

					ITile best = BestSettleSite(unit);
					if (best != null && (best.X != unit.X || best.Y != unit.Y))
						unit.Goto = new Point(best.X, best.Y);
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile next = Common.GotoStep(unit);
					if (next == null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
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
			else if (unit is Militia || unit is Phalanx || unit is Musketeers || unit is Riflemen || unit is MechInf)
			{
				// Trim excess defenders in cities (per-city cap of 4)
				while (unit.Tile.City != null && unit.Tile.Units.Count(x => x is Militia || x is Phalanx || x is Musketeers || x is Riflemen || x is MechInf) > 4)
				{
					IUnit disband = null;
					IUnit[] units = unit.Tile.Units.Where(x => x != unit).ToArray();
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Militia)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Phalanx)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Musketeers)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Riflemen)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is MechInf)) != null) { Game.DisbandUnit(disband); continue; }
				}

				// Chieftain: militia explore toward fog-of-war instead of fortifying immediately,
				// but only if the city still has another defender (or the unit is already in the field).
				bool lastCityDefender = unit.Tile.City != null
					&& unit.Tile.Units.Count(u => u.Role == UnitRole.Defense) <= 1;
				if (Game.Difficulty == 0 && unit is Militia && !lastCityDefender)
				{
					if (unit.Goto.IsEmpty)
					{
						ITile dest = BestExploreTile(unit);
						if (dest != null) unit.Goto = new Point(dest.X, dest.Y);
					}
					if (!unit.Goto.IsEmpty)
					{
						ITile next = Common.GotoStep(unit);
						if (next == null) { unit.Goto = Point.Empty; unit.Fortify = true; return; }
						if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y)) unit.SkipTurn();
						return;
					}
				}

				unit.Fortify = true;
			}
			else
			{
				// Land unit just disembarked — still on an ocean tile. Step straight to land.
				if (unit.Class == UnitClass.Land && unit.Tile.IsOcean)
				{
					ITile land = unit.Tile.GetBorderTiles()
					    .Where(t => t != null && !t.IsOcean && !t.Units.Any(u => u.Owner != unit.Owner))
					    .OrderBy(t => t.Units.Any() ? 0 : 1) // prefer our own units already there
					    .FirstOrDefault()
					    ?? unit.Tile.GetBorderTiles().FirstOrDefault(t => t != null && !t.IsOcean);
					if (land != null)
						unit.MoveTo(land.X - unit.X, land.Y - unit.Y);
					else
						unit.SkipTurn();
					return;
				}

				// Assign a mission if the unit is idle (sets unit.Goto)
				if (unit.Goto.IsEmpty) AssignMission(unit);

				if (!unit.Goto.IsEmpty)
				{
					ITile next = Common.GotoStep(unit);
					if (next == null)
					{
						// No land path — try boarding an adjacent friendly transport
						if (unit.Role == UnitRole.LandAttack)
						{
							byte own = (byte)Game.PlayerNumber(Player);
							ITile boardTile = unit.Tile.GetBorderTiles()
							    .FirstOrDefault(t => t != null && t.IsOcean
							        && t.Units.Any(u => u.Owner == own && u is IBoardable)
							        && t.Units.Where(u => u is IBoardable).Sum(u => (u as IBoardable).Cargo)
							           > t.Units.Count(u => u.Class == UnitClass.Land));
							if (boardTile != null)
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

					// Don't let a GoTo move initiate war with a civilization at peace.
					{
						Player nextCityOwner = (next.City != null && next.City.Owner != unit.Owner) ? Game.GetPlayer(next.City.Owner) : null;
						bool peacefulBlock =
							next.Units.Any(u => { if (u.Owner == unit.Owner) return false; Player p = Game.GetPlayer(u.Owner); return p != null && !Player.IsAtWar(p); })
							|| (nextCityOwner != null && !Player.IsAtWar(nextCityOwner));
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
						bool stagedAssault = _attackTarget != null && next.City == _attackTarget;
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
						if (Common.Random.Next(0, 100) < 67)
							unit.Goto = Point.Empty;
						else if (Common.Random.Next(0, 100) < 67)
							unit.SkipTurn();
						else
							Game.DisbandUnit(unit);
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
			if (Player.CurrentResearch != null) return;

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
			if (city == null || city.Size == 0 || city.Tile == null || Player != city.Owner) return;

			city.ClearProductionQueue();
			var stance = GetStance();
			var plan = Game.Difficulty == 0
				? PlanChieftain(city, stance)
				: PlanProduction(city, stance);
			city.SetProduction(plan[0]);
			for (int i = 1; i < plan.Count; i++)
				city.EnqueueProduction(plan[i]);
		}

		private static Dictionary<Player, AI> _instances = new Dictionary<Player, AI>();
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
