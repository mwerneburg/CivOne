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
			// Consolidate: a happiness crisis — drop expansion and build Temples/Colosseums/
			// Cathedrals (this stance front-loads them). Republics/Democracies feel unhappiness
			// early, so they consolidate on widespread discontent; the harsher governments only
			// once cities actually tip into disorder. The LuxuriesRate >= 4 clause keeps us in
			// Consolidate while ConsiderSliders is leaning on the luxury slider, so we keep
			// building happiness infrastructure until luxuries can wind back down toward science.
			if (cities.Length > 0 && (
			        (Player.RepublicDemocratic && cities.Count(c => c.UnhappyCitizens > 0) * 2 > cities.Length)
			        || cities.Count(c => c.IsInDisorder) >= 2
			        || Player.LuxuriesRate >= 4))
				return StrategyStance.Consolidate;

			// Militarize: already at war
			if (Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p)))
				return StrategyStance.Militarize;

			// Militarize: barbarian city visible near our empire — rally to expel them
			if (Game.GetCities().Any(c => c.Owner == 0
			    && Player.Cities.Any(oc => Common.DistanceToTile(c.X, c.Y, oc.X, oc.Y) <= 10)
			    && Player.Visible(c.X, c.Y)))
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
			// mapScale uses WIDTH/80 (linear) rather than (W×H)/4000 (area) so Epic 320×200
			// produces scale=4, not 16. The area formula gave Normal-development leaders a
			// 99-city target on Epic — unreachable in practice — so every civ stayed in
			// Expand forever, never flipped research priorities to Trade/Currency/Banking,
			// never reached Republic, never escaped the Despotism tile penalty. The linear
			// scale matches the civ-separation knob in Game.NewGame.cs:32 (same source).
			int mapScale = Math.Max(1, Map.WIDTH / 80);
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

		// ── tax/science slider management ─────────────────────────────────────

		internal void ConsiderSliders()
		{
			if (Player.IsDestroyed()) return;
			if (Player.Government is Gov.Anarchy) return;

			// Happiness safety valve: pump luxuries UP while cities are rioting, then wind them
			// back down once order returns. Civil disorder freezes a city's production AND its
			// growth, so with luxuries pinned at 0 and too few happiness buildings, a growing
			// empire tips into the grow→riot→shrink→recover oscillation (the mid-game "wasting
			// illness"). Raising luxuries quells it far faster than waiting for a Temple to
			// finish; it costs science, but GetStance flips to Consolidate to build the
			// happiness infrastructure that lets luxuries fall back toward research.
			int rioting = Player.Cities.Count(c => c.IsInDisorder);
			if (rioting > 0)
			{
				int maxLux = 10 - Player.TaxesRate;   // keep science >= 0
				if (Player.LuxuriesRate < maxLux)
					Player.LuxuriesRate = Math.Min(maxLux, Player.LuxuriesRate + (rioting >= 3 ? 2 : 1));
				return;
			}
			if (Player.LuxuriesRate > 0)
			{
				Player.LuxuriesRate--;
				return;
			}

			StrategyStance stance = GetStance();
			int gold = Player.Gold;
			int tax  = Player.TaxesRate;

			// Base target tax rate by strategic stance.
			int target = stance switch
			{
				StrategyStance.Militarize  => 6, // wars drain gold; lean on taxes
				StrategyStance.Develop     => 4, // peace dividend goes to science
				StrategyStance.Consolidate => 5,
				_                          => 5, // Expand
			};

			// Gold overlay: tighten taxes when broke, ease off when flush.
			if      (gold <  20) target = Math.Max(target, 8);
			else if (gold <  60) target = Math.Max(target, 7);
			else if (gold < 120) target = Math.Max(target, 6);
			else if (gold > 500) target = Math.Min(target, 4);
			else if (gold > 250) target = Math.Min(target, 5);

			// Keep science in [2, 8] and taxes in [2, 8].
			if (target < 2) target = 2;
			if (target > 8) target = 8;

			// Move one point per turn so the economy shifts smoothly.
			if      (tax < target) Player.TaxesRate = tax + 1;
			else if (tax > target) Player.TaxesRate = tax - 1;
		}

		// ── rush-buy logic ─────────────────────────────────────────────────────

		internal void ConsiderRushBuy()
		{
			if (Player.IsDestroyed()) return;
			if (Player.Government is Gov.Anarchy) return;
			if (Player.Gold < 20) return;

			StrategyStance stance = GetStance();

			foreach (City city in Player.Cities)
			{
				if (city.CurrentProduction is null) continue;
				if (city.Shields <= 0) continue; // no tail-end discount without prior investment
				if (city.IsInDisorder && city.CurrentProduction is IBuilding) continue;

				int fullCost = city.CurrentProduction.Price * 10;
				int gold     = Player.Gold;
				short buy    = city.BuyPrice;
				double done  = (double)city.Shields / fullCost;

				// Emergency: undefended city with an enemy land unit adjacent.
				// Rush the current production if it's a defender, even at low completion.
				if (city.CurrentProduction is IUnit emergUnit
				    && emergUnit.Role == UnitRole.Defense
				    && city.Tile.Units.Count(u => u.Role == UnitRole.Defense) == 0
				    && city.Tile.GetBorderTiles().Any(t =>
				           t.Units.Any(u => u.Owner != city.Owner
				                        && u.Role == UnitRole.LandAttack))
				    && gold >= buy)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Wonder: clinch it once > 70 % done — higher reserve to avoid going broke.
				if (city.CurrentProduction is IWonder
				    && done >= 0.7 && buy <= gold / 2 && gold - buy >= 60)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Tail-end: > 60 % done, affordable — matches how a human shops.
				// Keep 30g reserve to cover maintenance.
				if (done >= 0.6 && buy <= gold / 3 && gold - buy >= 30)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Militarize: more aggressive about completing attackers (> 50 % done).
				if (stance == StrategyStance.Militarize
				    && city.CurrentProduction is IUnit
				    && done >= 0.5 && buy <= gold / 4 && gold - buy >= 30)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
				}
			}
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

			if (BestGovernment() is null) return; // already optimal

			// Don't revolt while at war — the anarchy interregnum is too dangerous.
			if (Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p))) return;

			// Escaping Despotism is the single biggest economic win: it lifts the despot
			// tile penalty that suppresses irrigation and keeps cities tiny. Pursue it
			// eagerly — any stance, high chance — the moment a better government (Monarchy)
			// is available, rather than waiting for a Develop/Expand window.
			if (Player.Government is Gov.Despotism)
			{
				if (Common.Random.Next(100) < 60)
					Player.Revolt();
				return;
			}

			// Further upgrades (Monarchy → Republic/Democracy, etc.): only revolt from a
			// stable, developing position, and less often.
			StrategyStance stance = GetStance();
			if (stance == StrategyStance.Militarize || stance == StrategyStance.Consolidate) return;
			if (Common.Random.Next(100) < 25)
				Player.Revolt();
		}

		// ── proactive diplomacy ───────────────────────────────────────────────────

		internal List<AIDemand> GenerateDemands(Player human)
		{
			var demands = new List<AIDemand>();
			byte aiNum    = (byte)Game.PlayerNumber(Player);
			byte humanNum = (byte)Game.PlayerNumber(human);
			bool atWar    = Player.IsAtWar(human);

			if (atWar)
			{
				// At war: ask for ONE captured city back in exchange for peace — the most
				// valuable (largest) one. Listing every lost city at once reads ridiculously.
				City? wantBack = Game.GetCities()
					.Where(c => c.Owner == humanNum && c.OriginalOwner == aiNum)
					.OrderByDescending(c => c.Size)
					.FirstOrDefault();
				if (wantBack is not null)
					demands.Add(new AIDemand(AIDemandKind.ReturnCity, city: wantBack, duration: 100));
			}
			else if (Game.GameTurn >= 30)
			{
				// Check for grievance: AI has 2+ cities held by the human.
				City[] capturedByHuman = Game.GetCities()
					.Where(c => c.Owner == humanNum && c.OriginalOwner == aiNum)
					.ToArray();
				if (capturedByHuman.Length >= 2 && Game.GameTurn - LastGrievanceTurn >= 40)
				{
					// Formal grievance: one city back + one tech + gold.
					City wantBack = capturedByHuman.OrderByDescending(c => c.Size).First();

					IAdvance[] wantedTechs = human.Advances.Where(a => !Player.HasAdvance(a)).ToArray();
					IAdvance? wantedTech = wantedTechs.Length >= 1
						? wantedTechs.OrderByDescending(a => AdvanceDemandValue(a)).First()
						: null;

					int goldAmount = human.Gold >= 25 ? Math.Max(25, (int)(human.Gold * 0.2f)) : 0;

					LastGrievanceTurn = Game.GameTurn;
					demands.Add(new AIDemand(AIDemandKind.GrievancePack,
						city: wantBack, advance: wantedTech, amount: goldAmount, duration: 75));
					return demands;
				}

				// At peace: standard extortion for attitude bonus
				if (human.HasNewVisibilityFor(Player))
					demands.Add(new AIDemand(AIDemandKind.GiveMap, duration: 50));

				IAdvance[] techOptions = human.Advances.Where(a => !Player.HasAdvance(a)).ToArray();
				if (techOptions.Length >= 2)
				{
					int topWeight = techOptions.Max(a => AdvanceDemandValue(a));
					IAdvance[] top = techOptions.Where(a => AdvanceDemandValue(a) == topWeight).ToArray();
					demands.Add(new AIDemand(AIDemandKind.GiveTech, advance: top[Common.Random.Next(top.Length)], duration: 50));
				}

				if (human.Gold >= 25)
				{
					int amount = Math.Max(25, (int)(human.Gold * 0.25f));
					demands.Add(new AIDemand(AIDemandKind.GiveMoney, amount: amount, duration: 50));
				}

				City[] humanCities = Game.GetCities().Where(c => c.Owner == humanNum).ToArray();
				if (humanCities.Length >= 3)
				{
					City[] smallCities = humanCities.Where(c => c.Size <= 2 && !c.HasBuilding<Palace>()).ToArray();
					if (smallCities.Length > 0)
					{
						City[] aiCities = Game.GetCities().Where(c => c.Owner == aiNum).ToArray();
						City target = aiCities.Length > 0
							? smallCities.OrderBy(c => aiCities.Min(ac => Common.DistanceToTile(ac.X, ac.Y, c.X, c.Y))).First()
							: smallCities[Common.Random.Next(smallCities.Length)];
						demands.Add(new AIDemand(AIDemandKind.CedeCity, city: target, duration: 50));
					}
				}
			}

			return demands;
		}

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

			// Honour active goodwill / peace-treaty windows: no approaches until they expire.
			// The war channel stays open so the AI can still seek peace during a conflict.
			if (!Player.IsAtWar(human) &&
			    (Player.HasAttitudeBonus(human) || Player.HasPeaceTreaty(human)))
				return;

			// Humanitarian plea: a small civ at peace with a starving city begs the human for
			// aid rather than extorting. Far likelier than routine diplomacy so a doomed
			// frontier neighbour reaches out while it still can; the attitude-bonus return
			// above stops it begging again right after a successful airdrop.
			if (!Player.IsAtWar(human) && Player.Cities.Length <= 2)
			{
				City? dying = Player.Cities
					.Where(c => c.Size <= 2 && c.FoodIncome < 0)
					.OrderBy(c => c.Size).ThenBy(c => c.FoodIncome)
					.FirstOrDefault();
				if (dying is not null)
				{
					if (Common.Random.Next(100) >= 40) return;
					var plea = new List<AIDemand> { new AIDemand(AIDemandKind.BegForAid, city: dying, amount: 50, duration: 40) };
					GameTask.Enqueue(Show.MeetKing(Player, aiInitiated: true, demands: plea));
					return;
				}
			}

			// Base ~3 % per turn; personality and war status nudge the odds
			int chance = 3;
			if (Leader.Aggression == AggressionLevel.Aggressive) chance += 4;
			if (Leader.Militarism == MilitarismLevel.Militaristic) chance += 2;
			if (Leader.Aggression == AggressionLevel.Friendly)    chance += 4;
			if (Player.IsAtWar(human))                             chance += 6;

			if (Common.Random.Next(100) >= chance) return;

			List<AIDemand> demands = GenerateDemands(human);
			GameTask.Enqueue(Show.MeetKing(Player, aiInitiated: true, demands: demands));
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

			// ── Track war duration and peacetime city baseline ───────────────────
			bool atWar = Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p));
			if (atWar)
				_turnsAtWar++;
			else
			{
				_turnsAtWar      = 0;
				_peacetimeCities = Player.Cities.Length;
			}

			// ── Tribute pact (Layer 1) ───────────────────────────────────────────
			// A militarily outclassed AI civ at war with an AI neighbour it has an embassy
			// with offers tribute in exchange for peace. The protector accepts (no AI
			// refusal logic in Layer 1; the gold is free protection for them, costless to
			// agree). Tribute is the *better* outcome than the existing make-peace random
			// roll below for clearly losing civs — peace decays but tribute self-renews
			// each turn the gold flows, so the small civ stops bleeding shields on
			// futile attackers.
			if (atWar)
			{
				int ownPower = MilitaryScore(Player);
				Player[] tributeCandidates = Game.Players
				    .Where(p => p != Player && !p.IsDestroyed() && !p.IsHuman
				             && Game.PlayerNumber(p) != 0
				             && Player.IsAtWar(p)
				             && Player.HasEmbassy(p)
				             && ownPower * 2 < MilitaryScore(p))
				    .ToArray();
				if (tributeCandidates.Length > 0 && !Player.PaysTributeTo(tributeCandidates[0]))
				{
					Player protector = tributeCandidates.OrderByDescending(MilitaryScore).First();
					// Annual tribute scales with player gold income, clamped: 5 gold floor,
					// 25 gold ceiling. The cap matters because a tiny civ shouldn't price
					// itself out of survival, and a runaway civ shouldn't extract everything.
					int annual = Math.Max(5, Math.Min(25, Player.Gold / 20 + 5));
					if (Player.Gold >= annual)
					{
						Player.EstablishTribute(protector, annual);
					}
				}
			}

			// ── AI-vs-AI peace initiatives ───────────────────────────────────────
			if (atWar)
			{
				Player[] aiEnemies = Game.Players
				    .Where(p => p != Player && !p.IsDestroyed() && !p.IsHuman
				             && Game.PlayerNumber(p) != 0 && Player.IsAtWar(p))
				    .ToArray();

				if (aiEnemies.Length > 0)
				{
					// Sustained territory loss: net fewer cities than when the war began.
					bool losingTerritory = Player.Cities.Length < _peacetimeCities
					                    && Player.Cities.Length > 0;

					// War exhaustion: long campaign with an empty treasury.
					bool exhausted = _turnsAtWar > 40 && Player.Gold < 50;

					if (losingTerritory || exhausted)
					{
						int peaceChance = losingTerritory ? 30 : 20;
						foreach (Player enemy in aiEnemies)
						{
							if (Common.Random.Next(100) < peaceChance)
							{
								Player.MakePeace(enemy);
								break; // one treaty per turn
							}
						}
					}
				}
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

			// ── Expansion gate ───────────────────────────────────────────────────
			// Civs that still have room to grow prefer settlers to swords.
			// Militaristic/aggressive leaders can still fight but take a penalty;
			// everyone else waits until their empire is built out.
			// Linear map scale — see GetStance (line 71) for the rationale.
			int mapScale  = Math.Max(1, Map.WIDTH / 80);
			int cityTarget = Leader.Development == Expansionistic ? (9 * mapScale) + Game.Difficulty
			               : Leader.Development == Normal          ? (6 * mapScale) + Game.Difficulty
			               :                                         (4 * mapScale) + Game.Difficulty;
			bool stillExpanding = Player.Cities.Length < cityTarget && !atWar;

			bool warMinded = Leader.Militarism == MilitarismLevel.Militaristic
			              || Leader.Aggression == AggressionLevel.Aggressive;
			if (stillExpanding && !warMinded) return;

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

				// Expansion penalty: even war-minded leaders are less eager while still settling
				if (stillExpanding) chance -= 10;

				// Trade deterrent: an AI profiting from trade routes with this civ is reluctant
				// to wreck them. Sums the value of routes our cities hold with the enemy (either
				// side's caravan may have built them), capped at -15 so a rich partner is
				// meaningfully safer but a determined warmonger can still strike.
				byte enemyNum = (byte)Game.PlayerNumber(enemy);
				int tradeValue = Player.Cities
				    .SelectMany(c => c.TradeRoutes)
				    .Where(r => r.Partner.Owner == enemyNum)
				    .Sum(r => r.Value);
				if (tradeValue > 0) chance -= Math.Min(15, tradeValue / 2);

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
				score += tile.Food + tile.Shield * 2 + tile.Trade;
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

			// Natural-hazard risk: disasters are more likely on river tiles and mountain-adjacent sites.
			if (center is River) score -= 5;
			if (center.GetBorderTiles().Any(t => t is Mountains)) score -= 3;

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

			// Prefer sites within Chariot-reach (≤5 tiles) of the nearest own city; lone outposts are hard to defend.
			if (Player.Cities.Length > 0)
			{
				int nearestOwn = Player.Cities.Min(c => Common.DistanceToTile(center.X, center.Y, c.X, c.Y));
				if      (nearestOwn <= 5) score += 15;
				else if (nearestOwn <= 7) score += 5;
				else                      score -= 5;
			}

			return score;
		}

		internal ITile? BestSettleSite(IUnit settlers)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
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

		// Nearest tile worth terraforming near our own cities — an un-irrigated farm tile
		// next to fresh water, inside a city's work radius. A built-out empire sends its
		// settlers here to raise city food (irrigation) instead of founding ever-smaller
		// towns; this is the fix for AI cities stalling at ~+0.8 food/turn. Null when there's
		// nothing useful to improve nearby.
		internal ITile? BestImproveSite(IUnit settlers)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			byte ownId = Game.PlayerNumber(Player);
			var claimed = new System.Collections.Generic.HashSet<(int, int)>(
				Game.GetUnits().OfType<Settlers>()
				    .Where(s => s != settlers && s.Owner == ownId && !s.Goto.IsEmpty)
				    .Select(s => (s.Goto.X, s.Goto.Y)));

			ITile? best = null;
			int bestDist = int.MaxValue;
			for (int dy = -6; dy <= 6; dy++)
			for (int dx = -6; dx <= 6; dx++)
			{
				int tx = (settlers.X + dx + mapWidth) % mapWidth;
				int ty = settlers.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean || tile.City is not null) continue;
				if (tile.Irrigation || tile.Mine) continue;
				bool farmable = (tile is Grassland || tile is River || tile is Plains || tile is Desert)
					&& tile.CrossTiles().Any(x => x.Irrigation || x is River || x is Swamp || (x.IsOcean && Map.Instance.IsFreshwaterAt(x.X, x.Y)));
				if (!farmable) continue;
				if (!Player.Cities.Any(c => Common.DistanceToTile(c.X, c.Y, tx, ty) <= 2)) continue;
				if (claimed.Contains((tx, ty))) continue;
				int d = Common.DistanceToTile(settlers.X, settlers.Y, tx, ty);
				if (d < bestDist) { bestDist = d; best = tile; }
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
			// Same-continent filter: we have to walk attackers to the target, and the engine
			// has no naval transport AI yet — picking an off-continent target wedges every
			// attacker on the staging tile (GotoStep returns null for cross-continent paths).
			// "Same continent" = at least one of our cities shares a ContinentId with the target.
			var ownContinents = new HashSet<byte>(Player.Cities
			    .Where(oc => oc.Tile is not null)
			    .Select(oc => oc.Tile.ContinentId)
			    .Where(id => id >= 1 && id <= 14));
			bool reachable(City c) => ownContinents.Count == 0
			    || (c.Tile is not null
			        && c.Tile.ContinentId >= 1 && c.Tile.ContinentId <= 14
			        && ownContinents.Contains(c.Tile.ContinentId));

			var candidates = Game.GetCities()
			    .Where(c => c.Player != Player
			             && (Player.IsAtWar(c.Player) || c.Owner == 0)
			             && Player.Visible(c.X, c.Y)
			             && reachable(c));

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

		private ITile? StagingTile(City target)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			byte own = Game.PlayerNumber(Player);
			ITile? best = null;
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
		private ITile? LandingTile(City target)
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
						ITile? landing = LandingTile(_attackTarget);
						if (landing is not null)
						{
							// Already at the landing zone — unload so troops can storm the beach
							if (Common.DistanceToTile(unit.X, unit.Y, _attackTarget.X, _attackTarget.Y) <= 2)
							{
								(unit as BaseUnitSea)!.Unload();
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
				ITile? dest = BestExploreTile(unit);
				if (dest is not null) unit.Goto = new Point(dest.X, dest.Y);
				return;
			}

			// Diplomats: prefer the human player's cities (steal tech / sabotage), then nearest
			// other foreign city. Only consider cities reachable by land from the diplomat's
			// current tile — same ContinentId means a 4-connected land path exists. Without
			// this filter the diplomat ends up walking forever toward an unreachable target.
			//
			// First-step reachability mirrors the Caravan fix immediately below: skip any
			// candidate whose first step is peaceful-blocked or wedged by pathfinding, so
			// multiple diplomats don't all queue the same unreachable target and burn the
			// AI loop's same-unit circuit breaker turn after turn.
			if (unit is Diplomat)
			{
				byte myContinent = unit.Tile?.ContinentId ?? 15;
				bool sameContinent(City c) => myContinent != 15 && c.Tile is not null && c.Tile.ContinentId == myContinent;

				bool FirstStepReachable(City c)
				{
					ITile? step = Common.GotoStep(unit, c.X, c.Y);
					if (step is null) return false;
					// When the first step IS the target city, the Diplomat is adjacent and the step
					// is its spy mission (steal / incite / sabotage) — not a blocked path. Allow it;
					// AI.Move grants the matching exemption so the unit actually enters.
					if (step.X == c.X && step.Y == c.Y) return true;
					if (step.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					                     && Game.GetPlayer(u.Owner) is Player pu
					                     && !Player.IsAtWar(pu))) return false;
					if (step.City is not null && step.City.Owner != unit.Owner && step.City.Owner != 0
					    && Game.GetPlayer(step.City.Owner) is Player pc
					    && pc.Civilization is not CivOne.Civilizations.Barbarian
					    && !Player.IsAtWar(pc)) return false;
					return true;
				}

				Player human = Human;
				City target =
					Game.GetCities()
					    .Where(c => c.Player == human && Player.Visible(c.X, c.Y) && sameContinent(c))
					    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
					    .FirstOrDefault(FirstStepReachable)
					??
					Game.GetCities()
					    .Where(c => c.Player != Player && Player.Visible(c.X, c.Y) && sameContinent(c))
					    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
					    .FirstOrDefault(FirstStepReachable);
				if (target is not null) unit.Goto = new Point(target.X, target.Y);
				else unit.SkipTurn();
				return;
			}

			// Caravans: head for the nearest worthwhile foreign city (trade route gold), but
			// only among cities on the same continent so we don't dispatch the unit on an
			// impossible walk across the ocean.
			if (unit is Caravan)
			{
				// Caravans deliver trade-route gold by entering a foreign city
				// (Caravan.cs:127, EstablishTradeRoute). Target the nearest reachable foreign
				// city (see the targeting note below for why nearest, not most-distant), and
				// verify the first step is actually reachable — otherwise a Caravan can commit
				// to a target whose path is wedged by a peaceful neighbour, loop in AI.Move
				// until the circuit breaker fires, and waste turn budget.
				//
				// No own-city fallback: walking an AI Caravan into its own city does nothing
				// (CaravanChoice is human-only at Caravan.cs:100-103); the unit would idle
				// on arrival and block its build slot. SkipTurn at home is better.
				byte myContinent = unit.Tile?.ContinentId ?? 15;
				bool sameContinent(City c) => myContinent != 15 && c.Tile is not null && c.Tile.ContinentId == myContinent;

				bool FirstStepReachable(City c)
				{
					ITile? step = Common.GotoStep(unit, c.X, c.Y);
					if (step is null) return false;
					// When the first step IS the target city, the Caravan is adjacent and the step
					// is its trade-route delivery — not a blocked path. Allow it; AI.Move grants the
					// matching exemption so the unit actually enters instead of shuttling on the rails.
					if (step.X == c.X && step.Y == c.Y) return true;
					// Peaceful-block: AI.Move at line ~343 refuses the step if the next tile
					// holds a non-warring player's unit, or is a non-Barbarian city at peace
					// with us. Mirror that here so we don't commit to a target we'd refuse
					// to step toward.
					if (step.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					                     && Game.GetPlayer(u.Owner) is Player pu
					                     && !Player.IsAtWar(pu))) return false;
					if (step.City is not null && step.City.Owner != unit.Owner && step.City.Owner != 0
					    && Game.GetPlayer(step.City.Owner) is Player pc
					    && pc.Civilization is not CivOne.Civilizations.Barbarian
					    && !Player.IsAtWar(pc)) return false;
					return true;
				}

				// Deliver to the NEAREST reachable foreign city, not the most distant. "Most
				// distant" maximised the gold bonus on paper but was an unstable target: each
				// time the caravan crossed the midpoint between two far cities the ranking
				// flipped and it doubled back, so it shuttled along the rails forever and never
				// delivered. Nearest stays nearest as the caravan approaches, so it commits and
				// pays out. Prefer a city of real size (a worthwhile route, per play-test note);
				// fall back to the nearest city of any size so the unit always delivers rather
				// than idling for endless turns at its owner's upkeep.
				const int popFloor = 3;
				var byDistance = Game.GetCities()
				    .Where(c => c.Player != Player && sameContinent(c))
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .ToList();
				City target = byDistance.FirstOrDefault(c => c.Size >= popFloor && FirstStepReachable(c))
				           ?? byDistance.FirstOrDefault(FirstStepReachable);

				if (target is not null) unit.Goto = new Point(target.X, target.Y);
				else unit.SkipTurn();
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
					// Same-continent staleness check: an attacker whose target is on a different
					// continent can't path there. Cached _attackTarget held over from before the
					// fix (or picked when our continents shifted) needs invalidating so the
					// strategy code re-picks a reachable target.
					byte ownPN = Game.PlayerNumber(Player);
					// Tile can be null when a city is mid-capture / in transient sentinel state
					// (X==Y==255). Guard both the cached target and each iterated own-city so a
					// ghost reference doesn't NRE the strategy and stall the turn.
					bool targetOffContinent = _attackTarget is not null
					    && _attackTarget.Tile is not null
					    && !Player.Cities.Any(oc => oc.Tile is not null
					                              && oc.Tile.ContinentId == _attackTarget.Tile.ContinentId
					                              && oc.Tile.ContinentId >= 1 && oc.Tile.ContinentId <= 14);
					bool targetStale = _attackTarget is null
					    || _attackTarget.Tile is null
					    || _attackTarget.Size <= 0
					    || !Game.GetCities().Contains(_attackTarget)
					    || _attackTarget.Player == Player
					    || (_attackTarget.Owner != 0 && !Player.IsAtWar(_attackTarget.Player))
					    || targetOffContinent;
					if (targetStale)
						_attackTarget = PickAttackTarget();

					if (_attackTarget is not null)
					{
						ITile? staging = StagingTile(_attackTarget);
						byte own = Game.PlayerNumber(Player);

						// How many attackers are already at the staging tile?
						int staged = staging?.Units.Count(u =>
						    u.Owner == own && u.Role == UnitRole.LandAttack) ?? 0;

						// Commit when we have enough force; be generous if we outbuilt the defense
						int defenders = _attackTarget!.Tile!.Units.Count(u => u.Role == UnitRole.Defense);
						int threshold = Math.Max(2, defenders + 1);

						Point dest = (staged >= threshold || staging is null)
						    ? new Point(_attackTarget.X, _attackTarget.Y)
						    : new Point(staging!.X, staging!.Y);
						unit.Goto = dest;
						return;
					}
				}

				// Default: reinforce the most under-defended own city. If every own city is
				// already garrisoned (>= 2 defenders) and the attacker still has nothing to do,
				// fall back to the nearest own city anyway — better to pile up there and fortify
				// than to sit in open terrain getting nothing done turn after turn.
				City needsHelp = Player.Cities
				    .Where(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense) < 2)
				    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
				    .ThenBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault()
				    ?? Player.Cities
				       .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				       .FirstOrDefault();
				if (needsHelp is not null && (needsHelp.X != unit.X || needsHelp.Y != unit.Y))
					unit.Goto = new Point(needsHelp.X, needsHelp.Y);
				else if (needsHelp is null)
					unit.Fortify = true;
				// (already at the only fallback city — leave Goto empty, let next turn pick again
				// without thrashing; the unit just sits but won't waste enqueue cycles)
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
					if (a is Masonry)             weight += 4; // gateway to Construction -> Aqueduct (growth)
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
					if (a is AquaticColonization) weight += 6; // new city sites
					if (a is TransitConduit)      weight += 5; // fast movement upgrade
					break;
			}

			// Post-contact advances — useful in all stances once available.
			if (a is Xenobiology)           weight += 6; // gifted free, but may need to be researched
			if (a is Gravitics)             weight += 7; // gateway to sea + tubes
			if (a is SyntheticEcology)      weight += 6; // tile yield improvements
			if (a is MemeticProtocols)      weight += 5; // happiness/diplomacy
			if (a is AquaticColonization)   weight += 5;
			if (a is TransitConduit)        weight += 6;
			if (a is BioplexEngineering)    weight += 5;
			if (a is CanopyCultivation)     weight += 5;
			if (a is NeuralInterface)       weight += 5;
			if (a is GravitonEngineering)   weight += 4;
			if (a is PlanetaryStewardship)  weight += 4;
			if (a is CollectiveMemory)      weight += 4;

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

		private IWonder? SelectWonder(City city, StrategyStance stance)
		{
			if (!IsTopProductionCity(city)) return null;

			// Prioritise dome component(s) assigned to this civilisation, if any
			foreach (var wonderId in Game.Instance.GetDomeAssignments(Player))
			{
				IWonder assigned = Reflect.GetWonders().FirstOrDefault(w => w.Id == (byte)wonderId);
				if (assigned is not null && !Game.WonderBuilt(assigned) && Player.ProductionAvailable(assigned))
					return assigned;
			}

			IWonder[] preferred;
			if (stance == StrategyStance.Militarize)
			{
				preferred = new IWonder[]
				{
					new GreatWall(), new Colossus(), new MichelangelosChapel(),
					new SunTzusWarAcademy(), new LeonardosWorkshop()
				};
			}
			else if (stance == StrategyStance.Consolidate)
			{
				preferred = new IWonder[]
				{
					new ShakespearesTheatre(), new JSBachsCathedral(),
					new HangingGardens(), new MichelangelosChapel(), new Oracle(),
					new AdamSmithsTradingHouse()
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
					new LeonardosWorkshop(), new SunTzusWarAcademy(),
					new AdamSmithsTradingHouse(),
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

			// Universal first: garrison before barracks so a city isn't left naked while building.
			if (defenders < 1)                Consider(BestDefender());

			// Difficulty 0 (Chieftain): front-load a militia screen, an early settler and a
			// Temple ahead of the standard infrastructure chain. (Folded in from a former
			// stand-alone PlanChieftain method that only ever added these three then ran this
			// same plan — Consider() dedupes by type, matching its old plan.All(...) guards.)
			if (Game.Difficulty == 0)
			{
				byte chId       = Game.PlayerNumber(Player);
				int  chCities   = Player.Cities.Length;
				int  chMilitia  = Game.GetUnits().Count(u => u.Owner == chId && u is Militia);
				int  chSettlers = Game.GetUnits().Count(u => u.Owner == chId && u is Settlers);
				if (chMilitia < chCities * 4) Consider(new Militia());
				if (city.Size >= 4 && city.FoodIncome >= 0 && chSettlers < Math.Max(1, chCities / 2)) Consider(new Settlers());
				if (!city.HasBuilding<Temple>()) Consider(new Temple());
			}

			// Garrison a flexible second defender when a hostile unit is actually close — a
			// barbarian raid or a war party within 3 tiles. We deliberately do NOT build City
			// Walls (here or in the standard chain below): per play-testing they are slow to
			// build, drain upkeep, and fold to Catapults, Knights, and bribing Diplomats. A
			// mobile second defender you can re-station or disband on a shield crunch is the
			// better, more flexible insurance. (Barbarians, owner 0, spawn in human-unexplored
			// areas — i.e. on top of AI civs — so this often decides whether an early city survives.)
			byte threatOwnId = Game.PlayerNumber(Player);
			bool hostileNear = Game.GetUnits().Any(u => u.Owner != threatOwnId
				&& (u.Owner == 0 || Player.IsAtWar(Game.GetPlayer(u.Owner)))
				&& Common.DistanceToTile(u.X, u.Y, city.X, city.Y) <= 3);
			if (hostileNear && defenders < 2) Consider(BestDefender());

			// Preventive happiness: a city on the verge of disorder — unhappy citizens no
			// longer outweighed by happy ones — builds a Temple (then Colosseum, then
			// Cathedral) NOW, ahead of growth, military and settlers. Stance-independent and
			// high priority because a rioting city produces nothing: getting ahead of the
			// happiness ceiling breaks the grow→riot→luxury-quell→grow sawtooth that was
			// leaving the AIs relying on the reactive luxury valve instead of infrastructure.
			if (city.UnhappyCitizens > 0 && city.UnhappyCitizens >= city.HappyCitizens)
			{
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>())    Consider(new Temple());
				if (Player.HasAdvance<Construction>()     && !city.HasBuilding<Colosseum>()) Consider(new Colosseum());
				if (Player.HasAdvance<Religion>()         && !city.HasBuilding<Cathedral>()) Consider(new Cathedral());
			}

			// Growth-first: Granary before Barracks/Settlers when Pottery is known.
			// Without this, tiny AI civs build Militia → Barracks → Settlers → ship
			// settler → city drops to size 1 → cycle repeats, and the city never
			// accumulates food past size 2 because Granary stays buried at the bottom
			// of the standard infrastructure chain.
			if (Player.HasAdvance<Pottery>() && !city.HasBuilding<Granary>()) Consider(new Granary());

			// Barracks is deliberately NOT considered here. It only makes future units
			// veteran — no growth, no expansion, no immediate defense — yet it used to sit
			// at slot #4 ahead of Settlers and infrastructure, so tiny AI cities burned
			// their early shields on it (Barracks was the single most-built early item in
			// the decision logs). It is now built only in the Militarize stance.

			int ownCities = Player.Cities.Length;
			// Match the city target used by GetStance (line 70-73) so that the Settler-cap and
			// the Expand→Develop transition agree. Previous hard caps of 13/10/7 caused Epic-map
			// civs to stop founding cities long before hitting the stance target, leaving them
			// stuck in Expand stance forever (no research weight shift to Trade/Currency/Banking
			// → never reaches Republic → permanent Despotism tile penalty → cities stay tiny).
			// Linear map scale — see GetStance (line 71) for why area-based was wrong.
			int mapScale = Math.Max(1, Map.WIDTH / 80);
			int maxCities = Leader.Development == Expansionistic ? (9 * mapScale) + Game.Difficulty
			              : Leader.Development == Normal          ? (6 * mapScale) + Game.Difficulty
			              :                                         (4 * mapScale) + Game.Difficulty;

			// Tiny-empire settlers: < 3 cities → skip Explorer, build settlers
			// once the city has actual mass to spend. Requiring size >= 3 (and Granary
			// where Pottery is researched) breaks the "size-1 cycle" where AI civs
			// repeatedly ship a settler and revert to size 1, never accumulating food.
			// Never build settlers from a starving city — that accelerates population loss.
			if (ownCities < 3 && stance != StrategyStance.Consolidate)
			{
				bool granaryReady = !Player.HasAdvance<Pottery>() || city.HasBuilding<Granary>();
				if (city.Size >= 3 && granaryReady && city.FoodIncome >= 0 && !city.Units.Any(x => x is Settlers) && ownCities < maxCities)
					Consider(new Settlers());
			}

			// Explorer: one per 3 cities while the map still has meaningful fog. Stop
			// queueing once the player has revealed > 70% of the world's land — late-game
			// Explorer builds just churn shields with nothing useful to scout. Analytics
			// (2026-06-06) showed 8.8% of late-game builds were Explorers, with civs of
			// 30+ cities still pumping them out.
			if (Player.ExploredLandFraction < 0.70)
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

			// Militarize: garrison up to 2, barracks for veterans, then attackers
			if (stance == StrategyStance.Militarize)
			{
				if (defenders < 2) Consider(BestDefender());
				if (!city.HasBuilding<Barracks>()) Consider(new Barracks());
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
				if (city.Size >= minSize && city.FoodIncome >= 0 && !city.Units.Any(x => x is Settlers) && ownCities < maxCities)
					Consider(new Settlers());
			}

			// Worker settlers: a built-out empire (Develop/Consolidate) keeps a few settlers
			// terraforming — irrigating the tiles its cities work — so cities reach the food
			// surplus to grow past size 3 instead of stalling. Capped at ~1 per 4 cities so it
			// doesn't crowd out economy, and only from healthy cities with mass to spend.
			// AI.Move routes these to BestImproveSite() rather than founding new towns.
			if ((stance == StrategyStance.Develop || stance == StrategyStance.Consolidate)
			    && city.Size >= 4 && city.FoodIncome >= 0 && !city.Units.Any(x => x is Settlers))
			{
				byte wsId = Game.PlayerNumber(Player);
				int workers = Game.GetUnits().Count(u => u.Owner == wsId && u is Settlers);
				if (workers < Math.Max(1, ownCities / 4))
					Consider(new Settlers());
			}

			// Standard infrastructure chain (all stances)
			if (Player.HasAdvance<Pottery>()           && !city.HasBuilding<Granary>())      Consider(new Granary());
			// Aqueduct: unlocks growth past size 6 (City.cs:1187). Build when the
			// city is approaching the cap (size 5+) so shields aren't wasted in
			// tiny cities; without this the AI's entire empire stalls at size 6.
			if (Player.HasAdvance<Construction>()      && city.Size >= 5  && !city.HasBuilding<Aqueduct>())   Consider(new Aqueduct());
			if (Player.HasAdvance<CeremonialBurial>()  && !city.HasBuilding<Temple>())        Consider(new Temple());
			if (Player.HasAdvance<Writing>()           && !city.HasBuilding<Library>())       Consider(new Library());
			if (Player.HasAdvance<Currency>()          && !city.HasBuilding<MarketPlace>())   Consider(new MarketPlace());
			if (Player.HasAdvance<Rocketry>()          && !city.HasBuilding<SamBattery>())    Consider(new SamBattery());
			if (Player.HasAdvance<Construction>()      && !city.HasBuilding<Colosseum>())     Consider(new Colosseum());
			if (Player.HasAdvance<Religion>()          && !city.HasBuilding<Cathedral>())     Consider(new Cathedral());
			if (Player.HasAdvance<Computers>()         && !city.HasBuilding<Observatory>())   Consider(new Observatory());
			// Sewer System: unlocks growth past size 12 (City.cs:1188). Same
			// pattern — only consider once the city is closing on the cap.
			if (Player.HasAdvance<Engineering>()       && city.Size >= 10 && !city.HasBuilding<SewerSystem>()) Consider(new SewerSystem());

			// Post-contact buildings
			if (Player.HasAdvance<Xenobiology>()        && !city.HasBuilding<Xenolab>())        Consider(new Xenolab());
			if (Player.HasAdvance<MemeticProtocols>()   && !city.HasBuilding<ExchangeCenter>()) Consider(new ExchangeCenter());
			if (Player.HasAdvance<NeuralInterface>()    && !city.HasBuilding<NeuralLab>())      Consider(new NeuralLab());
			if (Player.HasAdvance<AquaticColonization>() && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean) && !city.HasBuilding<SeaPlatform>()) Consider(new SeaPlatform());

			// Hydro Engineer: build one per ~4 coastal cities so the AI can colonize ocean tiles.
			// Skips if the city is starving or has no population to spare.
			if (Player.HasAdvance<AquaticColonization>() && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean))
			{
				byte ownIdH = Game.PlayerNumber(Player);
				int ownHydro = Game.GetUnits().Count(u => u.Owner == ownIdH && u is HydroEngineer);
				int coastalCities = Player.Cities.Count(c => Map[c.X, c.Y].GetBorderTiles().Any(t => t.IsOcean));
				int hydroCap = Math.Max(1, coastalCities / 4);
				if (ownHydro < hydroCap && city.Size >= 2 && city.FoodIncome >= 0 && !city.Units.Any(u => u is HydroEngineer))
					Consider(new HydroEngineer());
			}

			// Wonder: only for the empire's top production city
			IWonder? wonder = SelectWonder(city, stance);
			if (wonder is not null) Consider(wonder);

			// Second defender once infrastructure is underway
			if (defenders < 2) Consider(BestDefender());

			// Soft units by government / stance
			if (stance == StrategyStance.Militarize && !Player.RepublicDemocratic)
			{
				Consider(BestAttacker());
			}

			// Diplomats: useful under every stance (espionage, sabotage, incite revolt).
			// Previously gated to non-Militarize, which is why no civ ever built one in heavy
			// fighting eras. One per 2 cities, minimum 3 empire-wide — espionage (especially
			// tech theft, now repeatable via the TechStolen cooldown) is high-value, and
			// diplomats are consumed on use, so a larger steady-state pool keeps spies in play.
			if (Player.HasAdvance<Writing>())
			{
				byte ownId2 = Game.PlayerNumber(Player);
				int ownDiplomats = Game.GetUnits().Count(u => u.Owner == ownId2 && u is Diplomat);
				int diplomatCap  = Math.Max(3, Player.Cities.Length / 2);
				if (ownDiplomats < diplomatCap)
					Consider(new Diplomat());
			}

			// Caravans: trade-route gold once Trade is researched. Capped empire-wide so
			// the planner doesn't queue Caravan after Caravan once Trade lands. Caravans are
			// one-shot (consumed on delivery at Caravan.cs:77), so the cap counts in-flight
			// units, not lifetime production. /6 keeps the queue flowing for a typical empire
			// without crowding out science/military builds — see the Diplomat cap above for
			// the same shape applied to a persistent unit.
			if (Player.HasAdvance<Trade>())
			{
				byte ownId3 = Game.PlayerNumber(Player);
				int ownCaravans = Game.GetUnits().Count(u => u.Owner == ownId3 && u is Caravan);
				int caravanCap  = Math.Max(2, Player.Cities.Length / 6);
				if (ownCaravans < caravanCap)
					Consider(new Caravan());
			}

			// Fallback: nothing useful left to build, so pick a random available item — but
			// NEVER the Palace when the civ already has a capital. Building a Palace just
			// relocates the capital (City.cs:1412), so a random pick here had built-out AI civs
			// forever shuffling their seat of government. A capital-less civ (lost its capital in
			// war) is still allowed one, so it can re-establish a corruption-free centre.
			if (plan.Count == 0)
			{
				bool hasCapital = Player.Cities.Any(c => c.HasBuilding<Palace>());
				IProduction[] items = city.AvailableProduction
				    .Where(p => !hasCapital || !(p is Palace))
				    .ToArray();
				if (items.Length == 0) items = city.AvailableProduction.ToArray();
				Consider(items[Common.Random.Next(items.Length)]);
			}

			return plan;
		}

		// ── exploration helpers ───────────────────────────────────────────────

		internal ITile? BestExploreTile(IUnit unit)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
			int bestScore = 0; // only move if it adds value
			var ownCities = Player.Cities;

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

				// Hut bias: BaseUnitLand.TribalHut (case 0/3) rolls Barbarians when
				// NearestCity >= 4 AND the player has cities — ~25% of outcomes there
				// spawn hostile units, which is a real risk to a lone Explorer. So
				// weight close-to-home huts highly and decay the bonus past distance 3.
				// Direct-hit (tile.Hut) > adjacent (will step onto it next turn).
				int hutBonus = tile.Hut ? 12
				             : tile.GetBorderTiles().Any(bt => bt is not null && bt.Hut) ? 8
				             : 0;
				if (hutBonus > 0 && ownCities.Length > 0)
				{
					int homeDist = ownCities.Min(c => Common.DistanceToTile(c.X, c.Y, tx, ty));
					hutBonus = Math.Max(0, hutBonus - Math.Max(0, homeDist - 3));
				}

				int score = CountUnseenTiles(tx, ty) - dist + hutBonus;
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

		// Ocean-tile target finder for Hydro Engineer: prefers open ocean far from any city
		// (a candidate floating-city site) over tiles already inside a city's working radius.
		internal ITile? BestFloatingSite(IUnit unit)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
			int bestScore = 0;
			City[] cities = Game.GetCities();

			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (unit.X + dx + mapWidth) % mapWidth;
				int ty = unit.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || !tile.IsOcean || tile.City is not null) continue;
				if (tile.Units.Any()) continue;
				int dist = Common.DistanceToTile(unit.X, unit.Y, tx, ty);
				int nearestCity = cities.Any() ? cities.Min(c => Common.DistanceToTile(c.X, c.Y, tx, ty)) : 255;
				int score = nearestCity - dist;
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
		    // Under Despotism the despot penalty cuts any tile yielding >2, so irrigation adds little.
		    // Build roads first to connect the empire; switch to irrigation once Monarchy removes the penalty.
		    bool preMonarchy = Player.Government is Gov.Despotism || Player.Government is Gov.Anarchy;

		    // Expansion phase: roads first; skip irrigation under Despotism
		    if (stance == StrategyStance.Expand)
		        return validRoad ? SettlerImprovement.Road :
		               (!preMonarchy && validIrrigation) ? SettlerImprovement.Irrigation :
		               validMine ? SettlerImprovement.Mine : SettlerImprovement.None;

		    // Consolidation: irrigation → growth (roads first under Despotism)
		    if (stance == StrategyStance.Consolidate)
		        return (!preMonarchy && validIrrigation) ? SettlerImprovement.Irrigation :
		               validRoad ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine :
		               validIrrigation ? SettlerImprovement.Irrigation : SettlerImprovement.None;

		    // Militarization: roads first for rapid troop movement
		    if (stance == StrategyStance.Militarize)
		        return validRoad ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine :
		               validIrrigation ? SettlerImprovement.Irrigation : SettlerImprovement.None;

		    // Default (Develop): roads first under Despotism; irrigation once Monarchy unlocks it
		    if (preMonarchy)
		        return validRoad ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine : SettlerImprovement.None;

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
		internal ITile? BestOlvirImproveSite(IUnit settler)
		{
			byte ownId = Game.PlayerNumber(Player);
			City[] ownCities = Game.GetCities().Where(c => c.Owner == ownId).ToArray();
			if (ownCities.Length == 0) return null;

			ITile? best = null;
			int bestDist = int.MaxValue;

			foreach (City city in ownCities)
			{
				for (int dy = -2; dy <= 2; dy++)
				for (int dx = -2; dx <= 2; dx++)
				{
					if ((dx == -2 || dx == 2) && (dy == -2 || dy == 2)) continue; // skip corners (match CityRadius)
					int tx = (city.X + dx + Map.WIDTH) % Map.WIDTH;
					int ty = city.Y + dy;
					if (ty < 0 || ty >= Map.HEIGHT) continue;
					ITile tile = Map[tx, ty];
					if (tile is null || tile.IsOcean || tile.City is not null) continue;
					if (Game.Instance.OlvirImprovements.ContainsKey((tx, ty))) continue;
					int dist = Common.DistanceToTile(city.X, city.Y, tx, ty);
					if (dist < bestDist) { bestDist = dist; best = tile; }
				}
			}
			return best;
		}
	}
}