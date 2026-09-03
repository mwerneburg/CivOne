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
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;
using CivOne.IO;
using CivOne.Screens;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.UserInterface;
using CivOne.Wonders;

namespace CivOne.Units
{
	internal abstract class BaseUnit : BaseInstance, IUnit
	{
		protected int _x, _y;

		public virtual bool Busy
		{
			get
			{
				return (Sentry || Fortify);
			}
			set
			{
				Sentry = false;
				Fortify = false;
				FortifyActive = false;
			}
		}
		public bool Veteran { get; set; }
		public bool FortifyActive { get; private set; }
		private bool _fortify = false;
		public bool Fortify
		{
			get
			{
				return (_fortify || FortifyActive);
			}
			set
			{
				if (Class != UnitClass.Land) return;
				if (this is Settlers) return;
				if (!value)
					_fortify = false;
				else if (Fortify)
					return;
				else
					FortifyActive = true;
			}
		}

		private bool _sentry;
		public bool Sentry
		{
			get
			{
				return _sentry;
		 	}
			set
			{
				if (_sentry == value) return;
				if (!(_sentry = value) || !Game.Started) return;
				MovesLeft = 0;
				PartMoves = 0;
				MovementDone(Map[X, Y]);
			}
		}

		public bool Moving => (Movement is not null);
		public MoveUnit? Movement { get; protected set; }

		// Whether this unit's move should be animated (slid over 16 ticks) rather than
		// completed instantly. Mirror of the GameMap draw gate: only spend animation time
		// on a sprite that will actually be drawn. Skipping undrawn AI moves is the single
		// biggest cut to the late-game between-turns pause.
		private bool MoveIsVisible
		{
			get
			{
				if (Human is null) return false;
				if (Human == Owner) return true;
				if (!Game.EnemyMoves) return false;
				if (!Settings.RevealWorld && !Human.Visible(X, Y)) return false;
				return true;
			}
		}

		private int AttackStrength(IUnit defendUnit)
		{
			// Step 1: Determine the nominal attack value of the attacking unit and multiply it by 8.
			int attackStrength = ((int)Attack * 8);

			if (Owner == 0)
			{
				// Step 2: If the attacking unit is a Barbarian unit and the defending unit is player-controlled, multiply the attack strength by the Difficulty Modifier, then divide it by 4.
				if (Human == defendUnit.Owner)
				{
					attackStrength *= (Game.Difficulty + 1);
					attackStrength /= 4;
				}

				// Step 3: If the attacking unit is a Barbarian unit and the defensing unit is AI-controlled, divide the attack strength by 2.
				if (Human != defendUnit.Owner)
				{
					attackStrength /= 2;
				}

				// Step 4: If the attacking unit is a Barbarian unit and the defending unit is inside a city and the defending civilization does not control any other cities, set the attack strength to zero.
				// This actually makes the defending unit invincible in this special case. Might well save you from being obliterated by that unlucky hut at 3600BC.
				if (defendUnit.Tile.City is not null && Game.GetPlayer(defendUnit.Owner).Cities.Length == 1)
				{
					attackStrength = 0;
				}

				// Step 5: If the attacking unit is a Barbarian unit and the defending unit is inside a city with a Palace, divide the attack strength by 2.
				if (defendUnit.Tile.City is not null && defendUnit.Tile.City.HasBuilding<Palace>())
				{
					attackStrength /= 2;
				}
			}

			// Step 6: If the attacking unit is a veteran unit, increase the attack strength by 50%.
			if (Veteran)
			{
				attackStrength += (attackStrength / 2);
			}
			
			// Step 7: If the attacking unit has only 0.2 movement points left, multiply the attack strength by 2, then divide it by 3. If the attacking unit has only 0.1 movement points left, then just divide by 3 instead.
			if (MovesLeft == 0)
			{
				attackStrength *= PartMoves;
				attackStrength /= 3;
			}

			// Step 8: If the attacking unit is a Barbarian unit and the defending unit is player-controlled, check the difficulty level. On Chieftain and Warlord levels, divide the attack strength by 2.
			if (Owner == 0 && Human == defendUnit.Owner)
			{
				if (Game.Difficulty < 2)
				{
					attackStrength /= 2;
				}
			}

			// Step 9: If the attacking unit is player-controlled, check the difficulty level. On Chieftain level, multiply the attack strength by 2.
			// So on Chieftain difficulty, it is often better to attack than be attacked, even with a defensive unit.
			if (Human == Owner && Game.Difficulty == 0)
			{
				attackStrength *= 2;
			}

			return attackStrength;
		}

		private int DefendStrength(IUnit defendUnit, IUnit attackUnit)
		{
			// Air attack on a non-air defender strips the defender's bonuses. A SAM Battery
			// blunts that — it does not cancel it.
			//
			// The rule used to be all-or-nothing: no SAM and the defender lost BOTH its
			// terrain and its fortification multipliers; a SAM and it kept both, as though
			// the aircraft were infantry. Graded instead: under a SAM the defender keeps its
			// FORTIFICATION, which is the part missiles are actually bad at reaching, and
			// still loses the terrain bonus, because a mountainside does not hide a city from
			// the air. A fortified defender in a SAM city therefore stands at roughly three
			// times a defender caught in the open, without making air attack pointless.
			if (attackUnit.Class == UnitClass.Air && defendUnit.Class != UnitClass.Air)
			{
				bool hasSam = defendUnit.Tile.City?.HasBuilding<Buildings.SamBattery>() == true;
				int baseDefend = (int)defendUnit.Defense * 2;
				if (hasSam)
				{
					int fortification = defendUnit.Tile.Fortress ? 8
					                  : (defendUnit.Fortify || defendUnit.FortifyActive) ? 6
					                  : 4;
					// /4 because the fortification modifier carries a factor of 4 already
					// (see step 3 below); this keeps an unfortified defender at baseDefend.
					baseDefend = baseDefend * fortification / 4;
				}
				if (defendUnit.Veteran) baseDefend += baseDefend / 2;
				return baseDefend;
			}

			// Check City Walls for step 5 (Great Wall acts as City Walls for all owner cities until Gunpowder)
			bool cityWalls = defendUnit.Tile.City is not null
				&& (defendUnit.Tile.City.HasBuilding<CityWalls>()
				    || (!Game.WonderObsolete<Wonders.GreatWall>()
				        && Game.GetPlayer(defendUnit.Tile.City.Owner)?.HasWonder<Wonders.GreatWall>() == true));

			// Step 1: Determine the nominal defense value of defending unit.
			int defendStrength = (int)defendUnit.Defense;

			if (defendUnit.Class == UnitClass.Land || (defendUnit.Class == UnitClass.Water && cityWalls && attackUnit.Attack != 12))
			{
				int fortificationModifier = 4;
				if (defendUnit.Tile.Fortress)
					fortificationModifier = 8;
				else if (defendUnit.Fortify || defendUnit.FortifyActive)
					fortificationModifier = 6;

				// Step 2: If the defending unit is a ground unit, multiply the defense strength by the Terrain Modifier.
				// This modifier effectively includes a factor of 2.
				defendStrength *= defendUnit.Tile.Defense;
				
				if (!cityWalls || attackUnit.IgnoresCityWalls)
				{
					// Step 3: If the defending unit is a ground unit, multiply the defense strength by the Fortification Modifier.
					// This modifier effectively includes a factor of 4, resulting in a combined factor of 8.
					defendStrength *= fortificationModifier;
				}
			}

			// Step 4: If the defending unit is a sea or air unit, multiply the defense strength by 8.
			// This effectively treats the Terrain Modifier as 2, regardless of the actual terrain type. It also means that these units will never benefit from the Fortification Modifier.
			if (defendUnit.Class != UnitClass.Land && (!cityWalls || attackUnit.IgnoresCityWalls))
			{
				defendStrength *= 8;
			}

			// Step 5: If the defending unit is inside a city with City Walls and the nominal attack value of the attacking unit is NOT equal to 12, check the domain of the defending unit. If the domain is NOT air, re-calculate steps 1 and 2 (ignore steps 3 and 4) and multiply the result by 12.
			// When determining if the attacking unit ignores City Walls, the game just checks for attack value, not unit type. So if you change any unit's attack rating to 12, the game will have it ignore City Walls as well.
			if (cityWalls && attackUnit.Attack != 12)
			{
				defendStrength *= 12;
			}

			// Step 6: If the defending unit is a veteran unit, increase the defense strength by 50%.
			if (defendUnit.Veteran)
			{
				defendStrength += (defendStrength / 2);
			}

			return defendStrength;
		}

