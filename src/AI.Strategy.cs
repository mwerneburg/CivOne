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
using CivOne.Tasks;
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

			// Militarize: barbarian city visible near our empire — rally to expel them
			if (Game.GetCities().Any(c => c.Owner == 0
			    && Player.Cities.Any(oc => Common.DistanceToTile(c.X, c.Y, oc.X, oc.Y) <= 10)
			    && Player.Visible(c.X, c.Y)))
				return StrategyStance.Militarize;

			// Militarize: human player is running away — drop economic goals and arm up
			if (HumanIsDominant() && Human is not null && IsNeighbor(Human))
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

			// Expand: below the leader's preferred city count (scales with difficulty and map size).
			int mapScale = Math.Max(1, (Map.WIDTH * Map.HEIGHT + 2000) / 4000);
			int target = Leader.Development == Expansionistic ? (9 * mapScale) + Game.Difficulty
			           : Leader.Development == Normal          ? (6 * mapScale) + Game.Difficulty
			           :                                         (4 * mapScale) + Game.Difficulty;
			if (cities.Length < target) return StrategyStance.Expand;

			return StrategyStance.Develop;
		}

		private bool IsNeighbor(Player enemy)
		{
			return Player.Cities.Any(oc =>
			    enemy.Cities.Any(ec =>
			        Common.DistanceToTile(oc.X, oc.Y, ec.X, ec.Y) <= 15));
		}

		// True when the human player has broken away from the pack: 2× the cities or 2× the
		// score of the strongest AI.  8-city floor on the city check so it doesn't fire in
		// the early expansion phase before civs have had a chance to settle.
		private bool HumanIsDominant()
		{
			Player human = Human;
			if (human is null || human.IsDestroyed()) return false;

			Player[] aiPlayers = Game.Players
			    .Where(p => Game.PlayerNumber(p) != 0 && !p.IsDestroyed() && p != human)
			    .ToArray();
			if (aiPlayers.Length == 0) return false;

			int humanCities = human.Cities.Length;
			int humanScore  = Math.Max(1, human.Score);
			int bestAICities = aiPlayers.Max(p => p.Cities.Length);
			int bestAIScore  = aiPlayers.Max(p => Math.Max(1, p.Score));

			if (humanCities >= 8 && humanCities > bestAICities * 2) return true;
			if (humanScore > bestAIScore * 2) return true;
			return false;
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

			if (BestGovernment() is null) return; // already optimal

			// ~25 % chance per turn → roughly 4-turn lag before acting
			if (Common.Random.Next(100) < 25)
				Player.Revolt();
		}

		// ── proactive diplomacy ───────────────────────────────────────────────────

		internal void ConsiderDiplomacy()
		{
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Civilization is Olvir) return; // refugees seek coexistence, not negotiation
			if (Player.Government is Governments.Anarchy) return;

			if (Player.IsDestroyed()) return;

			Player human = Human;
			if (human is null || human == Player || human.IsDestroyed()) return;

			// Only approach if we've spotted at least one of their cities
			if (!Game.GetCities().Any(c => c.Player == human && Player.Visible(c.X, c.Y))) return;

			// Base ~3 % per turn; personality and war status nudge the odds
			int chance = 3;
			if (Leader.Aggression == AggressionLevel.Aggressive) chance += 4;
			if (Leader.Militarism == MilitarismLevel.Militaristic) chance += 2;
			if (Leader.Aggression == AggressionLevel.Friendly)    chance += 4;
			if (Player.IsAtWar(human))                             chance += 6;

			if (Common.Random.Next(100) >= chance) return;

			GameTask.Enqueue(Show.MeetKing(Player, aiInitiated: true));
		}

		// ── background map trading ────────────────────────────────────────────────

		internal void ConsiderMapTrade()
		{
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Government is Governments.Anarchy) return;
			if (Player.IsDestroyed()) return;

			// ~3 % chance per turn to consider a map trade
			if (Common.Random.Next(100) >= 3) return;

			// Pick a random non-barbarian, non-hostile AI partner that has an embassy
			Player[] candidates = Game.Players
				.Where(p => p != Player
				         && !p.IsDestroyed()
				         && Game.PlayerNumber(p) != 0   // not barbarians
				         && !p.IsHuman
				         && !Player.IsAtWar(p)
				         && Player.HasEmbassy(p))
				.ToArray();

			if (candidates.Length == 0) return;

			Player partner = candidates[Common.Random.Next(candidates.Length)];

			bool weHaveNew   = Player.HasNewVisibilityFor(partner);
			bool theyHaveNew = partner.HasNewVisibilityFor(Player);
			if (!weHaveNew && !theyHaveNew) return;

			Player.MergeVisibility(partner);
			partner.MergeVisibility(Player);
		}

		// ── proactive war declaration ──────────────────────────────────────────

		internal void ConsiderWar()
		{
			// Barbarians use their own logic; governments in revolution are distracted
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Civilization is Olvir) return; // refugees do not declare war
			if (Player.Government is Governments.Anarchy) return;

			// ── Coalition against a runaway human ────────────────────────────────
			if (HumanIsDominant())
			{
				// Wind down existing AI-vs-AI wars (~25 % chance per war per turn).
				// MakePeace is bilateral so only one side needs to call it.
				foreach (Player ally in Game.Players)
				{
					if (ally == Player || ally.IsDestroyed() || ally.IsHuman) continue;
					if (Game.PlayerNumber(ally) == 0) continue; // not barbarians
					if (!Player.IsAtWar(ally)) continue;
					if (Common.Random.Next(100) < 25)
						Player.MakePeace(ally);
				}

				// Non-Rep/Dem civs with an army may now turn on the human
				if (!Player.RepublicDemocratic)
				{
					int own = MilitaryScore(Player);
					Player human = Human;
					if (own > 0 && human is not null && !human.IsDestroyed()
					    && !Player.IsAtWar(human) && IsNeighbor(human)
					    && !human.HasWonder<UnitedNations>())
					{
						// Base 10 %, +5 % per difficulty level, +10 % if we can field half their force
						int chance = 10 + Game.Difficulty * 5;
						if (own >= MilitaryScore(human) / 2) chance += 10;
						if (Common.Random.Next(100) < chance)
							Player.DeclareWar(human);
					}
				}

				// No new inter-AI wars while coalition is active
				return;
			}
			// ── Normal war logic below ───────────────────────────────────────────

			// Republics and Democracies are blocked by their Senate from starting wars
			if (Player.RepublicDemocratic) return;

			// Civilised non-aggressive leaders don't pick fights
			if (Leader.Militarism == MilitarismLevel.Civilized
			    && Leader.Aggression != AggressionLevel.Aggressive)
				return;

			// Don't pick fights with other civs while barbarians hold a city near our empire
			if (Game.GetCities().Any(c => c.Owner == 0
			    && Player.Cities.Any(oc => Common.DistanceToTile(c.X, c.Y, oc.X, oc.Y) <= 10)
			    && Player.Visible(c.X, c.Y)))
				return;

			int ownScore = MilitaryScore(Player);
			if (ownScore == 0) return; // no army, no war

			foreach (Player enemy in Game.Players)
			{
				if (enemy == Player || enemy.IsDestroyed()) continue;
				if (Player.IsAtWar(enemy)) continue;
				if (!IsNeighbor(enemy)) continue;
				if (enemy.HasWonder<UnitedNations>()) continue;

				int their = MilitaryScore(enemy);

				// Base chance from leader personality + difficulty bonus
				int chance = Game.Difficulty * 3;
				if (Leader.Aggression  == AggressionLevel.Aggressive)    chance += 8;
				if (Leader.Militarism  == MilitarismLevel.Militaristic)   chance += 7;

				// Modifier for relative strength
				if (ownScore > their)             chance += 5;
				if (ownScore > their * 3 / 2)     chance += 5; // notably stronger
				if (their > ownScore * 3 / 2)     chance -= 20; // notably weaker — don't be reckless

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
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;

			// Resource value of every tile in the working diamond.
			// Ocean tiles get a +2 premium for long-term coastal trade potential.
			// Special resource tiles get +3 for improvement headroom (mines, irrigation).
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue;
				int tx = (center.X + dx + mapWidth) % mapWidth;
				int ty = center.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null) continue;
				score += tile.Food * 2 + tile.Shield + tile.Trade;
				if (tile.IsOcean) score += 2;
				if (tile.Special)  score += 3;
			}

			// Immediate neighbours: river adjacency unlocks irrigation chains.
			// Track whether we have both coastal and river neighbours for the
			// river-mouth synergy bonus below.
			bool hasCoastNeighbor = false, hasRiverNeighbor = false;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (center.X + dx + mapWidth) % mapWidth;
				int ty = center.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is River)             { score += 3; hasRiverNeighbor  = true; }
				else if (tile is not null && tile.IsOcean) hasCoastNeighbor = true;
			}

			// A river-mouth site combines irrigation, river trade, and ocean trade.
			if (hasCoastNeighbor && hasRiverNeighbor) score += 6;

			// City proximity penalties
			foreach (City city in Game.GetCities())
			{
				int dist = Common.DistanceToTile(center.X, center.Y, city.X, city.Y);
				if (dist < 4) { score -= 20; continue; } // working-radius overlap
				if (dist < 6) { score -= 5;  continue; }
				// Foreign city in the 6–10 band: contested border risk
				if (city.Player != Player && dist < 10)
					score -= Player.IsAtWar(city.Player) ? 10 : 4;
			}

			// Prefer sites that extend the empire rather than leap into the void.
			if (Player.Cities.Any(c =>
			    Common.DistanceToTile(center.X, center.Y, c.X, c.Y) <= 6))
				score += 10;

			return score;
		}

		internal ITile BestSettleSite(IUnit settlers)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile best = null;
			int bestScore = int.MinValue;

			byte ownId = Game.PlayerNumber(Player);
			var claimedGotos = new System.Collections.Generic.HashSet<(int, int)>(
				Game.GetUnits().OfType<Settlers>()
				    .Where(s => s != settlers && s.Owner == ownId && !s.Goto.IsEmpty)
				    .Select(s => (s.Goto.X, s.Goto.Y)));

			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				int tx = (settlers.X + dx + mapWidth) % mapWidth;
				int ty = settlers.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean || tile.City is not null) continue;
				if (Game.GetCities().Any(c => Common.DistanceToTile(c.X, c.Y, tx, ty) < 4)) continue;
				if (claimedGotos.Contains((tx, ty))) continue;
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
			// Prefer the weakest (fewest defenders) visible enemy city closest to our empire.
			// Barbarians (P0) are treated as always hostile even without a formal war state.
			var candidates = Game.GetCities()
			    .Where(c => c.Player != Player
			             && (Player.IsAtWar(c.Player) || c.Owner == 0)
			             && Player.Visible(c.X, c.Y));

			// When the human is dominant and we're at war with them, hit their cities first.
			Player human = Human;
			if (HumanIsDominant() && human is not null && Player.IsAtWar(human))
			{
				City humanCity = candidates
				    .Where(c => c.Player == human)
				    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
				    .ThenBy(c => Player.Cities.Min(oc => Common.DistanceToTile(oc.X, oc.Y, c.X, c.Y)))
				    .FirstOrDefault();
				if (humanCity is not null) return humanCity;
			}

			return candidates
			    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
			    .ThenBy(c => Player.Cities.Min(oc => Common.DistanceToTile(oc.X, oc.Y, c.X, c.Y)))
			    .FirstOrDefault();
		}

		private ITile StagingTile(City target)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			byte own = Game.PlayerNumber(Player);
			ITile best = null;
			int bestCount = -1;

			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (target.X + dx + mapWidth) % mapWidth;
				int ty = target.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean) continue;
				// Don't stage on a tile already occupied by enemies
				if (tile.Units.Any(u => u.Owner != own)) continue;
				int count = tile.Units.Count(u => u.Owner == own && u.Role == UnitRole.LandAttack);
				if (best is null || count > bestCount) { best = tile; bestCount = count; }
			}
			return best;
		}

		// ── naval transport helpers ───────────────────────────────────────────────

		// Ocean tile adjacent to a city — where a transport can drop troops.
		private ITile LandingTile(City target)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (target.X + dx + mapWidth) % mapWidth;
				int ty = target.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is not null && tile.IsOcean) return tile;
			}
			return null;
		}

		// Own coastal city that has land attackers waiting for a ride.
		private City EmbarkationCity()
		{
			byte own = Game.PlayerNumber(Player);
			return Player.Cities
			             .Where(c => c.Tile.GetBorderTiles().Any(t => t.IsOcean)
			                      && c.Tile.Units.Any(u => u.Owner == own && u.Role == UnitRole.LandAttack))
			             .OrderByDescending(c => c.Tile.Units.Count(u => u.Owner == own && u.Role == UnitRole.LandAttack))
			             .FirstOrDefault();
		}

		// Ocean tile adjacent to the given city where a transport can wait.
		private ITile EmbarkationTile(City city)
		{
			byte own = Game.PlayerNumber(Player);
			return city.Tile.GetBorderTiles()
			           .Where(t => t is not null && t.IsOcean)
			           .OrderByDescending(t => t.Units.Count(u => u.Owner == own && u is IBoardable))
			           .FirstOrDefault();
		}

		private void AssignMission(IUnit unit)
		{
			StrategyStance stance = GetStance();

			// Naval units
			if (unit.Class == UnitClass.Water)
			{
				if (unit is IBoardable)
				{
					byte own = Game.PlayerNumber(Player);
					bool hasPassengers = unit.Tile.Units.Any(u => u.Owner == own && u.Class == UnitClass.Land);

					if (hasPassengers && _attackTarget is not null)
					{
						ITile landing = LandingTile(_attackTarget);
						if (landing is not null)
						{
							// Already at the landing zone — unload so troops can storm the beach
							if (Common.DistanceToTile(unit.X, unit.Y, _attackTarget.X, _attackTarget.Y) <= 2)
							{
								(unit as BaseUnitSea).Unload();
								return;
							}
							unit.Goto = new Point(landing.X, landing.Y);
							return;
						}
					}

					// No passengers (or no target): wait at a coastal city for troops
					City embark = EmbarkationCity();
					if (embark is not null)
					{
						ITile pier = EmbarkationTile(embark);
						if (pier is not null) { unit.Goto = new Point(pier.X, pier.Y); return; }
					}
				}

				// Warships and fallback: patrol nearest own city
				City port = Player.Cities
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault();
				if (port is not null) unit.Goto = new Point(port.X, port.Y);
				return;
			}

			// Explorers: head for the nearest unseen tile
			if (unit is Explorer)
			{
				ITile dest = BestExploreTile(unit);
				if (dest is not null) unit.Goto = new Point(dest.X, dest.Y);
				return;
			}

			// Diplomats: head for the nearest visible foreign city
			if (unit is Diplomat)
			{
				City target = Game.GetCities()
				    .Where(c => c.Player != Player && Player.Visible(c.X, c.Y))
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault();
				if (target is not null) unit.Goto = new Point(target.X, target.Y);
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
				if (target is not null) unit.Goto = new Point(target.X, target.Y);
				return;
			}

			// Offensive land units
			if (unit.Role == UnitRole.LandAttack)
			{
				if (stance == StrategyStance.Militarize)
				{
					// Validate or refresh the civ-wide attack target.
					// Barbarian cities stay valid until captured; non-barbarian targets
					// are dropped when the war ends.
					bool targetStale = _attackTarget is null
					    || _attackTarget.Player == Player
					    || (_attackTarget.Owner != 0 && !Player.IsAtWar(_attackTarget.Player));
					if (targetStale)
						_attackTarget = PickAttackTarget();

					if (_attackTarget is not null)
					{
						ITile staging = StagingTile(_attackTarget);
						byte own = Game.PlayerNumber(Player);

						// How many attackers are already at the staging tile?
						int staged = staging?.Units.Count(u =>
						    u.Owner == own && u.Role == UnitRole.LandAttack) ?? 0;

						// Commit when we have enough force; be generous if we outbuilt the defense
						int defenders = _attackTarget.Tile.Units.Count(u => u.Role == UnitRole.Defense);
						int threshold = Math.Max(2, defenders + 1);

						Point dest = (staged >= threshold || staging is null)
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
				if (needsHelp is not null) unit.Goto = new Point(needsHelp.X, needsHelp.Y);
			}
		}

		// ── research weights ──────────────────────────────────────────────────

		// Returns how much the AI values acquiring a given advance right now.
		// Used by the King screen to pick the advance it demands in a trade.
		internal int AdvanceDemandValue(IAdvance a) => AdvanceWeight(a, GetStance());

		private static int AdvanceWeight(IAdvance a, StrategyStance stance)
		{
			int weight = 1; // baseline: every advance can be chosen

			switch (stance)
			{
				case StrategyStance.Militarize:
					if (a is BronzeWorking)      weight += 7;
					if (a is IronWorking)         weight += 7;
					if (a is TheWheel)            weight += 6;
					if (a is HorsebackRiding)     weight += 7;
					if (a is Feudalism)           weight += 5;
					if (a is Chivalry)            weight += 7;
					if (a is Gunpowder)           weight += 8;
					if (a is Mathematics)         weight += 4;
					if (a is Physics)             weight += 5;
					if (a is Chemistry)           weight += 5;
					if (a is Metallurgy)          weight += 7;
					if (a is Engineering)         weight += 5;
					if (a is SteamEngine)         weight += 5;
					if (a is Industrialization)   weight += 6;
					if (a is Conscription)        weight += 8;
					if (a is Automobile)          weight += 8;
					if (a is LaborUnion)          weight += 8;
					if (a is Masonry)             weight += 4; // CityWalls for defence
					break;

				case StrategyStance.Develop:
					if (a is Alphabet)            weight += 7;
					if (a is Writing)             weight += 8;
					if (a is Literacy)            weight += 6;
					if (a is CodeOfLaws)          weight += 6;
					if (a is TheRepublic)         weight += 7;
					if (a is Advances.Democracy)  weight += 6;
					if (a is Pottery)             weight += 6;
					if (a is Trade)               weight += 8;
					if (a is Currency)            weight += 7;
					if (a is Banking)             weight += 7;
					if (a is TheCorporation)      weight += 6;
					if (a is Philosophy)          weight += 5;
					if (a is Advances.University)  weight += 7;
					if (a is Invention)           weight += 6;
					if (a is TheoryOfGravity)     weight += 6;
					if (a is Masonry)             weight += 5;
					if (a is Construction)        weight += 5;
					if (a is CeremonialBurial)    weight += 5;
					if (a is Mysticism)           weight += 4;
					if (a is Religion)            weight += 5;
					break;

				case StrategyStance.Consolidate:
					if (a is CeremonialBurial)    weight += 9; // Temple
					if (a is Mysticism)           weight += 8; // doubles Temple
					if (a is Philosophy)          weight += 6;
					if (a is Religion)            weight += 8; // Cathedral
					if (a is Construction)        weight += 8; // Colosseum
					if (a is Pottery)             weight += 7; // Granary
					if (a is Trade)               weight += 6;
					if (a is Currency)            weight += 6;
					if (a is Banking)             weight += 5;
					if (a is Writing)             weight += 5;
					break;

				case StrategyStance.Expand:
					if (a is Pottery)             weight += 8; // Granary feeds growth
					if (a is BridgeBuilding)      weight += 7; // roads cross rivers
					if (a is RailRoad)            weight += 7; // fast movement
					if (a is Masonry)             weight += 6;
					if (a is MapMaking)           weight += 5; // explore coasts
					if (a is Alphabet)            weight += 5;
					if (a is Writing)             weight += 5;
					if (a is Trade)               weight += 5;
					if (a is TheWheel)            weight += 5;
					if (a is HorsebackRiding)     weight += 5;
					break;
			}

			return weight;
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

			// Prioritise the dome component assigned to this civilisation, if any
			Enums.Wonder? assignment = Game.Instance.GetDomeAssignment(Player);
			if (assignment.HasValue)
			{
				IWonder assigned = Reflect.GetWonders().FirstOrDefault(w => w.Id == (byte)assignment.Value);
				if (assigned is not null && !Game.WonderBuilt(assigned) && Player.ProductionAvailable(assigned))
					return assigned;
			}

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
					new Colossus(), new Lighthouse(), new MagellansExpedition(),
					new MarcoPoloVoyage(), new ZhengHeVoyage()
				};
			}

			return preferred.FirstOrDefault(w =>
				!Game.WonderBuilt(w) && Player.ProductionAvailable(w));
		}

		// ── full production plan for a city ────────────────────────────────────

		private List<IProduction> PlanProduction(City city, StrategyStance stance)
		{
			return PlanProductionInto(new List<IProduction>(), city, stance);
		}

		private List<IProduction> PlanProductionInto(List<IProduction> plan, City city, StrategyStance stance)
		{
			void Consider(IProduction p)
			{
				if (plan.All(x => x.GetType() != p.GetType())) plan.Add(p);
			}

			int defenders = city.Tile.Units.Count(u => u.Role == UnitRole.Defense);

			// Universal first: barracks and minimum garrison
			if (!city.HasBuilding<Barracks>()) Consider(new Barracks());
			if (defenders < 1)                Consider(BestDefender());

			int ownCities = Player.Cities.Length;
			int maxCities = Leader.Development == Expansionistic ? 13
			              : Leader.Development == Normal          ? 10 : 7;

			// Tiny-empire settlers: < 3 cities → skip Explorer, build settlers immediately
			// after first defender so the civ doesn't stagnate.
			// Safe minSize: solo city can be size 1 (game protects it), otherwise 2.
			if (ownCities < 3 && stance != StrategyStance.Consolidate)
			{
				int minSize = ownCities == 1 ? 1 : 2;
				if (city.Size >= minSize && !city.Units.Any(x => x is Settlers) && ownCities < maxCities)
					Consider(new Settlers());
			}

			// Explorer: one per 3 cities while the map still has fog-of-war to reveal
			{
				byte ownId = Game.PlayerNumber(Player);
				int ownExplorers = Game.GetUnits().Count(u => u.Owner == ownId && u is Explorer);
				int explorerCap  = Math.Max(1, ownCities / 3);
				if (ownExplorers < explorerCap) Consider(new Explorer());
			}

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

			// Expand: infrastructure before settlers, then settlers when city is large enough.
			// Granary goes first so food investment lands before population is spent.
			// minSize raised so cities consolidate at size 3+ before spawning settlers.
			if (stance == StrategyStance.Expand && ownCities >= 3)
			{
				if (Player.HasAdvance<Pottery>() && !city.HasBuilding<Granary>()) Consider(new Granary());
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>()) Consider(new Temple());
				int minSize = Leader.Development == Expansionistic ? 3
				            : Leader.Development == Normal          ? 4 : 4;
				if (city.Size >= minSize && !city.Units.Any(x => x is Settlers) && ownCities < maxCities)
					Consider(new Settlers());
			}

			// Standard infrastructure chain (all stances)
			if (Player.HasAdvance<Pottery>()           && !city.HasBuilding<Granary>())      Consider(new Granary());
			if (Player.HasAdvance<CeremonialBurial>()  && !city.HasBuilding<Temple>())        Consider(new Temple());
			if (Player.HasAdvance<Writing>()           && !city.HasBuilding<Library>())       Consider(new Library());
			if (Player.HasAdvance<Currency>()          && !city.HasBuilding<MarketPlace>())   Consider(new MarketPlace());
			if (Player.HasAdvance<Masonry>()           && !city.HasBuilding<CityWalls>())     Consider(new CityWalls());
			if (Player.HasAdvance<Rocketry>()          && !city.HasBuilding<SamBattery>())    Consider(new SamBattery());
			if (Player.HasAdvance<Construction>()      && !city.HasBuilding<Colosseum>())     Consider(new Colosseum());
			if (Player.HasAdvance<Religion>()          && !city.HasBuilding<Cathedral>())     Consider(new Cathedral());
			if (Player.HasAdvance<Computers>()         && !city.HasBuilding<Observatory>())   Consider(new Observatory());

			// Wonder: only for the empire's top production city
			IWonder wonder = SelectWonder(city, stance);
			if (wonder is not null) Consider(wonder);

			// Second defender once infrastructure is underway
			if (defenders < 2) Consider(BestDefender());

			// Soft units by government / stance
			if (stance == StrategyStance.Militarize && !Player.RepublicDemocratic)
			{
				Consider(BestAttacker());
			}
			else if (Player.HasAdvance<Writing>())
			{
				// One Diplomat per 3 cities (same cadence as Explorers), minimum 2 empire-wide.
				byte ownId2 = Game.PlayerNumber(Player);
				int ownDiplomats = Game.GetUnits().Count(u => u.Owner == ownId2 && u is Diplomat);
				int diplomatCap  = Math.Max(2, Player.Cities.Length / 3);
				if (ownDiplomats < diplomatCap)
					Consider(new Diplomat());
				else if (Player.HasAdvance<Trade>())
					Consider(new Caravan());
			}
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

		// ── Chieftain-specific production plan ────────────────────────────────

		private List<IProduction> PlanChieftain(City city, StrategyStance stance)
		{
			var plan = new List<IProduction>();

			int defenders  = city.Tile.Units.Count(u => u.Role == UnitRole.Defense);
			byte ownId     = Game.PlayerNumber(Player);
			int ownCities  = Player.Cities.Length;
			int ownMilitia = Game.GetUnits().Count(u => u.Owner == ownId && u is Militia);
			int ownSettlers = Game.GetUnits().Count(u => u.Owner == ownId && u is Settlers);

			// 1. Defensive unit if city is undefended
			if (defenders < 1) plan.Add(BestDefender());

			// 2. Explorer — one free scout early on
			if (Game.GetUnits().Count(u => u.Owner == ownId && u is Explorer) < 1)
				plan.Add(new Explorer());

			// 3. Barracks
			if (!city.HasBuilding<Barracks>()) plan.Add(new Barracks());

			// 3. Militia — capped at 4× city count
			if (ownMilitia < ownCities * 4 && plan.All(x => !(x is Militia)))
				plan.Add(new Militia());

			// 4. Settler — size >= 4 so the city stays viable at size 3; cap at 1 per 2 cities
			if (city.Size >= 4 && ownSettlers < Math.Max(1, ownCities / 2) && plan.All(x => !(x is Settlers)))
				plan.Add(new Settlers());

			// 5. Temple
			if (!city.HasBuilding<Temple>()) plan.Add(new Temple());

			// 6. Append standard plan items (no duplicates)
			PlanProductionInto(plan, city, stance);

			return plan;
		}

		// ── exploration helpers ───────────────────────────────────────────────

		internal ITile BestExploreTile(IUnit unit)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile best = null;
			int bestScore = 0; // only move if it adds value

			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (unit.X + dx + mapWidth) % mapWidth;
				int ty = unit.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean) continue;
				int dist = Common.DistanceToTile(unit.X, unit.Y, tx, ty);
				int score = CountUnseenTiles(tx, ty) - dist;
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

	private int CountUnseenTiles(int x, int y)
		{
			int count = 0;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				int tx = (x + dx + Map.WIDTH) % Map.WIDTH;
				int ty = y + dy;
				if (ty < 0 || ty >= Map.HEIGHT) continue;
				if (!Player.Visible(tx, ty)) count++;
			}
			return count;
		}

		// ── settler improvement selection ──────────────────────────────────────

		private enum SettlerImprovement { Road, Irrigation, Mine, None }

		private SettlerImprovement ChooseSettlerImprovement(
		    IUnit unit, bool validRoad, bool validIrrigation, bool validMine, int nearestOwnCity)
		{
		    StrategyStance stance = GetStance();
		    
		    // Expansion phase: build roads to unlock new settlement paths
		    if (stance == StrategyStance.Expand)
		        return validRoad ? SettlerImprovement.Road : 
		               validIrrigation ? SettlerImprovement.Irrigation :
		               validMine ? SettlerImprovement.Mine : SettlerImprovement.None;
		    
		    // Consolidation: irrigation → food → growth
		    if (stance == StrategyStance.Consolidate)
		        return validIrrigation ? SettlerImprovement.Irrigation :
		               validRoad ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine : SettlerImprovement.None;
		    
		    // Militarization: roads first for rapid troop movement
		    if (stance == StrategyStance.Militarize)
		        return validRoad ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine :
		               validIrrigation ? SettlerImprovement.Irrigation : SettlerImprovement.None;
		    
		    // Default development: prioritize water access, then shields, then roads
		    return validIrrigation ? SettlerImprovement.Irrigation :
		           validMine ? SettlerImprovement.Mine :
		           validRoad ? SettlerImprovement.Road : SettlerImprovement.None;
		}

		// ── Olvir improvement helpers ─────────────────────────────────────────

		// Pick the Olvir improvement type that best suits a given tile.
		internal static OlvirImprovementType OlvirImprovementFor(ITile tile)
		{
			if (tile.GetBorderTiles().Any(b => b.IsOcean))        return OlvirImprovementType.Aquafarm;
			if (tile is Forest || tile is Jungle)                  return OlvirImprovementType.CanopyArray;
			if (tile is Hills  || tile is Mountains)               return OlvirImprovementType.RepairBay;
			return (tile.X + tile.Y) % 2 == 0
				? OlvirImprovementType.ExchangeNode
				: OlvirImprovementType.BiofilterWall;
		}

		// Find the nearest unimproved land tile within the working radius of any
		// Olvir city.  Returns null if everything reachable is already developed.
		internal ITile BestOlvirImproveSite(IUnit settler)
		{
			byte ownId = Game.PlayerNumber(Player);
			City[] ownCities = Game.GetCities().Where(c => c.Owner == ownId).ToArray();
			if (ownCities.Length == 0) return null;

			ITile best = null;
			int bestDist = int.MaxValue;

			foreach (City city in ownCities)
			{
				for (int dy = -4; dy <= 4; dy++)
				for (int dx = -4; dx <= 4; dx++)
				{
					int tx = (city.X + dx + Map.WIDTH) % Map.WIDTH;
					int ty = city.Y + dy;
					if (ty < 0 || ty >= Map.HEIGHT) continue;
					ITile tile = Map[tx, ty];
					if (tile is null || tile.IsOcean || tile.City is not null) continue;
					if (Game.Instance.OlvirImprovements.ContainsKey((tx, ty))) continue;
					int dist = Common.DistanceToTile(settler.X, settler.Y, tx, ty);
					if (dist < bestDist) { bestDist = dist; best = tile; }
				}
			}
			return best;
		}
	}
}
