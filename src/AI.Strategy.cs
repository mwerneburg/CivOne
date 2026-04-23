// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

using static CivOne.Enums.DevelopmentLevel;

namespace CivOne
{
	internal partial class AI
	{
		// ── strategic stance ───────────────────────────────────────────────────

		private enum StrategyStance { Expand, Develop, Militarize, Consolidate }

		private StrategyStance GetStance()
		{
			var cities = Player.Cities;

			// Consolidate: Rep/Dem with unhappy majorities can't sustain expansion
			if (Player.RepublicDemocratic && cities.Length > 0
			    && cities.Count(c => c.UnhappyCitizens > 0) * 2 > cities.Length)
				return StrategyStance.Consolidate;

			// Militarize: already at war
			if (Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p)))
				return StrategyStance.Militarize;

			// Militarize: aggressive/militaristic and at least as strong as a neighbour
			if (Leader.Militarism == MilitarismLevel.Militaristic
			    || Leader.Aggression == AggressionLevel.Aggressive)
			{
				int own = MilitaryScore(Player);
				if (own > 0 && Game.Players.Any(p =>
				    p != Player && !p.IsDestroyed()
				    && IsNeighbor(p) && own >= MilitaryScore(p)))
					return StrategyStance.Militarize;
			}

			// Expand: below the leader's preferred city count
			int target = Leader.Development == Expansionistic ? 9
			           : Leader.Development == Normal          ? 6 : 4;
			if (cities.Length < target) return StrategyStance.Expand;

			return StrategyStance.Develop;
		}

		private bool IsNeighbor(Player enemy)
		{
			return Player.Cities.Any(oc =>
			    enemy.Cities.Any(ec =>
			        Common.DistanceToTile(oc.X, oc.Y, ec.X, ec.Y) <= 15));
		}

		private int MilitaryScore(Player player)
		{
			byte num = Game.PlayerNumber(player);
			return Game.GetUnits()
			           .Where(u => u.Owner == num && u.Role == UnitRole.LandAttack)
			           .Sum(u => u.Attack + u.Defense);
		}

		// ── city-site scoring ──────────────────────────────────────────────────

		private int SiteSuitability(ITile center)
		{
			int score = 0;
			int w = Map.WIDTH, h = Map.HEIGHT;

			// Sum resource value of every tile in the city's working diamond
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue; // corners outside diamond
				int tx = (center.X + dx + w) % w;
				int ty = center.Y + dy;
				if (ty < 0 || ty >= h) continue;
				ITile t = Map[tx, ty];
				if (t == null) continue;
				score += t.Food * 2 + t.Shield + t.Trade;
			}

			// River adjacency: nearby rivers enable irrigation
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (center.X + dx + w) % w;
				int ty = center.Y + dy;
				if (ty < 0 || ty >= h) continue;
				if (Map[tx, ty] is River) score += 3;
			}

			// Penalise sites whose working radius overlaps existing cities
			foreach (City city in Game.GetCities())
			{
				int d = Common.DistanceToTile(center.X, center.Y, city.X, city.Y);
				if (d < 4) score -= 20;
				else if (d < 6) score -= 5;
			}

			// Bonus for staying connected to the existing empire
			if (Player.Cities.Any(c =>
			    Common.DistanceToTile(center.X, center.Y, c.X, c.Y) <= 6))
				score += 5;

			return score;
		}

		internal ITile BestSettleSite(IUnit settlers)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			ITile best = null;
			int bestScore = int.MinValue;

			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				int tx = (settlers.X + dx + w) % w;
				int ty = settlers.Y + dy;
				if (ty < 0 || ty >= h) continue;
				ITile tile = Map[tx, ty];
				if (tile == null || tile.IsOcean || tile.City != null) continue;
				if (Game.GetCities().Any(c => Common.DistanceToTile(c.X, c.Y, tx, ty) < 4)) continue;
				if (!Player.Visible(tx, ty)) continue;
				int score = SiteSuitability(tile);
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

		// ── unit mission assignment ────────────────────────────────────────────
		// Sets unit.Goto; leaves it empty if no useful mission is found.

		private void AssignMission(IUnit unit)
		{
			StrategyStance stance = GetStance();

			// Naval units: patrol the nearest coastal city
			if (unit.Class == UnitClass.Water)
			{
				City port = Player.Cities
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault();
				if (port != null) unit.Goto = new Point(port.X, port.Y);
				return;
			}

			// Diplomats: head for the nearest visible foreign city
			if (unit is Diplomat)
			{
				City target = Game.GetCities()
				    .Where(c => c.Player != Player && Player.Visible(c.X, c.Y))
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault();
				if (target != null) unit.Goto = new Point(target.X, target.Y);
				return;
			}

			// Caravans: head for the most distant foreign city (trade route gold)
			if (unit is Caravan)
			{
				City target = Game.GetCities()
				    .Where(c => c.Player != Player)
				    .OrderByDescending(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault()
				    ?? Player.Cities
				       .OrderByDescending(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				       .FirstOrDefault();
				if (target != null) unit.Goto = new Point(target.X, target.Y);
				return;
			}

			// Offensive land units: attack when militarizing, otherwise reinforce
			if (unit.Role == UnitRole.LandAttack)
			{
				if (stance == StrategyStance.Militarize)
				{
					// Find the weakest visible enemy city (fewest defenders)
					City target = Game.GetCities()
					    .Where(c => c.Player != Player
					             && Player.IsAtWar(c.Player)
					             && Player.Visible(c.X, c.Y))
					    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
					    .ThenBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
					    .FirstOrDefault();
					if (target != null) { unit.Goto = new Point(target.X, target.Y); return; }
				}

				// Default: reinforce the most under-defended own city
				City needsHelp = Player.Cities
				    .Where(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense) < 2)
				    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
				    .ThenBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault();
				if (needsHelp != null) unit.Goto = new Point(needsHelp.X, needsHelp.Y);
			}
		}

		// ── production helpers ─────────────────────────────────────────────────

		private IProduction BestDefender()
		{
			if (Player.HasAdvance<LaborUnion>())    return new MechInf();
			if (Player.HasAdvance<Conscription>())  return new Riflemen();
			if (Player.HasAdvance<Gunpowder>())     return new Musketeers();
			if (Player.HasAdvance<BronzeWorking>()) return new Phalanx();
			return new Militia();
		}

		private IProduction BestAttacker()
		{
			if (Player.HasAdvance<Automobile>())       return new Armor();
			if (Player.HasAdvance<Metallurgy>())       return new Cannon();
			if (Player.HasAdvance<Chivalry>())         return new Knights();
			if (Player.HasAdvance<TheWheel>())         return new Chariot();
			if (Player.HasAdvance<HorsebackRiding>())  return new Cavalry();
			if (Player.HasAdvance<IronWorking>())      return new Legion();
			return new Militia();
		}
	}
}