		private bool AttackOutcome(IUnit attackUnit, ITile defendTile)
		{
			IUnit defendUnit = defendTile.Units.OrderByDescending(x => x.Attack * (x.Veteran ? 1.5 : 1)).ThenBy(x => (int)x.Type).First();

			int attackStrength = AttackStrength(defendUnit);
			int defenseStrength = DefendStrength(defendUnit, attackUnit);
			int randomAttack = Common.Random.Next(attackStrength);
			int randomDefense = Common.Random.Next(defenseStrength);
			bool win = (randomAttack > randomDefense);
			if (win && attackUnit.Owner == 0 && defendUnit.Tile.City is not null)
			{
				 // If the attacking unit is a Barbarian unit and the defending unit is inside a city, then, if the attacking unit won, the procedure will be repeated once
				 // This time, the attacking unit wins on a tie.
				randomAttack = Common.Random.Next(attackStrength);
				randomDefense = Common.Random.Next(defenseStrength);
				win = (randomAttack >= randomDefense);
			}

			// 50% chance to award veteran status to the winner
			if (Common.Random.Next(100) < 50)
			{
				if (win && !attackUnit.Veteran) attackUnit.Veteran = true;
				if (!win && !defendUnit.Veteran) defendUnit.Veteran = true;
			}
			
			return win;
		}

		protected virtual bool Confront(int relX, int relY)
		{
			// Non-combat land units refuse to walk into enemies. Diplomat and Caravan have
			// their own Confront overrides for their special interactions with foreign cities;
			// they end up here only if those overrides don't catch the case. Sending an unarmed
			// unit into combat is suicide and the AI pathfinder doesn't avoid enemy-occupied
			// tiles, so guard at the boundary.
			//
			// Gated on ATTACK, not on a list of type names. The list read Diplomat, Caravan,
			// Settlers and HydroEngineer — and left out the EXPLORER, which has attack 0 like
			// every one of them. An explorer could therefore walk into an undefended city and
			// take it; a size-1 city taken is destroyed outright, so an unarmed scout razed a
			// city the turn after it was founded. Reported from a game, 20 Aug 2026.
			//
			// Every unarmed land unit belongs here, including the next one somebody adds — that
			// is the whole reason this is a property and not a roster.
			if (Class == UnitClass.Land && Attack == 0)
			{
				return false;
			}

			Movement = new MoveUnit(relX, relY, MoveIsVisible);

			ITile moveTarget = Map[X, Y][relX, relY];
			if (moveTarget is null) return false;

			{
				string targetDesc = moveTarget.City is not null
					? $"city {moveTarget.City.Name}(P{moveTarget.City.Owner})"
					: $"unit {(moveTarget.Units.FirstOrDefault()?.GetType().Name ?? "?")}(P{moveTarget.Units.FirstOrDefault()?.Owner})";
				Log($"[Confront] {GetType().Name} P{Owner} ({X},{Y}) → ({X+relX},{Y+relY}) {targetDesc}");
			}

			// Any hostile act against another civ triggers a state of war.
			// Democracy: the Senate blocks sneak attacks — the unit cannot initiate
			// a new war, so only proceed if already at war with the target.
			// ...but not when the AI is steering this civ. The Senate veto is a HUMAN
			// handicap: it exists so a democratic player must answer to their legislature.
			// Under Autopilot the acting player IS the human slot, so without this clause the
			// autopiloted civ is the only democracy in the world that cannot start a war —
			// every AI civ attacks freely while it stands down. Same rule the difficulty
			// handicap and the diplomat targeting already follow (City.cs "aiRun",
			// AI.Strategy "HumanOpponent").
			//
			// This was briefly reverted while bisecting a late-game stall, on the theory that
			// letting the steered civ into combat it was previously forbidden might be the
			// cause. It was not. The stall was the notification queue: per-city celebration
			// and disorder screens piling up faster than autopilot could dismiss them. Once
			// those were gated (City.cs, DisorderNotifications) the same save's worst turn
			// past 714 fell from 23.7s to 5.9s and `other` from 11.2s to 1.0s, on the only
			// save that had ever reproduced it. Restored.
			if (Human == Owner && !Settings.Instance.Autopilot
			    && Player.Government is Governments.Democracy)
			{
				Player? targetOwner = moveTarget.City is not null
				    ? Game.GetPlayer(moveTarget.City.Owner)
				    : moveTarget.Units.Any(u => u.Owner != Owner)
				        ? Game.GetPlayer(moveTarget.Units.First(u => u.Owner != Owner).Owner)
				        : null;
				// ...unless that civ has already crossed the provocation threshold. The Senate
				// refuses to START a war for you; it does not go on shielding a civ whose
				// diplomats have been dismantling your cities. See Game.RecordProvocation.
				if (targetOwner is not null && targetOwner != Player && !Player.IsAtWar(targetOwner)
				    && targetOwner.Civilization is not Barbarian
				    && !Game.IsProvocateur(Game.PlayerNumber(targetOwner)))
				{
					GameTask.Enqueue(Message.Error("-- Civilization Note --", "The Senate has", "blocked your attack!"));
					Movement = null;
					return false;
				}
			}

			if (moveTarget.City is not null && moveTarget.City.Owner != Owner)
				Player.DeclareWar(Game.GetPlayer(moveTarget.City.Owner));
			else if (moveTarget.Units.Any(u => u.Owner != Owner))
				Player.DeclareWar(Game.GetPlayer(moveTarget.Units.First(u => u.Owner != Owner).Owner));

			// `this is not Nuclear` first, because this chain is checked IN ORDER and the
			// first match returns. A missile aimed at an UNDEFENDED city matched here — as a
			// would-be occupier — was asked "are you a land unit?", and was refused with
			// "Only land units can capture a city" before anything checked what it actually
			// was. The Nuclear branch below never got a look in, so a nuke could destroy a
			// garrison but not an empty city, which is exactly backwards.
			if (this is not Nuclear
			    && !moveTarget.Units.Any(u => u.Owner != Owner) && moveTarget.City is not null && moveTarget.City.Owner != Owner)
			{
				// An empty enemy city. Only land units can walk in and take it; air and sea
				// units are refused. The refusal was reported as ERROR/OCCUPY ("that tile is
				// already occupied"), which is the opposite of what happened — this branch
				// is reached only when the tile holds no enemy unit at all.
				if (Class != UnitClass.Land)
				{
					// Owner only: an AI frigate bumping an empty enemy city is not the
					// player's business, and there are a lot of frigates.
					if (Human == Owner)
						GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText($"ERROR/NOCAPTURE")));
					Movement = null;
					return false;
				}

