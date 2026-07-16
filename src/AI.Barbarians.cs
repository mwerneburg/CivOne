// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Drawing;
using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne
{
	internal partial class AI
	{
		private static bool IsPolarTile(ITile tile) => tile.Type == Terrain.Arctic;

		private void BarbarianMove(IUnit unit)
		{
			// The Leviathan hunts on its own instincts — the generic water AI
			// would disband an empty barbarian hull, and this one never quits.
			if (unit is Leviathan)
			{
				LeviathanMove(unit);
				return;
			}

			switch (unit.Class)
			{
				case UnitClass.Water:
					BarbarianMoveWater(unit);
					return;
				case UnitClass.Land:
					BarbarianMoveLand(unit);
					return;
				default:
					Game.DisbandUnit(unit);
					return;
			}
		}

		// The Leviathan (Lighthouse curse): it does not raid, it hunts. Attack
		// any adjacent ship on open water; otherwise stalk the nearest civilized
		// coastal city and circle its waters until prey sails past. Ships docked
		// in harbor are safe — it wants them at sea.
		private void LeviathanMove(IUnit unit)
		{
			ITile[] prey = unit.Tile.GetBorderTiles()
				.Where(t => t.IsOcean && t.City is null && t.Units.Any(u => u.Owner != 0))
				.ToArray();
			if (prey.Length > 0)
			{
				ITile target = prey[Common.Random.Next(prey.Length)];
				if (!unit.MoveTo(target.X - unit.X, target.Y - unit.Y))
					unit.SkipTurn();
				return;
			}

			if (unit.Goto.IsEmpty)
			{
				City nearestCoastal = Game.GetCities()
					.Where(c => c.Owner != 0 && c.Size > 0
					         && c.Tile.GetBorderTiles().Any(t => t.IsOcean && !IsPolarTile(t)))
					.OrderBy(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y))
					.FirstOrDefault();
				if (nearestCoastal is not null)
				{
					ITile[] waters = nearestCoastal.Tile.GetBorderTiles()
						.Where(t => t.IsOcean && !IsPolarTile(t))
						.ToArray();
					ITile lurk = waters[Common.Random.Next(waters.Length)];
					if (lurk.X != unit.X || lurk.Y != unit.Y)
						unit.Goto = new Point(lurk.X, lurk.Y);
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
		}

		private void BarbarianMoveWater(IUnit unit)
		{
			if (!unit.Tile.Units.Any(x => x.Class == UnitClass.Land))
			{
				Game.DisbandUnit(unit);
				return;
			}

			for (int i = 0; i < 1000; i++)
			{
				// Only try to disembark when there are enemy-free land tiles to land on.
				// If every adjacent land tile is occupied by a non-barbarian, fall through
				// to the Goto navigation so the ship can seek a better landing spot.
				ITile[] landingZones = unit.Tile.GetBorderTiles()
					.Where(x => !x.IsOcean && !IsPolarTile(x) && !x.Units.Any(u => u.Owner != 0))
					.ToArray();
				if (landingZones.Length > 0)
				{
					City[] civilizedCities = Game.GetCities().Where(x => x.Owner != 0).ToArray();
					if (civilizedCities.Any())
					{
						City nearestCity = civilizedCities.OrderBy(x => Common.DistanceToTile(x.X, x.Y, unit.X, unit.Y)).ThenBy(x => x.Player == Human ? 0 : 1).First();
						if (nearestCity.Player == Human && Human.Visible(unit.Tile))
						{
							GameTask.Insert(Message.Advisor(Advisor.Defense, false, "Barbarian raiding party", $"lands near {nearestCity.Name}!", "Citizens are alarmed."));
						}
					}

					// Aboard units are invisible to ActiveUnit so UnitWait can never unblock.
					// Move each land unit directly to a landing tile instead.
					foreach (IUnit landUnit in unit.Tile.Units.Where(x => x.Class == UnitClass.Land).ToList())
					{
						landUnit.Sentry = false;
						ITile dest = landingZones[Common.Random.Next(landingZones.Length)];
						landUnit.MoveTo(dest.X - landUnit.X, dest.Y - landUnit.Y);
					}
					unit.SkipTurn();
					return;
				}

				if (unit.Goto.IsEmpty)
				{
					// Target a coastal ocean tile adjacent to the nearest palace city.
					// Targeting the city tile itself would make GotoStep fail (water→land),
					// causing an infinite re-targeting loop.
					City nearestCity = Game.GetCities()
						.Where(x => x.Owner != 0 && x.HasBuilding<Palace>()
						         && x.Tile.GetBorderTiles().Any(t => t.IsOcean && !IsPolarTile(t)))
						.OrderBy(x => Common.DistanceToTile(x.X, x.Y, unit.X, unit.Y))
						.FirstOrDefault();

					if (nearestCity is null
					    || Common.DistanceToTile(unit.X, unit.Y, nearestCity.X, nearestCity.Y) > 10)
					{
						Game.DisbandUnit(unit);
						return;
					}

					ITile approach = nearestCity.Tile.GetBorderTiles()
						.Where(t => t.IsOcean && !IsPolarTile(t))
						.OrderBy(t => Common.DistanceToTile(unit.X, unit.Y, t.X, t.Y))
						.First();

					unit.Goto = new Point(approach.X, approach.Y);
					continue;
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null)
					{
						// No path to current target — give up for this turn.
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						return;
					}
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

			// Safety fallback: loop exhausted without resolving — skip turn.
			unit.SkipTurn();
		}

		private void BarbarianMoveLand(IUnit unit)
		{
			if (unit.Tile.IsOcean && unit.Tile.GetBorderTiles().Where(x => !x.IsOcean && !IsPolarTile(x)).All(x => x.Units.Any(u => u.Owner != 0)))
			{
				IUnit ship = unit.Tile.Units.FirstOrDefault(u => u.Class == UnitClass.Water && u.MovesLeft > 0);
				if (ship is not null)
				{
					ITile[] landTiles = unit.Tile.GetBorderTiles().Where(x => !x.IsOcean && !IsPolarTile(x) && x.Units.Any(u => u.Owner != 0)).ToArray();
					if (landTiles.Length > 0)
					{
						ITile tile = landTiles[Common.Random.Next(landTiles.Length)];
						if (!ship.MoveTo(tile.X - unit.X, tile.Y - unit.Y))
							unit.SkipTurn();
						return;
					}
				}
				unit.SkipTurn();
				return;
			}

			if (unit is Diplomat)
			{
				ITile[] friendlyTiles = unit.Tile.GetBorderTiles().Where(x => !x.IsOcean && !IsPolarTile(x) && x.Units.Any() && x.Units.First().Owner == 0).ToArray();
				if (friendlyTiles.Length > 0)
				{
					ITile moveTo = friendlyTiles[Common.Random.Next(friendlyTiles.Length)];
					int relX = moveTo.X - unit.X;
					int relY = moveTo.Y - unit.Y;
					if (!unit.MoveTo(relX, relY))
						unit.SkipTurn();
					return;
				}

				if (unit.Tile.Units.Any(x => !(x is Diplomat) && x.MovesLeft > 0))
				{
					Game.UnitWait();
					return;
				}

				if (unit.Tile.Units.Any(x => !(x is Diplomat)))
				{
					unit.SkipTurn();
					return;
				}
			}

			ITile[] tiles = unit.Tile.GetBorderTiles().Where(t => !((unit.Tile.IsOcean || unit is Diplomat) && t.City is not null) && !t.IsOcean && !IsPolarTile(t) && t.Units.Any(u => u.Owner != 0)).ToArray();
			if (tiles.Length == 0)
			{
				// No adjacent enemies — march toward the nearest civilization city.
				if (unit.Goto.IsEmpty)
				{
					City target = Game.GetCities()
						.Where(c => c.Owner != 0)
						.OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
						.FirstOrDefault();
					if (target is not null)
						unit.Goto = new Point(target.X, target.Y);
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null)
					{
						// Arrived or path blocked — re-evaluate next turn.
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						return;
					}
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
					}
					return;
				}

				// No civilization cities exist — disband.
				Game.DisbandUnit(unit);
				return;
			}
			else
			{
				ITile moveTo = tiles[Common.Random.Next(tiles.Length)];
				int relX = moveTo.X - unit.X;
				int relY = moveTo.Y - unit.Y;
				while (relX < -1) relX += Map.WIDTH;
				while (relX > 1) relX -= Map.WIDTH;
				if (unit is Diplomat && unit.Tile.City is not null) { unit.SkipTurn(); return; }

				if (!unit.MoveTo(relX, relY))
					unit.SkipTurn();
			}
		}
	}
}