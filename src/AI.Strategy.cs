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
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

using CivOne.Governments;
using CivOne.Wonders;
using Gov = CivOne.Governments;
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

		// ── government progression ────────────────────────────────────────────

		private static int GovernmentScore(IGovernment gov, StrategyStance stance)
		{
			if (gov is Gov.Democracy)
				return stance == StrategyStance.Develop ? 5 : 2;
			if (gov is Gov.Republic)
				return stance == StrategyStance.Develop ? 4 : 3;
			if (gov is Gov.Communism)
				return stance == StrategyStance.Militarize ? 4 : 3;
			if (gov is Gov.Monarchy)
				return stance == StrategyStance.Militarize || stance == StrategyStance.Expand ? 5 : 3;
			if (gov is Gov.Despotism)
				return 1;
			return 0;
		}

		private IGovernment BestGovernment()
		{
			StrategyStance stance = GetStance();
			int currentScore = GovernmentScore(Player.Government, stance);
			return Player.AvailableGovernments
			             .Where(g => GovernmentScore(g, stance) > currentScore)
			             .OrderByDescending(g => GovernmentScore(g, stance))
			             .FirstOrDefault();
		}

		// Called when anarchy ends: pick the best available government.
		internal void ChooseGovernment()
		{
			Player.Government = BestGovernment() ?? new Gov.Despotism();
		}

		// Called each turn: consider starting a revolution if conditions are good.
		internal void ConsiderGovernment()
		{
			if (Player.Government is Gov.Anarchy) return;

			// Only revolt from a stable position
			StrategyStance stance = GetStance();
			if (stance == StrategyStance.Militarize || stance == StrategyStance.Consolidate) return;

			// Don't revolt while at war
			if (Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p))) return;

			if (BestGovernment() == null) return; // already optimal

			// ~25 % chance per turn → roughly 4-turn lag before acting
			if (Common.Random.Next(100) < 25)
				Player.Revolt();
		}

		// ── proactive war declaration ──────────────────────────────────────────

		internal void ConsiderWar()
		{
			// Barbarians use their own logic; governments in revolution are distracted
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Government is Governments.Anarchy) return;

			// Republics and Democracies are blocked by their Senate from starting wars
			if (Player.RepublicDemocratic) return;

			// Civilised non-aggressive leaders don't pick fights
			if (Leader.Militarism == MilitarismLevel.Civilized
			    && Leader.Aggression != AggressionLevel.Aggressive)
				return;

			int own = MilitaryScore(Player);
			if (own == 0) return; // no army, no war

			foreach (Player enemy in Game.Players)
			{
				if (enemy == Player || enemy.IsDestroyed()) continue;
				if (Player.IsAtWar(enemy)) continue;
				if (!IsNeighbor(enemy)) continue;

				int their = MilitaryScore(enemy);

				// Base chance from leader personality
				int chance = 0;
				if (Leader.Aggression  == AggressionLevel.Aggressive)    chance += 8;
				if (Leader.Militarism  == MilitarismLevel.Militaristic)   chance += 7;

				// Modifier for relative strength
				if (own > their)           chance += 5;
				if (own > their * 3 / 2)   chance += 5; // notably stronger
				if (their > own * 3 / 2)   chance -= 20; // notably weaker — don't be reckless

				if (Common.Random.Next(100) < chance)
				{
					Player.DeclareWar(enemy);
					return; // one declaration per turn
				}
			}
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

		// ── attack staging ────────────────────────────────────────────────────────

		private City PickAttackTarget()
		{
			// Prefer the weakest (fewest defenders) visible enemy city closest to our empire
			return Game.GetCities()
			           .Where(c => c.Player != Player
			                    && Player.IsAtWar(c.Player)
			                    && Player.Visible(c.X, c.Y))
			           .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
			           .ThenBy(c => Player.Cities.Min(oc =>
			               Common.DistanceToTile(oc.X, oc.Y, c.X, c.Y)))
			           .FirstOrDefault();
		}

		private ITile StagingTile(City target)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			byte own = Game.PlayerNumber(Player);
			ITile best = null;
			int bestCount = -1;

			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (target.X + dx + w) % w;
				int ty = target.Y + dy;
				if (ty < 0 || ty >= h) continue;
				ITile t = Map[tx, ty];
				if (t == null || t.IsOcean) continue;
				// Don't stage on a tile already occupied by enemies
				if (t.Units.Any(u => u.Owner != own)) continue;
				int count = t.Units.Count(u => u.Owner == own && u.Role == UnitRole.LandAttack);
				if (best == null || count > bestCount) { best = t; bestCount = count; }
			}
			return best;
		}

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

			// Offensive land units
			if (unit.Role == UnitRole.LandAttack)
			{
				if (stance == StrategyStance.Militarize)
				{
					// Validate or refresh the civ-wide attack target
					if (_attackTarget == null
					    || _attackTarget.Player == Player         // we captured it
					    || !Player.IsAtWar(_attackTarget.Player)) // war ended
						_attackTarget = PickAttackTarget();

					if (_attackTarget != null)
					{
						ITile staging = StagingTile(_attackTarget);
						byte own = Game.PlayerNumber(Player);

						// How many attackers are already at the staging tile?
						int staged = staging?.Units.Count(u =>
						    u.Owner == own && u.Role == UnitRole.LandAttack) ?? 0;

						// Commit when we have enough force; be generous if we outbuilt the defense
						int defenders = _attackTarget.Tile.Units.Count(u => u.Role == UnitRole.Defense);
						int threshold = Math.Max(2, defenders + 1);

						Point dest = (staged >= threshold || staging == null)
						    ? new Point(_attackTarget.X, _attackTarget.Y)
						    : new Point(staging.X, staging.Y);
						unit.Goto = dest;
						return;
					}
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

		// ── research weights ──────────────────────────────────────────────────

		private static int AdvanceWeight(IAdvance a, StrategyStance stance)
		{
			int w = 1; // baseline: every advance can be chosen

			switch (stance)
			{
				case StrategyStance.Militarize:
					if (a is BronzeWorking)      w += 7;
					if (a is IronWorking)         w += 7;
					if (a is TheWheel)            w += 6;
					if (a is HorsebackRiding)     w += 7;
					if (a is Feudalism)           w += 5;
					if (a is Chivalry)            w += 7;
					if (a is Gunpowder)           w += 8;
					if (a is Mathematics)         w += 4;
					if (a is Physics)             w += 5;
					if (a is Chemistry)           w += 5;
					if (a is Metallurgy)          w += 7;
					if (a is Engineering)         w += 5;
					if (a is SteamEngine)         w += 5;
					if (a is Industrialization)   w += 6;
					if (a is Conscription)        w += 8;
					if (a is Automobile)          w += 8;
					if (a is LaborUnion)          w += 8;
					if (a is Masonry)             w += 4; // CityWalls for defence
					break;

				case StrategyStance.Develop:
					if (a is Alphabet)            w += 7;
					if (a is Writing)             w += 8;
					if (a is Literacy)            w += 6;
					if (a is CodeOfLaws)          w += 6;
					if (a is TheRepublic)         w += 7;
					if (a is Advances.Democracy)  w += 6;
					if (a is Pottery)             w += 6;
					if (a is Trade)               w += 8;
					if (a is Currency)            w += 7;
					if (a is Banking)             w += 7;
					if (a is TheCorporation)      w += 6;
					if (a is Philosophy)          w += 5;
					if (a is Advances.University)  w += 7;
					if (a is Invention)           w += 6;
					if (a is TheoryOfGravity)     w += 6;
					if (a is Masonry)             w += 5;
					if (a is Construction)        w += 5;
					if (a is CeremonialBurial)    w += 5;
					if (a is Mysticism)           w += 4;
					if (a is Religion)            w += 5;
					break;

				case StrategyStance.Consolidate:
					if (a is CeremonialBurial)    w += 9; // Temple
					if (a is Mysticism)           w += 8; // doubles Temple
					if (a is Philosophy)          w += 6;
					if (a is Religion)            w += 8; // Cathedral
					if (a is Construction)        w += 8; // Colosseum
					if (a is Pottery)             w += 7; // Granary
					if (a is Trade)               w += 6;
					if (a is Currency)            w += 6;
					if (a is Banking)             w += 5;
					if (a is Writing)             w += 5;
					break;

				case StrategyStance.Expand:
					if (a is Pottery)             w += 8; // Granary feeds growth
					if (a is BridgeBuilding)      w += 7; // roads cross rivers
					if (a is RailRoad)            w += 7; // fast movement
					if (a is Masonry)             w += 6;
					if (a is MapMaking)           w += 5; // explore coasts
					if (a is Alphabet)            w += 5;
					if (a is Writing)             w += 5;
					if (a is Trade)               w += 5;
					if (a is TheWheel)            w += 5;
					if (a is HorsebackRiding)     w += 5;
					break;
			}

			return w;
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

		// ── wonder selection ───────────────────────────────────────────────────

		// Only the single highest-production city should chase a wonder.
		// Ties are broken by map position for stability across turns.
		private bool IsTopProductionCity(City city)
		{
			City[] cities = Player.Cities;
			if (cities.Length == 0) return false;
			int maxShields = cities.Max(c => c.ShieldIncome);
			if (city.ShieldIncome < maxShields) return false;
			return cities.Where(c => c.ShieldIncome == maxShields)
			             .OrderBy(c => c.X).ThenBy(c => c.Y)
			             .First() == city;
		}

		private IWonder SelectWonder(City city, StrategyStance stance)
		{
			if (!IsTopProductionCity(city)) return null;

			IWonder[] preferred;
			if (stance == StrategyStance.Militarize)
			{
				preferred = new IWonder[]
				{
					new GreatWall(), new Colossus(), new MichelangelosChapel()
				};
			}
			else if (stance == StrategyStance.Consolidate)
			{
				preferred = new IWonder[]
				{
					new ShakespearesTheatre(), new JSBachsCathedral(),
					new HangingGardens(), new MichelangelosChapel(), new Oracle()
				};
			}
			else
			{
				preferred = new IWonder[]
				{
					new Pyramids(), new ShakespearesTheatre(), new IsaacNewtonsCollege(),
					new JSBachsCathedral(), new HangingGardens(), new Oracle(),
					new GreatLibrary(), new DarwinsVoyage(), new CopernicusObservatory(),
					new Colossus(), new Lighthouse(), new MagellansExpedition()
				};
			}

			return preferred.FirstOrDefault(w =>
				!Game.WonderBuilt(w) && Player.ProductionAvailable(w));
		}

		// ── full production plan for a city ────────────────────────────────────

		private List<IProduction> PlanProduction(City city, StrategyStance stance)
		{
			var plan = new List<IProduction>();
			void Consider(IProduction p)
			{
				if (plan.All(x => x.GetType() != p.GetType())) plan.Add(p);
			}

			int defenders = city.Tile.Units.Count(u => u.Role == UnitRole.Defense);

			// Universal first: barracks and minimum garrison
			if (!city.HasBuilding<Barracks>()) Consider(new Barracks());
			if (defenders < 1)                Consider(BestDefender());

			// Consolidate: happiness and growth buildings first
			if (stance == StrategyStance.Consolidate)
			{
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>())    Consider(new Temple());
				if (Player.HasAdvance<Construction>()     && !city.HasBuilding<Colosseum>()) Consider(new Colosseum());
				if (Player.HasAdvance<Religion>()         && !city.HasBuilding<Cathedral>()) Consider(new Cathedral());
				if (Player.HasAdvance<Pottery>()          && !city.HasBuilding<Granary>())   Consider(new Granary());
			}

			// Militarize: garrison up to 2, then attackers
			if (stance == StrategyStance.Militarize)
			{
				if (defenders < 2) Consider(BestDefender());
				if (!Player.RepublicDemocratic) Consider(BestAttacker());
			}

			// Expand: settlers when city is large enough
			if (stance == StrategyStance.Expand)
			{
				int minSize = Leader.Development == Expansionistic ? 2
				            : Leader.Development == Normal          ? 3 : 4;
				int maxCities = Leader.Development == Expansionistic ? 13
				              : Leader.Development == Normal          ? 10 : 7;
				if (city.Size >= minSize && !city.Units.Any(x => x is Settlers)
				    && Player.Cities.Length < maxCities)
					Consider(new Settlers());
			}

			// Standard infrastructure chain (all stances)
			if (Player.HasAdvance<Pottery>()           && !city.HasBuilding<Granary>())      Consider(new Granary());
			if (Player.HasAdvance<CeremonialBurial>()  && !city.HasBuilding<Temple>())        Consider(new Temple());
			if (Player.HasAdvance<Writing>()           && !city.HasBuilding<Library>())       Consider(new Library());
			if (Player.HasAdvance<Currency>()          && !city.HasBuilding<MarketPlace>())   Consider(new MarketPlace());
			if (Player.HasAdvance<Masonry>()           && !city.HasBuilding<CityWalls>())     Consider(new CityWalls());
			if (Player.HasAdvance<Construction>()      && !city.HasBuilding<Colosseum>())     Consider(new Colosseum());
			if (Player.HasAdvance<Religion>()          && !city.HasBuilding<Cathedral>())     Consider(new Cathedral());

			// Wonder: only for the empire's top production city
			IWonder wonder = SelectWonder(city, stance);
			if (wonder != null) Consider(wonder);

			// Second defender once infrastructure is underway
			if (defenders < 2) Consider(BestDefender());

			// Soft units by government / stance
			if (stance == StrategyStance.Militarize && !Player.RepublicDemocratic)
				Consider(BestAttacker());
			else if (Player.HasAdvance<Writing>())
				Consider(new Diplomat());
			else if (Player.HasAdvance<Trade>())
				Consider(new Caravan());

			// Fallback: first available production item
			if (plan.Count == 0)
			{
				IProduction[] items = city.AvailableProduction.ToArray();
				Consider(items[Common.Random.Next(items.Length)]);
			}

			return plan;
		}
	}
}
