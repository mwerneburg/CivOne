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

		internal void Move(IUnit unit)
		{
			if (Player != unit.Owner) return;

			if (unit.Owner == 0)
			{
				BarbarianMove(unit);
				return;
			}
			
			if (unit is Settlers)
			{
				ITile tile = unit.Tile;

				bool hasCity = (tile.City != null);
				bool validCity = (tile is Grassland || tile is River || tile is Plains) && (tile.City == null);
				bool validIrrigaton = (tile is Grassland || tile is River || tile is Plains || tile is Desert) && (tile.City == null) && (!tile.Mine) && (!tile.Irrigation) && tile.CrossTiles().Any(x => x.IsOcean || x is River || x.Irrigation);
				bool validMine = (tile is Mountains || tile is Hills) && (tile.City == null) && (!tile.Mine) && (!tile.Irrigation);
				bool validRoad = (tile.City == null) && tile.Road;
				int nearestCity = 255;
				int nearestOwnCity = 255;
				
				if (Game.GetCities().Any()) nearestCity = Game.GetCities().Min(x => Common.DistanceToTile(x.X, x.Y, tile.X, tile.Y));
				if (Game.GetCities().Any(x => x.Owner == unit.Owner)) nearestOwnCity = Game.GetCities().Where(x => x.Owner == unit.Owner).Min(x => Common.DistanceToTile(x.X, x.Y, tile.X, tile.Y));
				
				if (validCity && nearestCity > 3)
				{
					GameTask.Enqueue(Orders.FoundCity(unit as Settlers));
					return;
				}
				else if (nearestOwnCity < 3)
				{
					switch (Common.Random.Next(5 * nearestOwnCity))
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

				// Navigate toward the best visible city site
				if (unit.Goto.IsEmpty)
				{
					ITile best = BestSettleSite(unit);
					if (best != null && (best.X != unit.X || best.Y != unit.Y))
						unit.Goto = new Point(best.X, best.Y);
				}
				if (!unit.Goto.IsEmpty)
				{
					ITile next = Common.GotoStep(unit);
					if (next == null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y)) unit.SkipTurn();
					return;
				}
				unit.SkipTurn();
				return;
			}
			else if (unit is Militia || unit is Phalanx || unit is Musketeers || unit is Riflemen || unit is MechInf)
			{
				unit.Fortify = true;
				while (unit.Tile.City != null && unit.Tile.Units.Count(x => x is Militia || x is Phalanx || x is Musketeers || x is Riflemen || x is MechInf) > 2)
				{
					IUnit disband = null;
					IUnit[] units = unit.Tile.Units.Where(x => x != unit).ToArray();
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Militia)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Phalanx)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Musketeers)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is Riflemen)) != null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x is MechInf)) != null) { Game.DisbandUnit(disband); continue; }
				}
			}
			else
			{
				// Assign a mission if the unit is idle (sets unit.Goto)
				if (unit.Goto.IsEmpty) AssignMission(unit);

				if (!unit.Goto.IsEmpty)
				{
					ITile next = Common.GotoStep(unit);
					if (next == null)
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						return;
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

						if (unit.Attack < next.Units.Select(x => x.Defense).Max() && Common.Random.Next(0, 100) < 50)
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
			
			IAdvance[] advances = Player.AvailableResearch.ToArray();
			
			// No further research possible
			if (advances.Length == 0) return;

			Player.CurrentResearch = advances[Common.Random.Next(0, advances.Length)];

			Log($"AI: {Player.LeaderName} of the {Player.TribeNamePlural} starts researching {Player.CurrentResearch.Name}.");
		}

		internal void CityProduction(City city)
		{
			if (city == null || city.Size == 0 || city.Tile == null || Player != city.Owner) return;

			StrategyStance stance = GetStance();
			IProduction production = null;

			// Barracks: universal first priority — veteran units matter in every stance
			if (!city.HasBuilding<Barracks>())
			{
				city.SetProduction(new Barracks());
				return;
			}

			// Minimum garrison: at least 1 defender in every city at all times
			if (city.Tile.Units.Count(u => u.Role == UnitRole.Defense) < 1)
				production = BestDefender();

			// Consolidate: happiness and growth buildings take priority over everything else
			if (production == null && stance == StrategyStance.Consolidate)
			{
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>())   production = new Temple();
				else if (Player.HasAdvance<Construction>() && !city.HasBuilding<Colosseum>()) production = new Colosseum();
				else if (Player.HasAdvance<Religion>()      && !city.HasBuilding<Cathedral>()) production = new Cathedral();
				else if (Player.HasAdvance<Pottery>()       && !city.HasBuilding<Granary>())   production = new Granary();
			}

			// Militarize: build up to 2 defenders, then offensive units
			if (production == null && stance == StrategyStance.Militarize)
			{
				if (city.Tile.Units.Count(u => u.Role == UnitRole.Defense) < 2)
					production = BestDefender();
				else if (!Player.RepublicDemocratic)
					production = BestAttacker();
			}

			// Expand: produce Settlers once the city is big enough
			if (production == null && stance == StrategyStance.Expand)
			{
				int minSize = Leader.Development == Expansionistic ? 2
				            : Leader.Development == Normal          ? 3 : 4;
				int maxCities = Leader.Development == Expansionistic ? 13
				              : Leader.Development == Normal          ? 10 : 7;
				if (city.Size >= minSize && !city.Units.Any(x => x is Settlers)
				    && Player.Cities.Length < maxCities)
					production = new Settlers();
			}

			// Standard infrastructure chain (all stances)
			if (production == null)
			{
				if (Player.HasAdvance<Pottery>()           && !city.HasBuilding<Granary>())    production = new Granary();
				else if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>())    production = new Temple();
				else if (Player.HasAdvance<Writing>()          && !city.HasBuilding<Library>())   production = new Library();
				else if (Player.HasAdvance<Currency>()         && !city.HasBuilding<MarketPlace>()) production = new MarketPlace();
				else if (Player.HasAdvance<Masonry>()          && !city.HasBuilding<CityWalls>())  production = new CityWalls();
				else if (Player.HasAdvance<Construction>()     && !city.HasBuilding<Colosseum>())  production = new Colosseum();
				else if (Player.HasAdvance<Religion>()         && !city.HasBuilding<Cathedral>())  production = new Cathedral();
			}

			// Second defender once infrastructure is underway
			if (production == null && city.Tile.Units.Count(u => u.Role == UnitRole.Defense) < 2)
				production = BestDefender();

			// Soft units based on government and stance
			if (production == null)
			{
				if (stance == StrategyStance.Militarize && !Player.RepublicDemocratic)
					production = BestAttacker();
				else if (Player.HasAdvance<Writing>())
					production = new Diplomat();
				else if (Player.HasAdvance<Trade>())
					production = new Caravan();
			}

			// Fallback: random available production item
			if (production == null)
			{
				IProduction[] items = city.AvailableProduction.ToArray();
				production = items[Common.Random.Next(items.Length)];
			}

			city.SetProduction(production);
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