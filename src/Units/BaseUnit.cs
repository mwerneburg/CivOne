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
			// Air attack on a non-air defender: terrain and fortification bonuses are stripped
			// unless the city has a SAM Battery.  This makes air units dominant in the field
			// and gives SAM Batteries a clear, exclusive defensive role.
			if (attackUnit.Class == UnitClass.Air && defendUnit.Class != UnitClass.Air)
			{
				bool hasSam = defendUnit.Tile.City?.HasBuilding<Buildings.SamBattery>() == true;
				if (!hasSam)
				{
					int baseDefend = (int)defendUnit.Defense * 2;
					if (defendUnit.Veteran) baseDefend += baseDefend / 2;
					return baseDefend;
				}
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
				
				if (!cityWalls || attackUnit.Attack == 12)
				{
					// Step 3: If the defending unit is a ground unit, multiply the defense strength by the Fortification Modifier.
					// This modifier effectively includes a factor of 4, resulting in a combined factor of 8.
					defendStrength *= fortificationModifier;
				}
			}

			// Step 4: If the defending unit is a sea or air unit, multiply the defense strength by 8.
			// This effectively treats the Terrain Modifier as 2, regardless of the actual terrain type. It also means that these units will never benefit from the Fortification Modifier.
			if (defendUnit.Class != UnitClass.Land && (!cityWalls || attackUnit.Attack == 12))
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
			// they end up here only if those overrides don't catch the case. Settlers and
			// Hydro Engineer have Attack=0 — sending them into combat is suicide and the AI
			// pathfinder doesn't avoid enemy-occupied tiles, so guard at the boundary.
			if (Class == UnitClass.Land && (this is Diplomat || this is Caravan || this is Settlers || this is HydroEngineer))
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
			// Under Autopilot the acting player IS the human slot, so without this clause
			// the autopiloted civ is the only democracy in the world that cannot start a
			// war — every AI civ attacks freely while it stands down. Same rule the
			// difficulty handicap and the diplomat targeting already follow (City.cs
			// "aiRun", AI.Strategy "HumanOpponent"), and it keeps autoplay runs
			// representative of AI behaviour rather than of a crippled human slot.
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
					if (unit is not null)
					{
						GameTask.Insert(Show.DestroyUnit(unit, true));
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
			if (Class == UnitClass.Land && !(this is Diplomat || this is Caravan || this is Explorer) && !((ITile[])[Map[X, Y], moveTarget]).Any(t => t.IsOcean || t.City is not null) && moveTarget.GetBorderTiles().Where(t => t.City is null).SelectMany(t => t.Units).Any(u => u.Owner != Owner))
			{
				if (!moveTarget.Units.Any(x => x.Owner == Owner))
				{
					IUnit[] targetUnits = moveTarget.GetBorderTiles().Where(t => t.City is null).SelectMany(t => t.Units).Where(u => u.Owner != Owner).ToArray();
					IUnit[] borderUnits = Map[X, Y].GetBorderTiles().Where(t => t.City is null).SelectMany(t => t.Units).Where(u => u.Owner != Owner).ToArray();

					if (borderUnits.Any() && targetUnits.Any())
					{
						if (Human == Owner)
							GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText($"ERROR/ZOC")));
						return false;
					}
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
		
		protected MenuItem<int> MenuWait() => MenuItem<int>.Create("Wait").SetShortcut("w").OnSelect((s, a) => Game.UnitWait());
		
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

			// unit_tiles.txt: [section_name] sections with palette-index pixel data.
			// 256 pixels (16×16) → map unit tile override.
			// 1024 pixels (32×32) → garrison icon override.
			// Sections override any PNG of the same name.
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
			else if (pixels.Count == 1024)
			{
				var bmap = new Bytemap(32, 32);
				for (int y = 0; y < 32; y++)
				for (int x = 0; x < 32; x++)
					bmap[x, y] = pixels[y * 32 + x];
				_garrisonIcons[unitType] = bmap;
				Log($"Loaded garrison icon from txt: {name}");
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

		// ── garrison icons (32×32, used directly without upscaling) ──────────────

		private static Dictionary<UnitType, Bytemap> _garrisonIcons = new Dictionary<UnitType, Bytemap>();

		internal static bool GetGarrisonIcon(UnitType type, out Bytemap icon)
		{
			return _garrisonIcons.TryGetValue(type, out icon);
		}

		private static void LoadGarrisonIcons()
		{
			_garrisonIcons.Clear();
			string dir = Path.Combine(Settings.Instance.StorageDirectory, "garrison_icons");
			if (!Directory.Exists(dir)) return;
			foreach (string file in Directory.GetFiles(dir, "*.png"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (!Enum.TryParse(name, ignoreCase: true, out UnitType unitType)) continue;
				Bytemap? bmap = LoadGarrisonTile(file);
				if (bmap is not null)
				{
					_garrisonIcons[unitType] = bmap;
					Log($"Loaded garrison icon: {name}");
				}
			}
		}

		private static Bytemap? LoadGarrisonTile(string path)
		{
			byte[] rgba = PngFile.ReadRgba(path, out int w, out int h);
			if (rgba is null || w != 32 || h != 32) return null;
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
			LoadGarrisonIcons();

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