				City capturedCity = moveTarget.City;
				Movement!.Done += (s, a) =>
				{
					Action changeOwner = delegate()
					{
						Player previousOwner = Game.GetPlayer(capturedCity.Owner);
						Log($"[changeOwner] {GetType().Name} P{Owner} captures {capturedCity.Name}(P{capturedCity.Owner}) size={capturedCity.Size} walls={capturedCity.HasBuilding<CityWalls>()}");

						if (capturedCity.HasBuilding<Palace>())
							capturedCity.RemoveBuilding<Palace>();
						capturedCity.Food = 0;
						capturedCity.Shields = 0;
						while (capturedCity.Units.Length > 0)
							Game.DisbandUnit(capturedCity.Units[0]);
						// Ghost-garrison sweep: pre-capture check at line 298 fires synchronously
						// at move-initiation, but ownership flips async via Movement.Done. Any
						// non-capturer unit still standing on the captured tile when changeOwner
						// runs is invariant-violating — combat zombie, async-window arrival, or a
						// mid-capture reload survivor. Disband to keep tile occupancy clean.
						// (Filter by Owner; the capturing unit itself has Owner == Owner.)
						foreach (IUnit ghost in moveTarget.Units.Where(u => u.Owner != Owner).ToArray())
						{
							Log($"[changeOwner] sweep ghost {ghost.GetType().Name} P{ghost.Owner} from captured tile ({moveTarget.X},{moveTarget.Y})");
							Game.DisbandUnit(ghost);
						}
						capturedCity.Owner = Owner;
						capturedCity.TechStolen = false;

						// A story faction does not finish the previous administration's
						// paperwork. Ordinary capture zeroes Shields but leaves the QUEUE
						// standing, so an occupied city carries on building whatever it held —
						// which is how the Registry came to be completing a Dome component,
						// humanity's defence against the occupation, in a 1900 AD game.
						// ExecuteOwnersLanding already did this for the landing itself; conquest
						// is the path that was missed.
						if (Game.GetPlayer(Owner).Civilization is Civilizations.TheOthers
						                                       or Civilizations.TheThing
						                                       or Civilizations.Skynet)
						{
							capturedCity.ClearProductionQueue();
							capturedCity.SetProduction(new MechInf());
						}
						previousOwner.InvalidateCityCaches();
						Game.GetPlayer(Owner).InvalidateCityCaches();
						{
							City[] cities = Game.Instance.GetCities();
							int cIdx = System.Array.IndexOf(cities, capturedCity);
							Game.Instance.AddReplayEvent(new ReplayData.CityCaptured(Game.GameTurn, cIdx, capturedCity.NameId, capturedCity.X, capturedCity.Y, Owner));
						}

						if (!capturedCity.HasBuilding<CityWalls>()
						    && !(previousOwner.HasWonder<Wonders.GreatWall>() && !Game.WonderObsolete<Wonders.GreatWall>()))
						{
							capturedCity.Size--;
						}

						previousOwner.IsDestroyed();
						Log($"[changeOwner] done; {capturedCity.Name} now P{capturedCity.Owner} size={capturedCity.Size}");
					};

					IList<IAdvance> advancesToSteal = GetAdvancesToSteal(capturedCity.Player);

					if (Human == capturedCity.Owner || Human == Owner)
					{
						Show captureScreen;
						if (Human == Owner)
						{
							bool isLiberation = capturedCity.OriginalOwner == Owner;
							string artKey = isLiberation ? "cityliberated" : "cityconquered";
							string caption = isLiberation ? $"{capturedCity.Name} liberated!" : $"{capturedCity.Name} conquered!";
							captureScreen = Show.EventArt(artKey, caption);
						}
						else
						{
							captureScreen = Show.CaptureCity(capturedCity);
						}
						captureScreen.Done += (s1, a1) =>
						{
							changeOwner();

							if (capturedCity.Size == 0 || Human != Owner) return;
							GameTask.Insert(Show.CityManager(capturedCity));
						};
						GameTask.Insert(captureScreen);

						if (Human == Owner && advancesToSteal.Any())
							GameTask.Enqueue(Tasks.Show.SelectAdvanceAfterCityCapture(Player, advancesToSteal));
					}
					else
					{
						changeOwner();
						if (advancesToSteal.Any())
							Player.AddAdvance(advancesToSteal.First());
					}
					MoveEnd(s, a);
				};
			}
			else if (this is Nuclear)
			{
				// Real (space-based) SDI: a defender holding the Fusion Core wonder intercepts
				// the incoming strike anywhere over its cities/forces — the missile is shot down
				// rather than detonating. (The legacy per-city SDI Defense building was never
				// wired up; this is the genuine empire-wide interceptor.)
				Player? nukeTarget = moveTarget.City is not null
					? Game.GetPlayer(moveTarget.City.Owner)
					: (moveTarget.Units.FirstOrDefault(u => u.Owner != Owner) is IUnit du ? Game.GetPlayer(du.Owner) : null);
				if (nukeTarget is not null && nukeTarget.HasWonder<Wonders.FusionCore>())
				{
					if (Human == Owner || Human == nukeTarget)
						GameTask.Enqueue(Message.General("Nuclear strike intercepted", $"by the {nukeTarget.TribeName} Fusion Core!"));
					Game.DisbandUnit(this);
					return true;
				}

				Show nukeShow = Show.EventArt("nuclearbombdetonation", "Nuclear bomb detonated!");
				// Captured, not read from X/Y inside the handler: the missile does not move
				// in this branch today, but a closure that depends on that is a trap.
				int blastX = X + relX, blastY = Y + relY;
				Player detonator = Game.GetPlayer(Owner);
				nukeShow.Done += (s, a) => Game.ApplyNuclearStrike(blastX, blastY, detonator);
				GameTask.Enqueue(nukeShow);
			}
			else if (AttackOutcome(this, Map[X, Y][relX, relY]))
			{
				Movement!.Done += (s, a) =>
				{
					IUnit unit = Map[X, Y][relX, relY].Units.FirstOrDefault();
					// The attacker is credited: a Harvester killed here pays a bounty
					// (Screens.DestroyUnit). Every other loss path passes no credit.
					// ...unless we can use it. Salvage takes the unit intact instead of
					// destroying it; the loser is out one unit either way.
					// Beating a visitor craft starts the fuel clock, whether it is taken
					// intact or blown apart. Their drive is the only place the exotic fuel
					// exists, and prising it out of a wreck is the whole point.
					if (unit is not null) NoteVisitorWreck(unit);
					if (unit is not null && Salvage(unit)) { }
					else if (unit is not null && !Screens.DestroyUnit.ResolveIfUnseen(unit, true, Player))
					{
						GameTask.Insert(Show.DestroyUnit(unit, true, Player));
					}
					
					if (MovesLeft == 0)
					{
						PartMoves = 0;
					}
					else if (MovesLeft > 0)
					{
						if (this is Bomber)
						{
							SkipTurn();
						}
						else
						{
							MovesLeft--;
						}
					}
					Movement = null;

					// An expendable weapon is consumed by its own strike. This is the WIN path —
					// the loss path below already destroys the attacker — and it sits OUTSIDE the
					// city test, because a missile fired at a stack in the open is as spent as one
					// fired at a city. Without this, 40 shields buys a bomber that never needs fuel.
					if (CruiseMissile.IsExpendable(this)
					    && !Screens.DestroyUnit.ResolveIfUnseen(this, false))
						GameTask.Insert(Show.DestroyUnit(this, false));

					if (Map[X, Y][relX, relY].City is not null)
					{
						City cc = Map[X, Y][relX, relY].City;
						bool wallProtected = cc.HasBuilding<CityWalls>()
							|| (!Game.WonderObsolete<Wonders.GreatWall>()
							    && Game.GetPlayer(cc.Owner)?.HasWonder<Wonders.GreatWall>() == true);
						if (!wallProtected)
							cc.Size--;
					}
				};
			}
			else
			{
				Movement!.Done += (s, a) =>
				{
					if (!Screens.DestroyUnit.ResolveIfUnseen(this, false))
						GameTask.Insert(Show.DestroyUnit(this, false));
					Movement = null;
				};
			}
			GameTask.Insert(Movement!);
			return false;
		}
		
		private IList<IAdvance> GetAdvancesToSteal(Player victim)
		{
			return victim.Advances
			.Where(p => !Player.Advances.Any(p2 => p2.Id == p.Id))
			.OrderBy(a => Common.Random.Next(0, 1000))
			.Take(3)
			.ToList();
		}

		public virtual bool MoveTo(int relX, int relY)
		{
			if (Movement is not null)
			{
				Log($"[MoveTo] Blocked: {GetType().Name} P{Owner} ({X},{Y}) has active Movement");
				return false;
			}
			
			ITile moveTarget = Map[X, Y][relX, relY];
			if (moveTarget is null) return false;
			if (moveTarget.Units.Any(u => u.Owner != Owner))
			{
				if (Class == UnitClass.Land && Tile.IsOcean)
				{
					if (Human == Owner) GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText($"ERROR/AMPHIB")));
					return false;
				}
				return Confront(relX, relY);
			}
			// Zone of control blocks a step between two tiles that both border an enemy unit
			// IN THE OPEN. A garrisoned city projects no ZOC — only field units do — so exclude
			// city tiles from the border scans (matches the pathfinder's InZoc in Common.cs).
			// Without this a foreign city's garrison ZOC-blocked every adjacent approach, so a
			// unit at war couldn't move up to attack a city that had no field units near it.
			// ONE pass over the unit list, not up to twenty-four.
			//
			// This test used to read `GetBorderTiles()...SelectMany(t => t.Units)` three
			// times. ITile.Units is Game.GetUnits(x, y), which scans EVERY unit in the game,
			// sorts the matches and allocates an array — so eight border tiles cost eight full
			// scans, and three chains cost up to twenty-four. Every land unit paid that on
			// every step.
			//
			// Measured over turns 663-668 of a live 2,121-unit game, with the phases of an AI
			// move split into buckets: move:MoveTo cost 15.84 ms a call and 63.4 seconds of a
			// 95-second turn, against 0.68 ms for pathfinding and 0.41 ms for choosing a
			// mission. 24 scans x 2,121 units x 4,003 moves is ~200 million unit visits a turn.
			//
			// The pathfinder solved exactly this in Common.GotoStepInner — one occupancy pass
			// per search instead of ITile.Units per neighbour, noted there as once worth
			// "~100ms per path and 80% of the late-game turn" — and Map.NumberWaterBodies and
			// AI.StagingTile have both since had the same treatment. This is the fourth.
			//
			// Behaviour is unchanged, including which tiles count: a tile holding a CITY
			// projects no zone of control, so city tiles are excluded from both rings exactly
			// as before.
			if (Class == UnitClass.Land && !(this is Diplomat || this is Caravan || this is Explorer)
			    && !((ITile[])[Map[X, Y], moveTarget]).Any(t => t.IsOcean || t.City is not null))
			{
				bool ownUnitOnTarget = false, foreignByTarget = false, foreignByHere = false;
				int w = Map.WIDTH;
				bool Adjacent(int ax, int ay, int bx, int by)
				{
					int dx = Math.Abs(ax - bx);
					if (dx > w / 2) dx = w - dx;
					return dx <= 1 && Math.Abs(ay - by) <= 1 && !(dx == 0 && ay == by);
				}
				foreach (IUnit u in Game.GetUnits())
				{
					if (u is null) continue;
					if (u.Owner == Owner)
					{
						if (!ownUnitOnTarget && u.X == moveTarget.X && u.Y == moveTarget.Y)
							ownUnitOnTarget = true;
						continue;
					}
					// A garrison projects no ZOC — only field units do.
					if (Map[u.X, u.Y]?.City is not null) continue;
					if (!foreignByTarget && Adjacent(u.X, u.Y, moveTarget.X, moveTarget.Y)) foreignByTarget = true;
					if (!foreignByHere   && Adjacent(u.X, u.Y, X, Y))                       foreignByHere = true;
				}

				if (foreignByTarget && foreignByHere && !ownUnitOnTarget)
				{
					// Only when a PERSON is driving. Under Autopilot the AI moves the human's
					// units too, so a refusal that a player would see once — because a person
					// makes one move and reads the note — fires on every blocked attempt the
					// AI makes with them. Each one is a modal error the task queue must then
					// dwell on and dismiss, which is why this is the one notice that visibly
					// stutters an unattended run while every other message passes unnoticed.
					//
					// Nobody is reading it in that mode, and the AI does not need telling: it
					// re-plans the move regardless.
					if (Human == Owner && !Settings.Instance.Autopilot)
						GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText($"ERROR/ZOC")));
					return false;
				}
			}
			if (moveTarget.City is not null && moveTarget.City.Owner != Owner)
			{
				return Confront(relX, relY);
			}

			if (!MoveTargets.Any(t => t.X == moveTarget.X && t.Y == moveTarget.Y))
			{
				// Target tile is invalid
				// TODO: For some tiles, display a message detailing why the move is illegal
				return false;
			}

			// TODO: This implementation was done by observation, may need a revision
			bool srcRoad = Tile.Road || Tile.RailRoad || Tile.TransportTube || Tile.City is not null;
			bool dstRoad = moveTarget.Road || moveTarget.RailRoad || moveTarget.TransportTube || moveTarget.City is not null;
			bool riverBonus = (Tile is River && moveTarget is River)
				|| (Player.Civilization is Civilizations.Olvir && Tile is Jungle && moveTarget is Jungle);
			if (srcRoad && dstRoad || riverBonus)
			{
				// Handle movement in MovementDone
			}
			else if (MovesLeft == 0 && !moveTarget.Road && moveTarget.City is null && moveTarget.Movement > 1 && !IgnoresTerrainCost)
			{
				bool success;
				if (PartMoves >= 2)
				{
					// 2/3 moves left? 50% chance of success
					success = (Common.Random.Next(0, 2) == 0);
				}
				else
				{
					// 2/3 moves left? 33% chance of success
					success = (Common.Random.Next(0, 3) == 0);
				}

				if (!success)
				{
					PartMoves = 0;
					return false;
				}
			}

			MovementTo(relX, relY);
			return true;
		}

		private void MoveEnd(object sender, EventArgs args)
		{
			ITile previousTile = Map[_x, _y];
			X += Movement!.RelX;
			Y += Movement!.RelY;
			if (X == Goto.X && Y == Goto.Y)
			{
				Goto = Point.Empty;
			}
			Movement = null;

			Home?.InvalidateCache();
			Explore();
			MovementDone(previousTile);

			// Wake any adjacent human sentry units when an enemy moves next to them
			if (Game.Started && Human != Owner)
			{
				foreach (ITile adjacent in Tile.GetBorderTiles())
					foreach (IUnit sleeping in adjacent.Units.Where(u => u.Sentry && Human == u.Owner).ToList())
					{
						sleeping.Sentry = false;
						sleeping.MovesLeft = sleeping.Move;
					}
			}
		}

		protected void MovementTo(int relX, int relY)
		{
			MovementStart(Tile);
			Movement = new MoveUnit(relX, relY, MoveIsVisible);
			Movement!.Done += MoveEnd;
			GameTask.Insert(Movement!);
		}

		protected virtual void MovementStart(ITile previousTile)
		{
		}

		protected virtual void MovementDone(ITile previousTile)
		{
			bool railRailMove = (previousTile.RailRoad || previousTile.TransportTube) && (Tile.RailRoad || Tile.TransportTube);
			Log($"[MovementDone] {GetType().Name} ({previousTile.X},{previousTile.Y})->({X},{Y}) prevRail={previousTile.RailRoad} curRail={Tile.RailRoad} railRailMove={railRailMove} ML={MovesLeft}");
			if (MovesLeft > 0 && !railRailMove)
				MovesLeft--;
			Log($"[MovementDone] ML after decrement={MovesLeft}");

			Tile.Visit(Owner);

			if (Tile.Hut)
			{
				Tile.Hut = false;
			}
		}
		
		private static IBitmap[] _iconCache = new IBitmap[Enum.GetValues(typeof(UnitType)).Length];
		public virtual IBitmap Icon { get; private set; } = null!;
		private string _name = null!;
		public string Name
		{
			get => Modifications.LastOrDefault(x => x.Name.HasValue)?.Name.Value ?? _name;
			protected set => _name = value;
		}
		public byte PageCount => 2;

		// Override in a subclass to supply Civilopedia text directly from code, parallel to
		// BaseBuilding.GetPageText / BaseWonder.GetPageText. Returning a non-empty array takes
		// precedence over the BLURB2.TXT fallback used by the original game data.
		public virtual string[] GetPageText(byte pageNumber) => new string[0];

		public Picture DrawPage(byte pageNumber)
		{
			string[] text = GetPageText(pageNumber);
			if (text.Length == 0)
			{
				switch (pageNumber)
				{
					case 1:
						text = Resources.GetCivilopediaText("BLURB2/" + _name.ToUpper());
						break;
					case 2:
						text = Resources.GetCivilopediaText("BLURB2/" + _name.ToUpper() + "2");
						break;
					default:
						Log("Invalid page number: {0}", pageNumber);
						break;
				}
			}
			
			Picture output = new Picture(320, 200);
			
			output.AddLayer(this.ToBitmap(1), 215, 47);
			
			int yy = 76;
			foreach (string line in text)
			{
				Log(line);
				output.DrawText(line, 6, 1, 12, yy);
				yy += 9;
			}
			
			if (pageNumber == 2)
			{
				yy += 8;
				string requiredTech = "";
				if (RequiredTech is not null) requiredTech = RequiredTech.Name;
				output.DrawText($"Requires {requiredTech}", 6, 9, 100, yy); yy += 8;
				output.DrawText($"Cost: {Price}0 resources.", 6, 9, 100, yy); yy += 8;
				output.DrawText($"Attack Strength: {Attack}", 6, 12, 100, yy); yy += 8;
				output.DrawText($"Defense Strength: {Defense}", 6, 12, 100, yy); yy += 8;
				output.DrawText($"Movement Rate: {Move}", 6, 5, 100, yy);
			}
			
			return output;
		}
		
		private IAdvance? _requiredTech;
		public IAdvance? RequiredTech
		{
			get => Modifications.LastOrDefault(x => x.Requires.HasValue)?.Requires.Value.ToInstance() ?? _requiredTech;
			protected set => _requiredTech = value;
		}

		public IWonder? RequiredWonder { get; protected set; }

		private IAdvance? _obsoleteTech;
		public IAdvance? ObsoleteTech
		{
			get => Modifications.LastOrDefault(x => x.Obsolete.HasValue)?.Obsolete.Value.ToInstance() ?? _obsoleteTech;
			protected set => _obsoleteTech = value;
		}

		public UnitClass Class { get; protected set; }
		public UnitType Type { get; protected set; }
		public City? Home { get; protected set; }
		public short _buyPrice;
		public short BuyPrice
		{
			get => Modifications.LastOrDefault(x => x.BuyPrice.HasValue)?.BuyPrice.Value ?? _buyPrice;
			private set => _buyPrice = value;
		}
		public byte ProductionId => (byte)Type;
		private byte _price;
		public byte Price
		{
			get => Modifications.LastOrDefault(x => x.Price.HasValue)?.Price.Value ?? _price;
			protected set => _price = value;
		}
		public virtual UnitRole Role
		{
			get
			{
				UnitRole output = UnitRole.LandAttack;
				if (this is Settlers) output = UnitRole.Settler;
				else if (this is Caravan || this is Diplomat) output = UnitRole.Civilian;
				else if (this is BaseUnitSea)
				{
					if (this is IBoardable) output = UnitRole.Transport;
					else output = UnitRole.SeaAttack;
				}
				else if (this is Fighter) output = UnitRole.AirAttack;
				else if (this.Defense >= this.Attack) output = UnitRole.Defense;
				return output;
			}
		}

		private byte _attack;
		// City Walls stop most things. They do not stop a siege gun or a heavy bomber.
		//
		// This was written as `attackUnit.IgnoresCityWalls` in the two places below — the attack
		// VALUE standing in for the rule. Three units happen to have attack 12 (Artillery,
		// Bomber, and the Cruise Missile added later), so the missile inherited wall-piercing
		// that nobody chose, and the next unit given a 12 would have inherited it too.
		public virtual bool IgnoresCityWalls => false;

		public byte Attack
		{
			get => Modifications.LastOrDefault(x => x.Attack.HasValue)?.Attack.Value ?? _attack;
			protected set => _attack = value;
		}
		
		private byte _defense;
		public byte Defense
		{
			get => Modifications.LastOrDefault(x => x.Defense.HasValue)?.Defense.Value ?? _defense;
			protected set => _defense = value;
		}

		private byte _move;
		public byte Move
		{
			get => Modifications.LastOrDefault(x => x.Moves.HasValue)?.Moves.Value ?? _move;
			protected set => _move = value;
		}

		public int X
		{
			get
			{
				return _x;
			}
			set
			{
				int val = value;
				while (val < 0) val += Map.WIDTH;
				while (val >= Map.WIDTH) val -= Map.WIDTH;
				if (_x == -1 && _y != -1) Explore();
				_x = val;
			}
		}
		public int Y
		{
			get
			{
				return _y;
			}
			set
			{
				if (value < 0 || value >= Map.HEIGHT) return;
				if (_y == -1 && _x != -1 && value != -1) Explore();
				_y = value;
			}
		}

		public Point Goto { get; set; }
		
		public ITile Tile => Map[_x, _y];

		private byte _owner;
		public byte Owner
		{
			get => _owner;
			set
			{
				_owner = value;
				if (Game.Started) Tile.Visit(_owner);
			}
		}

		public Player Player => Game.GetPlayer(Owner);

		public byte Status
		{
			get
			{
				byte statusByte = 0;
				if (Sentry)         statusByte |= (1 << 0);
				if (FortifyActive)  statusByte |= (1 << 2);
				if (_fortify)       statusByte |= (1 << 3);
				if (Veteran)        statusByte |= (1 << 5);
				return statusByte;
			}
			set
			{
				bool[] bits = new bool[8];
				for (int i = 0; i < 8; i++)
					bits[i] = (((value >> i) & 1) > 0);
				if (bits[0]) Sentry = true;
				else if (bits[2]) FortifyActive = true;
				else if (bits[3]) _fortify = true;
				
				if (this is Settlers settlers)
				{
					settlers.SetStatus(bits);
				}

				Veteran = bits[5];
			}
		}
		public byte MovesLeft { get; set; }
		public byte PartMoves { get; set; }

		// ── Reverse engineering ──────────────────────────────────────────────────
		//
		// Hardware taken off a foreign army teaches you how to build it — but only if you
		// keep it intact long enough for your engineers to take it apart. Twenty turns is
		// deliberately long: the interesting decision is whether to spend a superior unit
		// fighting now or garrison it in the rear until it pays out a whole advance.
		//
		// Nothing here fires for a unit you built: CapturedOn is set only in Confront, and
		// only when the loser's RequiredTech was one you did not have.
		internal const int ReverseEngineerTurns = 20;

		public int? CapturedOn { get; set; }

		// One in four, and only for hardware we could not have built ourselves. Everything
		// else is destroyed as before — this is not a general "capture units" rule, it is the
		// narrow case where taking the wreck home is worth more than the kill.
		internal const int SalvageChance = 25;

		// A craft belonging to the visitors, whoever they turned out to be. Barbarian
		// megafauna and the other unbuildables are NOT this: they also carry a null
		// RequiredTech, and letting them pay out would hand the stars to anyone who shot a
		// monster.
		internal static bool IsVisitorCraft(IUnit u)
		{
			Player owner = Game.Instance.GetPlayer(u.Owner);
			return owner is not null
				&& owner.Civilization is Civilizations.Olvir or Civilizations.TheOthers;
		}

		// Start this civ's fuel clock the first time it beats one. Idempotent: the clock runs
		// from the FIRST wreck, so killing a second craft does not restart the wait.
		private void NoteVisitorWreck(IUnit loser)
		{
			if (!IsVisitorCraft(loser)) return;
			// ...and the visitors do not salvage their own, nor do the other story factions
			// salvage anything. Same exclusion as the gift, for the same reason: none of them
			// may claim the Diaspora, so fuel would only buy them wasted production.
			if (Player.Civilization is Civilizations.TheOthers or Civilizations.TheThing
			                        or Civilizations.Skynet or Civilizations.Olvir) return;
			PlayerProgress progress = Player.Progress;
			if (progress.HasExoticFuel || progress.ExoticFuelClock != 0) return;
			progress.ExoticFuelClock = (int)Game.GameTurn;
			DecisionLogger.LogSalvage("visitor-wreck", Player, loser, 0, null);
			if (Human == Owner)
				GameTask.Enqueue(Message.General($"We have broken open a {loser.Name}.",
					$"Give our engineers {ReverseEngineerTurns} years", "and they will have its drive."));
		}

		// Called on a won attack, BEFORE the defender is destroyed. True = taken instead.
		private bool Salvage(IUnit loser)
		{
			// A city's garrison is not salvage — the city itself is the prize, and its
			// capture already has its own advance-stealing path (GetAdvancesToSteal).
			if (loser.Tile.City is not null) return false;
			// Only from a lone unit in the open. Flipping one unit's flag inside an enemy
			// stack would leave it standing among units still at war with it.
			if (loser.Tile.Units.Length != 1) return false;
			if (loser.Class != UnitClass.Land) return false;
			// Nothing to learn: either we already build these, or nobody can — Harvesters and
			// the other unbuildable barbarian units have RequiredTech null, so alien
			// machinery is never salvageable no matter how long it is held.
			if (loser.RequiredTech is null || Player.HasAdvance(loser.RequiredTech)) return false;
			if (Common.Random.Next(100) >= SalvageChance) return false;

			loser.SetHome(null);
			loser.Owner = Owner;
			loser.CapturedOn = Game.GameTurn;
			loser.Goto = Point.Empty;
			loser.Sentry = false;
			loser.Fortify = false;
			loser.MovesLeft = 0;
			loser.PartMoves = 0;
			Log($"[Salvage] {GetType().Name} P{Owner} captures {loser.GetType().Name} at ({loser.X},{loser.Y})");
			// ...and to the decision log, which survives a RELEASE build. Log() above does not.
			DecisionLogger.LogSalvage("captured", Player, loser, 0, null);
			if (Human == Owner)
			{
				GameTask.Enqueue(Message.General($"We have captured an intact {loser.Name}!",
					$"Hold it {ReverseEngineerTurns} turns and our engineers", "will learn to build it."));
			}
			return true;
		}

		// Returns true when the clock paid out, so callers can report it.
		private bool ReverseEngineer()
		{
			if (CapturedOn is null) return false;
			if (Game.GameTurn - CapturedOn.Value < ReverseEngineerTurns) return false;

			int held = Game.GameTurn - CapturedOn.Value;
			CapturedOn = null;
			IAdvance? tech = RequiredTech;
			// Learned it in the meantime, or the unit teaches nothing — the clock still
			// stops, or it would re-check every turn for the rest of the game.
			if (tech is null || Player.HasAdvance(tech)) return false;

			// setOrigin: false — you did not discover this, you took it apart. Origin drives
			// who the Great Library credits, and the civ you looted should keep that credit.
			Player.AddAdvance(tech, false);
			DecisionLogger.LogSalvage("learned", Player, this, held, (tech as ICivilopedia)?.Name);
			return true;
		}

		// Hover units glide over rough terrain at the normal 1-MP/tile rate — they skip the
		// last-move penalty for entering Hills/Mountains (see MoveTo). Default off.
		public virtual bool IgnoresTerrainCost => false;

		public virtual void NewTurn()
		{
			if (FortifyActive)
			{
				FortifyActive = false;
				_fortify = true;
			}
			MovesLeft = Move;
			if (ReverseEngineer() && Human == Owner)
			{
				GameTask.Enqueue(Message.General($"Our engineers have stripped the captured {Name}",
					"and can now build our own!"));
			}
			Explore();
		}

		public void SetHome()
		{
			if (Map[X, Y].City is null) return;
			SetHome(Map[X, Y].City);
		}

		public void SetHome(City? city)
		{
			if (Home == city) return;
			Home?.RemoveHomeUnit(this);
			Home = city;
			city?.AddHomeUnit(this);
		}
		
		public void Pillage()
		{
			if (!(Tile.Irrigation || Tile.Mine || Tile.TransportTube || Tile.Road || Tile.RailRoad))
				return;

			if (Tile.Irrigation)
				Tile.Irrigation = false;
			else if (Tile.Mine)
				Tile.Mine = false;
			else if (Tile.TransportTube)
				Tile.TransportTube = false;
			else if (Tile.Road)
				Tile.Road = false;
			else if (Tile.RailRoad)
			{
				Tile.RailRoad = false;
				Tile.Road = true;
			}
			
			MovesLeft = 0;
			PartMoves = 0;
		}

		public virtual void SkipTurn()
		{
			MovesLeft = 0;
			PartMoves = 0;
		}
		
		protected void SetIcon(char page, int col, int row)
		{
			if (_iconCache[(int)Type] is null)
			{
				_iconCache[(int)Type] = Resources[$"ICONPG{page}"][col * 160, row * 62, 160, 60]
					.ColourReplace((byte)(GFX256 ? 253 : 15), 0);
			}
			Icon = _iconCache[(int)Type];
		}

		protected MenuItem<int> MenuNoOrders() => MenuItem<int>.Create("No Orders").SetShortcut("space").OnSelect((s, a) => SkipTurn());
		
		protected MenuItem<int> MenuFortify() => MenuItem<int>.Create("Fortify").SetShortcut("f").OnSelect((s, a) => Fortify = true);
		
		// 'z', not 'w': GameMap.KeyDown claims 'w' for waking the next sleeping unit — the
		// key that walks an obsolete army one unit at a time for upgrading — and it returns
		// before any unit order is dispatched. Tab still waits too.
		protected MenuItem<int> MenuWait() => MenuItem<int>.Create("Wait").SetShortcut("z").OnSelect((s, a) => Game.UnitWait());
		
		protected MenuItem<int> MenuSentry() => MenuItem<int>.Create("Sentry").SetShortcut("s").OnSelect((s, a) => Sentry = true);
		
		protected MenuItem<int> MenuGoTo() => MenuItem<int>.Create("GoTo").SetShortcut("g").OnSelect((s, a) => GameTask.Enqueue(Show.Goto));
		
		protected MenuItem<int> MenuPillage() => MenuItem<int>.Create("Pillage").SetShortcut("P").OnSelect((s, a) => Pillage());
		
		protected MenuItem<int> MenuHomeCity() => MenuItem<int>.Create("Home City").SetShortcut("h").OnSelect((s, a) => SetHome());
		
		protected MenuItem<int> MenuDisbandUnit() => MenuItem<int>.Create("Disband Unit").SetShortcut("D").OnSelect((s, a) => Game.DisbandUnit(this));

		// Returns the unit type this unit can upgrade to (one step), or null if none.
		public virtual UnitType? UpgradesTo => null;

		// Returns true when all conditions for upgrading are met, populating the resolved
		// target type, its name and the gold cost. Conditions: unit is in a city with a
		// Barracks, a fieldable target exists in its upgrade chain, and the player has the gold.
		protected bool CanUpgrade(out UnitType targetType, out string targetName, out int cost)
		{
			targetType = default;
			targetName = "";
			cost = 0;
			if (!UpgradesTo.HasValue) return false;
			City city = Map[X, Y].City;
			if (city is null || city.Owner != Owner) return false;
			if (!city.HasBuilding<Barracks>()) return false;

			// Walk the upgrade chain to the first unit the player can actually field, skipping
			// obsolete or otherwise unavailable intermediates. Without this an obsolete middle
			// tier (e.g. Knights once Automobile is researched) seals off the whole chain and the
			// unit below it (Chariot) can never upgrade — here it skips straight to Armor.
			// ProductionAvailable covers tech, obsolescence and wonder gates in one check.
			UnitType? next = UpgradesTo;
			for (int guard = 0; next.HasValue && guard < 16; guard++)   // guard: chains are short and acyclic
			{
				IUnit? cand = Game.PeekUnit(next.Value);
				if (cand is null) break;
				if (Player.ProductionAvailable(cand))
				{
					targetType = next.Value;
					targetName = cand.Name;
					cost = (int)cand.Price * 10;
					return Player.Gold >= cost;
				}
				next = (cand as BaseUnit)?.UpgradesTo;
			}
			return false;
		}

		protected MenuItem<int>? MenuUpgrade()
		{
			if (!CanUpgrade(out UnitType targetType, out string targetName, out int cost)) return null;
			return MenuItem<int>.Create($"Upgrade to {targetName} ({cost}g)")
				.OnSelect((s, a) => Game.UpgradeUnit(this, targetType, cost));
		}

		public abstract IEnumerable<MenuItem<int>> MenuItems { get; }

		protected abstract bool ValidMoveTarget(ITile tile);

		public IEnumerable<ITile> MoveTargets => Map[X, Y].GetBorderTiles().Where(t => ValidMoveTarget(t));

		protected void Explore(int range, bool sea = false, bool noCorners = false)
		{
			if (Game is null) return;
			Player player = Game.GetPlayer(Owner);
			if (player is null) return;
			player.Explore(X, Y, range, sea, noCorners);
			if (player.IsHuman) Common.GamePlay?.RefreshMap();
		}

		public virtual void Explore()
		{
			bool onMountain = Map[X, Y].Type == Terrain.Mountains;
			Explore(onMountain ? 2 : 1, noCorners: onMountain);
		}

		internal static IBitmap? GetBaseSprite(UnitType type)
		{
			if (!_modifications.ContainsKey(type)) return null;
			return _modifications[type].LastOrDefault(x => x.Sprite is not null && x.Sprite.GifToBitmap() is not null)?.Sprite.GifToBitmap();
		}

		private static Dictionary<UnitType, Bytemap> _pngOverrides = new Dictionary<UnitType, Bytemap>();

		internal static bool GetPngOverride(UnitType type, out Bytemap? copy)
		{
			if (_pngOverrides.TryGetValue(type, out Bytemap bmap))
			{
				copy = bmap[0, 0, bmap.Width, bmap.Height];
				return true;
			}
			copy = null;
			return false;
		}

		private static void LoadUnitTiles()
		{
			_pngOverrides.Clear();
			string tilesDir = Path.Combine(Settings.Instance.StorageDirectory, "unit_tiles");
			if (!Directory.Exists(tilesDir)) return;

			foreach (string file in Directory.GetFiles(tilesDir, "*.png"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (!Enum.TryParse(name, ignoreCase: true, out UnitType unitType)) continue;
				Bytemap? bmap = LoadPngTile(file);
				if (bmap is not null)
				{
					_pngOverrides[unitType] = bmap;
					Log($"Loaded unit tile: {name}");
				}
			}

			// unit_tiles.txt: [section_name] sections with 256 palette-index pixels
			// (16×16), a map unit tile override. Sections override any PNG of the
			// same name.
			string txtPath = Path.Combine(tilesDir, "unit_tiles.txt");
			if (!File.Exists(txtPath)) return;

			string? currentSection = null;
			var pixels = new List<byte>();
			foreach (string raw in File.ReadAllLines(txtPath))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;
				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					FlushTxtSection(currentSection, pixels);
					currentSection = line.Substring(1, line.Length - 2);
					pixels = new List<byte>();
				}
				else if (currentSection is not null)
				{
					foreach (string tok in line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries))
						if (byte.TryParse(tok, out byte v)) pixels.Add(v);
				}
			}
			FlushTxtSection(currentSection, pixels);
		}

		private static void FlushTxtSection(string? name, List<byte> pixels)
		{
			if (name is null) return;
			if (!Enum.TryParse(name, ignoreCase: true, out UnitType unitType)) return;
			if (pixels.Count == 256)
			{
				var bmap = new Bytemap(16, 16);
				for (int y = 0; y < 16; y++)
				for (int x = 0; x < 16; x++)
					bmap[x, y] = pixels[y * 16 + x];
				_pngOverrides[unitType] = bmap;
				Log($"Loaded unit tile from txt: {name}");
			}
		}

		private static Bytemap? LoadPngTile(string path)
		{
			byte[] rgba = PngFile.ReadRgba(path, out int w, out int h);
			if (rgba is null || w != 16 || h != 16) return null;
			using Palette pal = Common.DefaultPalette;
			CassetteTheme.ApplyTo(pal);
			byte[,] idx = PngFile.ToIndices(rgba, w, h, pal);
			var bmap = new Bytemap(w, h);
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				bmap[x, y] = idx[y, x] == CassetteTheme.BG0 ? (byte)0 : idx[y, x];
			return bmap;
		}

		private static Dictionary<UnitType, List<UnitModification>> _modifications = new Dictionary<UnitType, List<UnitModification>>();
		internal static void LoadModifications()
		{
			_modifications.Clear();
			LoadUnitTiles();

			UnitModification[] unitModifications = Reflect.GetModifications<UnitModification>().ToArray();
			if (unitModifications.Length == 0) return;

			Log("Applying unit modifications");

			foreach (UnitModification modification in Reflect.GetModifications<UnitModification>())
			{
				if (!_modifications.ContainsKey(modification.UnitType))
					_modifications.Add(modification.UnitType, new List<UnitModification>());
				_modifications[modification.UnitType].Add(modification);
			}

			Log("Finished applying unit modifications");
		}
		public IEnumerable<UnitModification> Modifications => _modifications.ContainsKey(Type) ? _modifications[Type].ToArray() : new UnitModification[0];
		
		protected BaseUnit(byte price = 1, byte attack = 1, byte defense = 1, byte move = 1)
		{
			Price = price;
			BuyPrice = (short)((Price + 4) * 10 * Price);
			Attack = attack;
			Defense = defense;
			Move = move;
			X = -1;
			Y = -1;
			Goto = Point.Empty;
			Owner = 0;
			Status = 0;
			RequiredWonder = null;
		}
	}
}