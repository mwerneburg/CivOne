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
using CivOne.IO;
using CivOne.Screens;
using CivOne.Screens.Reports;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne
{
	public partial class Game : BaseInstance
	{
		private readonly int _difficulty, _competition;
		private List<Player> _players;
		private readonly List<City> _cities;
		private readonly List<IUnit> _units;
		private readonly Dictionary<byte, byte> _advanceOrigin = new();
		private readonly List<ReplayData> _replayData = new();

		// [0]=barbarians, [1+]=civs; 0 = not yet launched; sized to player count at init, resized by AddPlayer()
		internal int[] SpaceshipLaunchTurn;
		internal int[] SpaceshipArrivalTurn;
		// SS part inventories — incremented when a city finishes a part; not stored as city buildings
		internal int[] SpaceshipStructural;
		internal int[] SpaceshipComponent;
		internal int[] SpaceshipModule;

		// Per-turn score snapshots: each int[] is [gameTurn, score0, score1, ..., scoreN]
		private readonly List<int[]> _scoreHistory = new();
		internal IReadOnlyList<int[]> ScoreHistory => _scoreHistory;

		internal void RecordScoreSnapshot()
		{
			var snap = new int[_players.Count + 1];
			snap[0] = _gameTurn;
			for (int i = 0; i < _players.Count; i++)
				snap[i + 1] = _players[i].Score;
			_scoreHistory.Add(snap);
		}

		// True once the satellite-coverage intelligence report has fired
		internal bool MapRevealedNotified;

		// Turn on which the SETI signal transmission should fire (0 = not scheduled)
		internal uint SETISignalTurn;

		// Set permanently once the SETI signal transmission has been shown.
		// Gates the InterstellarProbe wonder and both response paths (dome / spaceship).
		internal bool SETISignalReceived;

		// Set permanently when the visitors actually arrive (first contact) — ~80 turns
		// after the SETI signal. Gates the post-contact tech tree (Xenobiology et al.):
		// you can detect the signal and prepare, but you can't study alien biology until
		// you've met them.
		internal bool VisitorsArrived;

		// Archetype of the incoming visitors, seeded when the SETI signal fires
		internal VisitorArchetype VisitorType;

		// Turn on which the Tau Ceti approach warning fires (0 = not scheduled)
		internal uint TauCetiEscalationTurn;

		// Set when the probe wonder is built, cancels the approach warning (Phase 2)
		internal bool ProbeDispatched;

		// Turn on which the probe wonder was completed; drives interim + result scheduling.
		// 0 = probe not yet dispatched. Old saves with ProbeDispatched=true but this=0
		// have already shown their result and need no further action.
		internal uint ProbeDispatchTurn;

		// Which interim report fires next: 0=none yet, 1-3=phases, 4=result fired.
		internal int ProbeInterimPhase;

		// Advance IDs to grant the human player when the probe result fires (may be empty).
		internal int[] ProbeGrantedAdvanceIds = System.Array.Empty<int>();

		// Turn on which the Olvir arrival scene fires (0 = not yet scheduled).
		internal uint OlvirArrivalTurn;
		internal uint OlvirProximityAlarmTurn; // turn of the last "Olvir settling near you" advisor message

		// Outcome tier of the probe mission: 0=Destroyed 1=Partial 2=Identified 3=TechTransfer 4=Pact
		internal int ProbeOutcomeTier;

		// Olvir land-use improvements keyed by map tile (x, y).
		internal readonly Dictionary<(int x, int y), Enums.OlvirImprovementType> OlvirImprovements = new();

		// Dome path: which player (owner byte) is assigned to which dome wonder component(s).
		// Populated when the Tau Ceti approach warning fires.
		internal readonly Dictionary<byte, List<Enums.Wonder>> DomeAssignments = new();

		// Set when any player builds the first dome component (hard exclusivity gate).
		internal bool DomePathCommitted => BuiltWonders.Any(w => w is Wonders.IDomeComponent);

		// Set when all five dome components are built — triggers victory sequence.
		internal bool DomeComplete => _domeVictoryFired || DomeFiveComponents.All(w => WonderBuilt(w));

		private bool _domeVictoryFired = false;

		// Guards the conquest-victory sequence in EndTurn so it can't re-enqueue
		// the victory screens on every subsequent call before the game quits.
		private bool _conquestVictoryFired = false;

		internal static readonly Wonders.IWonder[] DomeFiveComponents =
		{
			new Wonders.DomeEmitterArray(),
			new Wonders.DomeSensorNet(),
			new Wonders.DomePowerCore(),
			new Wonders.DomeCommandHub(),
			new Wonders.DomeKineticRing(),
		};

		private static readonly Enums.Wonder[] _domeFiveWonderIds =
		{
			Enums.Wonder.DomeEmitterArray,
			Enums.Wonder.DomeSensorNet,
			Enums.Wonder.DomePowerCore,
			Enums.Wonder.DomeCommandHub,
			Enums.Wonder.DomeKineticRing,
		};

		// Log of terminal transmissions shown during this game
		internal readonly List<TransmissionRecord> Transmissions = new();

		internal void RecordTransmission(string type, string year)
			=> Transmissions.Add(new TransmissionRecord { Type = type, Year = year });

		// Exploration: byte[x, y] = player index who first revealed that tile; 255 = unvisited
		private byte[,] _firstExplorer = null!;
		internal byte[,] FirstExplorer
		{
			get
			{
				if (_firstExplorer is null)
				{
					_firstExplorer = new byte[Map.WIDTH, Map.HEIGHT];
					for (int x = 0; x < Map.WIDTH; x++)
					for (int y = 0; y < Map.HEIGHT; y++)
						_firstExplorer[x, y] = 255;
				}
				return _firstExplorer;
			}
			set => _firstExplorer = value;
		}

		internal bool ClaimTile(int x, int y, byte playerIdx)
		{
			if (FirstExplorer[x, y] != 255) return false;
			FirstExplorer[x, y] = playerIdx;
			return true;
		}

		internal readonly string[] CityNames = Common.AllCityNames.ToArray();
		
		private int _currentPlayer = 0;
		private int _activeUnit;
		private bool _activeUnitExplicit = false;
		private readonly HashSet<IUnit> _waitingUnits = new();

		private IUnit? _lastMovedUnit = null;
		private int _sameUnitMoveCount = 0;

		// One-shot diagnostic dump invoked from the circuit breaker. The goal is to capture
		// enough state to diagnose why AI.Move can't make progress on this unit — pathfinding
		// result, peaceful-block reason, continent mismatch, target city ownership — without
		// requiring a debugger attach. Everything goes through Log() so it lands in both the
		// debug console and the persisted game log.
		private void LogCircuitBreakerDiagnostics(IUnit unit)
		{
			try
			{
				ITile tile = unit.Tile;
				if (tile is null) { Log("[AI]   diag: unit.Tile is null (X={0}, Y={1})", unit.X, unit.Y); return; }
				byte myContinent = tile.ContinentId;
				Player owner = GetPlayer(unit.Owner);
				string ownerName = owner?.LeaderName ?? "<null>";

				if (unit.Goto.IsEmpty)
				{
					Log("[AI]   diag: Goto is empty; unit on Tile=({0},{1}) ContId={2} owner={3}", tile.X, tile.Y, myContinent, ownerName);
					return;
				}

				int gx = unit.Goto.X, gy = unit.Goto.Y;
				ITile targetTile = Map.Instance[gx, gy];
				byte targetContinent = targetTile?.ContinentId ?? 255;
				int distance = Common.DistanceToTile(unit.X, unit.Y, gx, gy);
				string targetCity = targetTile?.City is not null
					? $"{targetTile.City.Name}(P{targetTile.City.Owner}={GetPlayer(targetTile.City.Owner)?.LeaderName ?? "?"})"
					: "<none>";
				Log("[AI]   diag: Goto=({0},{1}) dist={2} unit.ContId={3} target.ContId={4} target.city={5}",
					gx, gy, distance, myContinent, targetContinent, targetCity);

				if (targetContinent != myContinent)
				{
					Log("[AI]   diag: continent mismatch — pathfinder will return null. Likely a stale Goto from a previous turn that AssignMission picked before reachability was checked.");
				}

				ITile? step = Common.GotoStep(unit, gx, gy);
				if (step is null)
				{
					Log("[AI]   diag: GotoStep returned null — no land path. Goto should be cleared but isn't.");
					return;
				}

				string stepUnits = step.Units.Length > 0
					? string.Join(", ", step.Units.Select(u => $"{u.GetType().Name}(P{u.Owner}={GetPlayer(u.Owner)?.LeaderName ?? "?"}={(u.Owner == 0 ? "barb" : owner is not null && owner.IsAtWar(GetPlayer(u.Owner)) ? "war" : "peace")})"))
					: "<empty>";
				string stepCity = step.City is not null
					? $"{step.City.Name}(P{step.City.Owner}={GetPlayer(step.City.Owner)?.LeaderName ?? "?"}={(step.City.Owner == 0 ? "barb" : owner is not null && owner.IsAtWar(GetPlayer(step.City.Owner)) ? "war" : "peace")})"
					: "<none>";
				Log("[AI]   diag: GotoStep=({0},{1}) terrain={2} step.units=[{3}] step.city={4}",
					step.X, step.Y, step.GetType().Name, stepUnits, stepCity);

				// Peaceful-block re-analysis using exactly the conditions AI.Move:343-358 checks.
				bool unitsBlock = step.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					&& GetPlayer(u.Owner) is Player pu && owner is not null && !owner.IsAtWar(pu));
				bool cityBlocks = step.City is not null && step.City.Owner != unit.Owner && step.City.Owner != 0
					&& GetPlayer(step.City.Owner) is Player pc
					&& pc.Civilization is not Civilizations.Barbarian
					&& owner is not null && !owner.IsAtWar(pc);
				Log("[AI]   diag: peaceful-block analysis: by-unit={0} by-city={1}", unitsBlock, cityBlocks);
				if (unitsBlock || cityBlocks)
				{
					Log("[AI]   diag: this step would trigger peaceful-block in AI.Move; expected behaviour is to clear Goto + SkipTurn. The fact that the unit is here means AI.Move never reached that branch.");
				}
			}
			catch (System.Exception ex)
			{
				Log("[AI]   diag: diagnostic dump itself threw {0}: {1}", ex.GetType().Name, ex.Message);
			}
		}

		// True for a land unit sitting on a non-city tile with a boardable ship —
		// it is effectively cargo and should not be prompted for orders.
		private static bool IsAboard(IUnit unit)
		{
			if (unit.Class != UnitClass.Land) return false;
			ITile tile = unit.Tile;
			if (tile is null || tile.City is not null) return false;
			return tile.Units.Any(u => u is IBoardable);
		}

		private ushort _anthologyTurn = 0;

		public bool Animations { get; set; }
		public bool CivilopediaText { get; set; }
		public bool EndOfTurn { get; set; }
		public bool InstantAdvice { get; set; }

		public bool EnemyMoves { get; set; }
		public bool Circuses { get; set; } = true;
		public bool Barricades { get; set; } = true;

		public void SetAdvanceOrigin(IAdvance advance, Player? player)
		{
			if (_advanceOrigin.ContainsKey(advance.Id))
				return;
			// A null player means a free/granted advance (e.g. handicap bonus techs)
			// with no genuine first-discoverer — record no origin. The old code stored
			// player 0 (the Barbarians, civ id 15), which the save layer couldn't round-
			// trip: the loader only re-attributes an origin to a civ that actually holds
			// the advance, and the Barbarians hold none, so it silently vanished on load.
			if (player is null)
				return;
			_advanceOrigin.Add(advance.Id, PlayerNumber(player));
		}
		public bool GetAdvanceOrigin(IAdvance advance, Player player)
		{
			if (_advanceOrigin.ContainsKey(advance.Id))
				return (_advanceOrigin[advance.Id] == PlayerNumber(player));
			return false;
		}

		public int Difficulty => _difficulty;

		public bool HasUpdate => false;
		
		internal ushort GlobalWarmingCount { get; set; }

		private ushort _gameTurn;
		internal ushort GameTurn
		{
			get
			{
				return _gameTurn;
			}
			set
			{
				_gameTurn = value;
				Log($"Turn {_gameTurn}: {GameYear}");
				if (_anthologyTurn >= _gameTurn)
				{
					//TODO: Show anthology
					_anthologyTurn = (ushort)(_gameTurn + 20 + Common.Random.Next(40));
				}
			}
		}
		
		internal string GameYear => Common.YearString(GameTurn);
		
		internal Player HumanPlayer { get; set; } = null!;
		
		internal Player CurrentPlayer => _players[_currentPlayer];

		internal ReplayData[] GetReplayData() => _replayData.ToArray();
		internal T[] GetReplayData<T>() where T : ReplayData => _replayData.Where(x => x is T).Cast<T>().ToArray();
		internal void AddReplayEvent(ReplayData entry) => _replayData.Add(entry);

		private void PlayerDestroyed(object sender, EventArgs args)
		{
			Player? player = (sender as Player);
			if (player is null) return;

			ICivilization destroyed = player.Civilization;
			// A civ destroyed during its own turn had no external attacker — its last city
			// starved out (City.cs famine → Size 0 → DestroyCity). Record the collapse as
			// self-destruction (DestroyedById == DestroyedId) so the replay can say
			// "collapsed" instead of mislabelling it "by the Barbarians" (the old
			// player-0 fallback, which the advisor message below had already stopped
			// using but the replay record hadn't).
			bool selfCollapse = Game.CurrentPlayer.Civilization == destroyed;
			ICivilization destroyedBy = Game.CurrentPlayer.Civilization;

			_replayData.Add(new ReplayData.CivilizationDestroyed(_gameTurn, destroyed.Id, destroyedBy.Id));

			if (player.IsHuman)
			{
				// TODO: Move Game Over code here
				return;
			}

			// Before 0 AD, respawn a destroyed AI civ as its buddy variant. ONLY the original
			// civs (Id 1–14) are paired: they share player slots 1–7 in two banks, so each has a
			// buddy at Id ± 7 (e.g. Romans Id=1 <-> Russians Id=8). Extended civs (Id 17–26) hold
			// exclusive slots and have NO buddy — the Id − 7 formula would map them onto an
			// original civ (Persians 18 → Aztecs 11, Inca 23 → Olvir 16, …) and spawn a duplicate
			// or special-case civ. That bug produced two Aztecs in one game (a destroyed Persia
			// respawning as a second, randomly-placed Aztec while the real Aztecs were still alive).
			if (!(destroyed is Barbarian) && destroyed.Id >= 1 && destroyed.Id <= 14 && Common.TurnToYear(_gameTurn) < 0)
			{
				byte playerSlot = (byte)destroyed.PreferredPlayerNumber;
				int buddyId = destroyed.Id >= 8 ? destroyed.Id - 7 : destroyed.Id + 7;
				bool buddyDestroyed = _replayData.OfType<ReplayData.CivilizationDestroyed>()
					.Any(rd => rd.DestroyedId == buddyId);
				// Never resurrect a buddy that is still in the game — the second guard that the
				// two-Aztecs bug slipped past (it only checked whether the buddy was *destroyed*).
				bool buddyAlive = _players.Any(p => p is not null && p.Civilization.Id == buddyId);
				ICivilization buddyCiv = Common.Civilizations.FirstOrDefault(c => c.Id == buddyId);
				if (!buddyDestroyed && !buddyAlive && buddyCiv is not null)
				{
					_players[playerSlot] = new Player(buddyCiv);
					_players[playerSlot].Destroyed += PlayerDestroyed;
					AddStartingUnits(playerSlot);
				}
			}

			if (selfCollapse)
				GameTask.Insert(Message.Advisor(Advisor.Defense, false, destroyed.Name, "civilization", "destroyed", "in collapse!"));
			else
				GameTask.Insert(Message.Advisor(Advisor.Defense, false, destroyed.Name, "civilization", "destroyed", $"by {destroyedBy.NamePlural}!"));
		}
		
		internal byte PlayerNumber(Player player)
		{
			byte i = 0;
			foreach (Player p in _players)
			{
				if (p == player)
					return i;
				i++;
			}
			return 0;
		}

		// Contract: callers pass a valid player number, so this is treated as non-null.
		// The bounds check is defensive against programming errors; it returns null in
		// that case only (hence null!), rather than forcing a null-check on every caller.
		internal Player GetPlayer(byte number)
		{
			if (number >= _players.Count)
				return null!;
			return _players[number];
		}

		internal IEnumerable<Player> Players => _players;

		internal void AddPlayer(Player player)
		{
			_players.Add(player);
			player.Destroyed += PlayerDestroyed;
			int n = _players.Count;
			Array.Resize(ref SpaceshipLaunchTurn,  n);
			Array.Resize(ref SpaceshipArrivalTurn, n);
			Array.Resize(ref SpaceshipStructural,  n);
			Array.Resize(ref SpaceshipComponent,   n);
			Array.Resize(ref SpaceshipModule,      n);
		}

		internal void ClearSpaceShipProduction(int playerIndex)
		{
			foreach (City city in _players[playerIndex].Cities.Where(c => c.CurrentProduction is Buildings.ISpaceShip))
			{
				IProduction fallback = city.AvailableProduction.FirstOrDefault();
				if (fallback is not null) city.SetProduction(fallback);
			}
		}

		// mass_ht = comps×4 + mods×4 + str (in hundred-ton units)
		// flight_years = (4445 + mass_ht) / (100 × engines)  where engines = comps/2
		internal static float SpaceshipFlightYears(int structural, int component, int module)
		{
			int engines = Math.Max(1, component / 2);
			int massHt = component * 4 + module * 4 + structural;
			return (4445f + massHt) / (100f * engines);
		}

		internal static int SpaceshipStructuresNeeded(int component, int module)
		{
			int engines = component / 2;
			int modSets = module / 3;
			return 15 + Math.Max(0, engines - 2) * 4 + Math.Max(0, modSets - 1) * 4;
		}

		// Success: 70% base (1 engine), +6.67% per additional engine up to +20%,
		//          +10% per additional module set above 1, capped at 100%.
		internal static int SpaceshipSuccessPct(int component, int module)
		{
			int engines = component / 2;
			int modSets = module / 3;
			int engineBonus = Math.Min(20, (engines - 1) * 20 / 3);
			int moduleBonus = Math.Min(10, Math.Max(0, modSets - 1) * 10);
			return Math.Min(100, 70 + engineBonus + moduleBonus);
		}

		// Score contribution: hab_modules × 500 × success% / 100
		internal static int SpaceshipScore(int module, int component)
		{
			return module * 500 * SpaceshipSuccessPct(component, module) / 100;
		}

		private static int SpaceshipTravelTurns(int structural, int component, int module)
		{
			return Math.Max(1, (int)Math.Ceiling(SpaceshipFlightYears(structural, component, module)));
		}

		// ── Pollution / Global Warming ───────────────────────────────────────────

		// Returns 0-4 warming indicator level: 0=none,1=darkred,2=lightred,3=yellow,4=white
		public int WarmingIndicator
		{
			get
			{
				int n = Map.AllTiles().Count(t => t.Pollution);
				if (n == 0) return 0;
				if (n == 1) return 1;
				if (n <= 3) return 2;
				if (n <= 5) return 3;
				return 4;
			}
		}

		private void HandleGlobalWarming()
		{
			int polluted = Map.AllTiles().Count(t => t.Pollution);
			int threshold = 8 + (GlobalWarmingCount * 2);
			if (polluted < threshold) return;

			GlobalWarmingCount++;

			// Sea-level-rise pass (ported from ChrisWi/CivOne
			// GlobalWarmingScourgeWithFloodService): low-lying / polar / wet tiles
			// roll for permanent submersion. Polar (top/bottom 3 rows or
			// Arctic/Tundra terrain) hit at GlobalWarmingCount * 20 % (cap 100);
			// River/Jungle/Swamp hit at GlobalWarmingCount * 10 %. On hit, land
			// units are disbanded, improvements clear, terrain becomes Ocean.
			// Cities immune. Irreversible — no engineer can reclaim ocean.
			int polarChance = Math.Min(GlobalWarmingCount * 20, 100);
			int otherChance = Math.Min(GlobalWarmingCount * 10, 100);
			foreach (ITile tile in Map.AllTiles().ToArray())
			{
				if (tile.City is not null || tile.IsOcean) continue;
				bool isPolar = tile.Y < 3 || tile.Y >= Map.HEIGHT - 3
					|| tile.Type == Terrain.Arctic || tile.Type == Terrain.Tundra;
				bool isAffected = isPolar
					|| tile.Type == Terrain.River
					|| tile.Type == Terrain.Jungle
					|| tile.Type == Terrain.Swamp;
				if (!isAffected) continue;
				int chance = isPolar ? polarChance : otherChance;
				if (Common.Random.Next(100) >= chance) continue;
				foreach (IUnit u in GetUnits(tile.X, tile.Y).Where(u => u.Class == UnitClass.Land).ToArray())
					DisbandUnit(u);
				tile.Road = false;
				tile.RailRoad = false;
				tile.Mine = false;
				tile.Fortress = false;
				tile.Irrigation = false;
				Map.ChangeTileType(tile.X, tile.Y, Terrain.Ocean);
			}

			// Remove all pollution, then transform affected tiles
			foreach (ITile tile in Map.AllTiles())
			{
				tile.Pollution = false;
				if (tile.City is not null || tile.IsOcean) continue;

				int adjacentOcean = tile.GetBorderTiles().Count(t => t is not null && t.IsOcean);
				int oceanThreshold = Math.Max(0, 7 - GlobalWarmingCount);

				if (adjacentOcean >= oceanThreshold)
				{
					// Flood: near-coast tiles become swamp/jungle
					Map.ChangeTileType(tile.X, tile.Y, tile is Tiles.Forest ? Terrain.Jungle : Terrain.Swamp);
					tile.Irrigation = false;
					tile.Mine = false;
				}
				else
				{
					// Dry out: deterministic mesh check (matches original algorithm)
					int mesh = (11 * tile.X + 13 * tile.Y) & 7;
					if (mesh != (GlobalWarmingCount & 7)) continue;
					// Skip Mountains — temperate/tropical peaks aren't levelled by warming.
					// Polar mountain submersion is handled by the sea-level-rise pass.
					if (tile.Type == Terrain.Mountains) continue;
					// Rivers re-form unless the area is completely desertified: skip
					// river dry-out unless every neighbour is already Desert.
					if (tile.Type == Terrain.River
						&& !tile.GetBorderTiles().All(n => n is not null && n.Type == Terrain.Desert))
						continue;
					bool isDesertOrPlains = tile.Type == Terrain.Desert || tile.Type == Terrain.Plains;
					Map.ChangeTileType(tile.X, tile.Y, isDesertOrPlains ? Terrain.Desert : Terrain.Plains);
					tile.Irrigation = false;
				}
			}

			GameTask.Enqueue(Show.EventArt("globalwarming", "Global warming! Icecaps melt."));
			// Advisor message belts-and-braces the art screen: if Autopilot dwell or a fast
			// dismiss skips the art, the advisor box stays until acknowledged.
			GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
				"Global warming!",
				"Icecaps melt, coastlines retreat,",
				"and deserts spread across the land."));
		}

		public void EndTurn()
		{
			// EndTurn has two phases separated by the _currentPlayer wrap-around check:
			//
			// Phase A — per-player advance (runs every call):
			//   Sweep destroyed players, advance _currentPlayer. If it hasn't wrapped
			//   yet, queue unit/city/player turns for the new current player and return.
			//
			// Phase B — global tick (runs once per full round, when _currentPlayer wraps
			//   back to 0):
			//   Global warming → Leonardo upgrade → story-arc events (Apollo intel,
			//   SETI signal, Tau Ceti approach, probe reports, Olvir arrival) →
			//   victory checks (dome, spaceship, 2100 AD score) → autosave →
			//   disasters + barbarian spawns.
			//   Victory checks in Phase B return early if the game ends, so conquest
			//   (last-AI-destroyed) is checked after the wrap-around guard as well.
			_waitingUnits.Clear();
			_activeUnitExplicit = false;
			foreach (Player player in _players.Where(x => !(x.Civilization is Barbarian)))
			{
				player.IsDestroyed();
			}

			if (++_currentPlayer >= _players.Count)
			{
				_currentPlayer = 0;
				HandleGlobalWarming();
				GameTurn++;
				RecordScoreSnapshot();

				// Leonardo's Workshop: one free unit upgrade per owner per turn
				if (!WonderObsolete<LeonardosWorkshop>())
					foreach (Player lp in _players.Where(p => p != null && !p.IsDestroyed() && p.HasWonder<LeonardosWorkshop>()))
						ApplyLeonardoUpgrade(lp);

				// Fire the satellite-anomaly intelligence report once Apollo is built
				if (!MapRevealedNotified && WonderBuilt<ApolloProgram>())
				{
					MapRevealedNotified = true;
					SouthPoleExpeditionLog.EnsureConfigFile();
					string gameYear = GameYear;
					RecordTransmission("SouthPoleIntel", gameYear);
					GameTask.Enqueue(Show.Screen(new SouthPoleIntelReport(gameYear)));
				}

				// SETI is a world-wide program, not a wonder: once five Observatories
				// exist anywhere on Earth, the Tau Ceti signal is detected five turns
				// later. Once scheduled it stays scheduled, even if observatories are
				// later lost — the transmission is already en route.
				if (!SETISignalReceived && SETISignalTurn == 0 &&
					_cities.Count(c => c.HasBuilding<Observatory>()) >= 5)
				{
					SETISignalTurn = (uint)(_gameTurn + 5);
				}

				// Fire the SETI signal transmission five turns after the fifth
				// Observatory was completed world-wide
				if (SETISignalTurn > 0 && _gameTurn >= SETISignalTurn)
				{
					SETISignalTurn = 0;
					SETISignalReceived = true;
					if (VisitorType == VisitorArchetype.None)
						VisitorType = VisitorOverride() ?? SelectVisitorArchetype(); // CIVONE_VISITOR overrides the quality-weighted draw
					TauCetiEscalationTurn = (uint)(_gameTurn + 20);
					SETISignalTransmission.EnsureConfigFile();
					string gameDate = GameYear;
					RecordTransmission("SETISignal", gameDate);
					GameTask.Enqueue(Show.Screen(new SETISignalTransmission(gameDate)));
				}

				// Fire the Tau Ceti approach warning 20 turns after the SETI signal
				if (TauCetiEscalationTurn > 0 && _gameTurn >= TauCetiEscalationTurn)
				{
					TauCetiEscalationTurn = 0;
					AssignDomeComponents();
					OlvirArrivalTurn = (uint)(_gameTurn + 80);
					string gameDate = GameYear;
					RecordTransmission("TauCetiApproach", gameDate);
					GameTask.Enqueue(Show.Screen(new TauCetiApproachWarning(gameDate, VisitorType, ProbeDispatched, ProbeInterimPhase)));
					var humanDomeComponents = GetDomeAssignments(HumanPlayer).ToList();
					if (humanDomeComponents.Count > 0)
					{
						string nameList = string.Join(" + ", humanDomeComponents
							.Select(w => DomeFiveComponents.First(c => (Enums.Wonder)c.Id == w).Name.ToUpper()));
						GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
							"Science brief: our role",
							"in the Dome project is",
							$"to build: {nameList}.",
							"Open World menu to track."));
					}
				}

				// Probe interim reports and final result
				if (ProbeDispatchTurn > 0 && ProbeInterimPhase < 4)
				{
					uint[] interimTurns = { ProbeDispatchTurn + 8, ProbeDispatchTurn + 18, ProbeDispatchTurn + 28 };
					uint resultTurn = ProbeDispatchTurn + 35;

					if (ProbeInterimPhase < 3 && _gameTurn >= interimTurns[ProbeInterimPhase])
					{
						int phase = ++ProbeInterimPhase;
						string gameDate = GameYear;
						RecordTransmission($"ProbeInterim{phase}", gameDate);
						// Text before art: the report builds to "artificial origin probable",
						// then the picture confirms it. Swapped from original art-first order.
						GameTask.Enqueue(Show.Screen(new Screens.ProbeInterimTransmission(gameDate, phase)));
						if (phase == 3)
							GameTask.Enqueue(Show.Screen(new EventArtScreen(
								EventArtScreen.FindPath("OlvirInSpace")!, "VISUAL CONTACT — TAU CETI")));
					}
					else if (ProbeInterimPhase == 3 && _gameTurn >= resultTurn)
					{
						ProbeInterimPhase = 4;
						string gameDate = GameYear;
						int tier = ProbeOutcomeTier;
						string[] techNames = System.Array.Empty<string>();
						if (ProbeGrantedAdvanceIds.Length > 0)
						{
							var grants = ProbeGrantedAdvanceIds
								.Select(id => HumanPlayer.Advances.Concat(HumanPlayer.AvailableResearch)
									.FirstOrDefault(a => a.Id == id))
								.Where(a => a is not null)
								.ToArray();
							techNames = grants.Select(a => (a as ICivilopedia)?.Name ?? "").ToArray();
							foreach (var adv in grants)
								if (!HumanPlayer.HasAdvance(adv))
									HumanPlayer.AddAdvance(adv);
							ProbeGrantedAdvanceIds = System.Array.Empty<int>();
						}
						if (tier >= 3)
							HumanPlayer.AwardMilestone(tier >= 4 ? 100 : 50);
						RecordTransmission("ProbeResult", gameDate);
						GameTask.Enqueue(Show.Screen(new Screens.ProbeResultTransmission(gameDate, VisitorType, tier, techNames)));
					}
				}

				// Visitor arrival scene
				if (OlvirArrivalTurn > 0 && _gameTurn >= OlvirArrivalTurn)
				{
					OlvirArrivalTurn = 0;
					VisitorsArrived = true; // first contact — unlocks the post-contact tech tree

					// The Owners ("The Others") arrive to reclaim humanity — a cinematic ending.
					// A defended world (dome complete) becomes a disputed claim and humanity
					// endures as a peer; an undefended one is recovered outright. Game over either way.
					if (VisitorType == VisitorArchetype.Owners)
					{
						ArriveOwners();
						return;
					}

					// Refugees (Olvir) — and, until their own arcs are built, the other archetypes
					// fall through to the peaceful-settlement path.
					bool probeWasSent = (ProbeInterimPhase == 4);
					string artCaption = probeWasSent
						? VisitorType switch
						{
							VisitorArchetype.Conquerors => "FIRST CONTACT — ULTIMATUM",
							VisitorArchetype.Owners     => "FIRST CONTACT — DISPUTED TERRITORY",
							VisitorArchetype.Evaluators => "FIRST CONTACT — EVALUATION",
							_                           => "FIRST CONTACT",
						}
						: "UNANNOUNCED CONTACT";
					SpawnOlvir();
					string gameDate = GameYear;
					string landfallYear = Common.YearString((ushort)Math.Min(_gameTurn + 30, ushort.MaxValue));
					RecordTransmission("OlvirArrival", gameDate);
					GameTask.Enqueue(Show.Screen(new EventArtScreen(
						EventArtScreen.FindPath("MeetTheOlvir")!, artCaption)));
					GameTask.Enqueue(Show.Screen(new Screens.OlvirArrivalTransmission(gameDate, VisitorType, probeWasSent, landfallYear)));
				}

				// Olvir proximity alarm: once the visitors are on the ground and expanding,
				// periodically warn the human when an Olvir city is within 6 tiles of one
				// of their own cities — the "naive colonisation" pressure the player should
				// feel. Rate-limited to once every 15 turns to avoid advisor spam.
				if (VisitorsArrived && VisitorType == VisitorArchetype.Refugees
				    && (_gameTurn - OlvirProximityAlarmTurn) >= 15)
				{
					Player? olvir = _players.FirstOrDefault(p => p.Civilization is Civilizations.Olvir);
					if (olvir is not null && olvir.Cities.Length > 0)
					{
						City? encroaching = olvir.Cities
							.FirstOrDefault(oc => HumanPlayer.Cities
								.Any(hc => Common.DistanceToTile(oc.X, oc.Y, hc.X, hc.Y) <= 6));
						if (encroaching is not null)
						{
							OlvirProximityAlarmTurn = (uint)_gameTurn;
							GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
								"Citizens are alarmed:",
								$"Olvir city {encroaching.Name}",
								"is settling near our borders.",
								"Unrest is rising."));
						}
					}
				}

				// Check for dome victory (all five components built)
				if (!_domeVictoryFired && DomeFiveComponents.All(w => WonderBuilt(w)))
				{
					_domeVictoryFired = true;
					HumanPlayer.AwardMilestone(150);
					string gameDate = GameYear;
					RecordTransmission("DomeComplete", gameDate);
					var doneScreen = new Screens.DomeCompleteTransmission(gameDate, VisitorType);
					GameTask.Enqueue(Show.Screen(doneScreen));

					if (!SETISignalReceived)
					{
						// Classic game (no alien arc): the Dome is the end.
						DecisionLogger.EndGame(HumanPlayer.Score, "Dome", humanWon: true, turns: _gameTurn);
						int domeFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Dome Victory");
						GameTask domeFt;
						GameTask.Enqueue(domeFt = Show.Screen(new Screens.Reports.FinalScore("Dome Victory")));
						domeFt.Done += (s, a) => EndSequence.ChainAfterFinal(domeFame, () => Runtime.Quit());
					}
					else
					{
						// SETI arc: the Dome is a milestone, not the end. The game continues
						// until 2200 AD (Coexistence ending). The Olvir still approach; their
						// arrival and the final score fire on schedule.
						GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
							"The Dome is operational.",
							OlvirArrivalTurn > 0
								? $"Olvir landfall est. {Common.YearString((ushort)OlvirArrivalTurn)}."
								: "Await the visitors.",
							"Continue to 2200 AD."));
					}
				}

				// Check for spaceship launches (AI players only — human launches manually via SpaceShips screen)
				for (int p = 1; p < _players.Count; p++)
				{
					if (_players[p].IsDestroyed()) continue;
					if (_players[p] == HumanPlayer) continue;
					int structural = SpaceshipStructural[p];
					int component  = SpaceshipComponent[p];
					int module     = SpaceshipModule[p];
					// Minimum: 1 engine (2 comps), 1 module set (3 mods), sufficient structure
					int needed = SpaceshipStructuresNeeded(component, module);
					if (component < 2 || module < 3 || structural < needed) continue;
					if (SpaceshipLaunchTurn[p] != 0) continue;

					SpaceshipLaunchTurn[p] = _gameTurn;
					SpaceshipArrivalTurn[p] = _gameTurn + SpaceshipTravelTurns(structural, component, module);
					ClearSpaceShipProduction(p);
					string eta = Common.YearString((ushort)SpaceshipArrivalTurn[p]);
					GameTask.Enqueue(Message.Advisor(Advisor.Foreign, false,
						$"The {_players[p].TribeNamePlural}",
						"have launched a spaceship!",
						$"Arrival: {eta}"));
				}

				// Check for spaceship arrivals.
				// If the Olvir/post-probe storyline is active the spaceship is a milestone,
				// not a game-ender — show the event and continue.
				int bestArrival = int.MaxValue;
				for (int p = 1; p < _players.Count; p++)
					if (SpaceshipArrivalTurn[p] > 0 && SpaceshipArrivalTurn[p] < bestArrival)
						bestArrival = SpaceshipArrivalTurn[p];

				if (bestArrival <= _gameTurn)
				{
					bool humanWins = SpaceshipArrivalTurn[PlayerNumber(HumanPlayer)] == bestArrival;

					if (SETISignalReceived)
					{
						// Story arc active: acknowledge the arrival but keep playing.
						if (humanWins)
						{
							HumanPlayer.AwardMilestone(100);
							string acCaption = VisitorType == VisitorArchetype.Refugees
								? "Colony established — Alpha Centauri II. The Olvir know this star."
								: "Colony established — Alpha Centauri II. Humanity is no longer bound to Earth.";
							GameTask.Enqueue(Show.EventArt("spaceshiparrived", acCaption));
							GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
								"Alpha Centauri II: colonised.",
								DomeComplete
									? "The Dome guards Earth."
									: "The Dome must still be built.",
								"Continue to 2200 AD."));
							SpaceshipArrivalTurn[PlayerNumber(HumanPlayer)] = 0;
						}
						else
						{
							for (int p = 1; p < _players.Count; p++)
							{
								if (SpaceshipArrivalTurn[p] != bestArrival) continue;
								GameTask.Enqueue(Message.Newspaper(null!, $"The {_players[p].TribeNamePlural}", "have reached", "Alpha Centauri!"));
								SpaceshipArrivalTurn[p] = 0;
								break;
							}
						}
					}
					else
					{
						if (humanWins)
						{
							DecisionLogger.EndGame(HumanPlayer.Score, "Space Race", humanWon: true, turns: _gameTurn);
							int spaceFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Space Race Victory");
							GameTask.Enqueue(Show.EventArt("spaceshiparrived", $"Spaceship reaches Alpha Centauri! Score: {HumanPlayer.Score}"));
							GameTask spaceFt;
							GameTask.Enqueue(spaceFt = Show.Screen(new FinalScore("Space Race Victory")));
							spaceFt.Done += (s, a) => EndSequence.ChainAfterFinal(spaceFame, () => Runtime.Quit());
						}
						else
						{
							for (int p = 1; p < _players.Count; p++)
							{
								if (SpaceshipArrivalTurn[p] != bestArrival) continue;
								GameTask.Enqueue(Message.Newspaper(null!, $"The {_players[p].TribeNamePlural}", "have reached", "Alpha Centauri!"));
								break;
							}
							DecisionLogger.EndGame(HumanPlayer.Score, "Space Race", humanWon: false, turns: _gameTurn);
							GameTask.Enqueue(Turn.GameOver(HumanPlayer));
						}
						return;
					}
				}

				// 2100 AD: game ends by score — waived if the SETI storyline is active,
				// since the alien contact arc has its own endings (dome, probe result).
				if (Common.TurnToYear(_gameTurn) >= 2100 && !SETISignalReceived)
				{
					Player winner = _players
						.Where(p => !(p.Civilization is Barbarian) && !p.IsDestroyed())
						.OrderByDescending(p => p.Score)
						.ThenBy(p => p == HumanPlayer ? 0 : 1)
						.FirstOrDefault();

					if (winner == HumanPlayer)
					{
						DecisionLogger.EndGame(HumanPlayer.Score, "Score", humanWon: true, turns: _gameTurn);
						int scoreFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Score Victory");
						GameTask.Enqueue(Message.Newspaper(null!, "The year is 2100!", $"Your score: {HumanPlayer.Score}", "You lead the world!"));
						GameTask scoreFt;
						GameTask.Enqueue(scoreFt = Show.Screen(new FinalScore("Score Victory")));
						scoreFt.Done += (s, a) => EndSequence.ChainAfterFinal(scoreFame, () => Runtime.Quit());
					}
					else
					{
						DecisionLogger.EndGame(HumanPlayer.Score, "Score", humanWon: false, turns: _gameTurn);
						GameTask.Enqueue(Turn.GameOver(HumanPlayer));
					}
					return;
				}

				// 2200 AD: post-contact backstop ending. While the alien-contact arc is active
				// (SETISignalReceived) the 2100 score ending above is waived, and the only built-in
				// finish is the Dome victory — which may never come if the Olvir struggle or land
				// somewhere the player can't reach. Without a backstop such a game runs forever.
				// From 2200 AD, close it out with a "Coexistence" ending that scores the peaceful,
				// multi-species Earth the player built and held together.
				if (Common.TurnToYear(_gameTurn) >= 2200 && SETISignalReceived)
				{
					Player? olvir = _players.FirstOrDefault(p => p.Civilization is Civilizations.Olvir);
					int olvirCities = olvir?.Cities.Length ?? 0;
					int coexistence = 100                                                    // reached a peaceful end at all
					                + (VisitorType == VisitorArchetype.Refugees ? 100 : 0)   // they came in peace, and stayed that way
					                + olvirCities * 25                                       // every surviving Olvir city is a home made on Earth
					                + OlvirImprovements.Count * 5;                            // and a shared world reshaped together
					HumanPlayer.AwardMilestone(coexistence);

					DecisionLogger.EndGame(HumanPlayer.Score, "Coexistence", humanWon: true, turns: _gameTurn);
					int coexFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Coexistence");
					GameTask.Enqueue(Message.Newspaper(null!, "The year is 2200.",
						olvirCities > 0 ? "Two species share one Earth." : "Earth endures, alone.",
						$"Your score: {HumanPlayer.Score}"));
					GameTask coexFt;
					GameTask.Enqueue(coexFt = Show.Screen(new FinalScore("Coexistence")));
					coexFt.Done += (s, a) => EndSequence.ChainAfterFinal(coexFame, () => Runtime.Quit());
					return;
				}

				PerformAutoSave();

				IEnumerable<City> disasterCities = _cities.OrderBy(o => Common.Random.Next(0,1000)).Take(2).AsEnumerable();
				foreach (City city in disasterCities)
					city.Disaster();

				// Hurricanes/typhoons: every coastal-or-floating city in the tropical or arid
				// bands rolls independently each turn, plus inland cities one tile from the ocean.
				// The Hurricane check self-gates by latitude/coast/probability; cheap to call on all.
				// WarmingIndicator scans the whole map, so compute it once for all cities.
				int hurricaneWarming = WarmingIndicator;
				foreach (City city in _cities)
					city.HurricaneCheck(hurricaneWarming);

				if (Barbarian.IsSeaSpawnTurn)
				{
					ITile? tile = Barbarian.SeaSpawnPosition;
					if (tile is not null)
					{
						foreach (UnitType unitType in Barbarian.SeaSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}

				if (Barbarian.IsLandSpawnTurn)
				{
					ITile? tile = Barbarian.LandSpawnPosition;
					if (tile is not null)
					{
						foreach (UnitType unitType in Barbarian.LandSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}
			}

			if (!_conquestVictoryFired && !_players.Any(x => Game.PlayerNumber(x) != 0 && x != Human && !x.IsDestroyed()))
			{
				_conquestVictoryFired = true;
				DecisionLogger.EndGame(HumanPlayer.Score, "Conquest", humanWon: true, turns: _gameTurn);
				int conquestFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Conquest Victory");
				GameTask conquest;
				GameTask.Enqueue(Message.Newspaper(null!, "Your civilization", "has conquered", "the entire planet!"));
				GameTask.Enqueue(conquest = Show.Screen<Conquest>());
				conquest.Done += (s, a) =>
				{
					var final = new FinalScore("Conquest Victory");
					final.Closed += (s2, a2) => EndSequence.ChainAfterFinal(conquestFame, () => Runtime.Quit());
					Common.AddScreen(final);
				};
			}

			foreach (IUnit unit in _units.Where(u => u.Owner == _currentPlayer))
			{
				GameTask.Enqueue(Turn.New(unit));
			}
			foreach (City city in _cities.Where(c => c.Owner == _currentPlayer).ToArray())
			{
				GameTask.Enqueue(Turn.New(city));
			}
			GameTask.Enqueue(Turn.New(CurrentPlayer));

			if (CurrentPlayer != HumanPlayer) return;
			
			if (Game.InstantAdvice && (Common.TurnToYear(Game.GameTurn) == -3600 || Common.TurnToYear(Game.GameTurn) == -2800))
				GameTask.Enqueue(Message.Help("--- Civilization Note ---", TextFile.Instance.GetGameText("HELP/HELP1")));
			else if (Game.InstantAdvice && (Common.TurnToYear(Game.GameTurn) == -3200 || Common.TurnToYear(Game.GameTurn) == -2400))
				GameTask.Enqueue(Message.Help("--- Civilization Note ---", TextFile.Instance.GetGameText("HELP/HELP2")));
		}
		
		public void Update()
		{
			IUnit? unit = ActiveUnit;
			// In Autopilot we want the human's units handled by the AI just like a regular
			// non-human player — fall through to the Turn.Move(unit) / Turn.End() branch
			// below instead of the human-only GoTo path.
			if (CurrentPlayer == HumanPlayer && !Settings.Instance.Autopilot)
			{
				if (unit is not null && !unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null)
					{
						unit.Goto = Point.Empty;
						return;
					}
					// Don't let a GoTo move initiate war — stop peacefully at the border.
					Player owner = HumanPlayer;
					Player? nextCityOwner = (next.City is not null && next.City.Owner != unit.Owner) ? GetPlayer(next.City.Owner) : null;
					bool peacefulBlock =
						next.Units.Any(u => { if (u.Owner == unit.Owner) return false; Player p = GetPlayer(u.Owner); return p is not null && u.Owner != 0 && !owner.IsAtWar(p); })
						|| (nextCityOwner is not null && nextCityOwner != GetPlayer(0) && !owner.IsAtWar(nextCityOwner));
					if (peacefulBlock)
					{
						unit.Goto = Point.Empty;
						return;
					}
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
						unit.Goto = Point.Empty;
					return;
				}
				return;
			}
			if (unit is not null && (unit.MovesLeft > 0 || unit.PartMoves > 0))
			{
				if (unit == _lastMovedUnit)
				{
					_sameUnitMoveCount++;
					if (_sameUnitMoveCount % 20 == 0)
					{
						string gotoStr = unit.Goto.IsEmpty ? "empty" : $"({unit.Goto.X},{unit.Goto.Y})";
						Log($"[AI] {unit.GetType().Name} P{unit.Owner} ({unit.X},{unit.Y}) queued {_sameUnitMoveCount}x; MovesLeft={unit.MovesLeft} PartMoves={unit.PartMoves} Moving={unit.Moving} Goto={gotoStr}");
					}

					// Circuit breaker: same unit queued > 50 times without making any progress
					// (position unchanged, moves not consumed) almost always means AI.Move is
					// either throwing silently (caught by the GameTask handler) or pathfinding
					// is looping. Force-skip the unit so the rest of the turn can advance.
					if (_sameUnitMoveCount > 50)
					{
						Log($"[AI] CIRCUIT BREAKER: force-skipping {unit.GetType().Name} P{unit.Owner} ({unit.X},{unit.Y}) after {_sameUnitMoveCount} stuck queues");
						LogCircuitBreakerDiagnostics(unit);
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						_sameUnitMoveCount = 0;
						_lastMovedUnit = null;
						return;
					}
				}
				else
				{
					_sameUnitMoveCount = 1;
					_lastMovedUnit = unit;
				}
				GameTask.Enqueue(Turn.Move(unit));
				return;
			}
			_sameUnitMoveCount = 0;
			_lastMovedUnit = null;
			Log($"[AI] P{_currentPlayer} ({CurrentPlayer.LeaderName}) ending turn");
			GameTask.Enqueue(Turn.End());
		}

		internal int CityNameId(Player player)
		{
			ICivilization civilization = player.Civilization;
			ICivilization[] civilizations = Common.Civilizations;
			int startIndex = Enumerable.Range(1, civilization.Id - 1).Sum(i => civilizations[i].CityNames.Length);
			int spareIndex = Enumerable.Range(1, Common.Civilizations.Length - 1).Sum(i => civilizations[i].CityNames.Length);
			int[] used = _cities.Select(c => c.NameId).ToArray();
			int[] available = Enumerable.Range(0, CityNames.Length)
				.Where(i => !used.Contains(i))
				.Where(i => civilization is Civilizations.Olvir || i < spareIndex)
				.OrderBy(i => (i >= startIndex && i < startIndex + civilization.CityNames.Length) ? 0 : 1)
				.ThenBy(i => i)
				.ToArray();
			if (player.CityNamesSkipped >= available.Length)
				return 0;
			return available[player.CityNamesSkipped];
		}

		internal City? AddCity(Player player, int nameId, int x, int y)
		{
			if (_cities.Any(c => c.X == x && c.Y == y))
				return null;

			byte ownerNum = PlayerNumber(player);
			City city = new City(ownerNum)
			{
				X = x,
				Y = y,
				NameId = nameId,
				OriginalOwner = ownerNum,
				Size = 1
			};
			if (!_cities.Any(c => c.Size > 0 && c.Owner == city.Owner))
			{
				Palace palace = new Palace();
				palace.SetFree();
				city.AddBuilding(palace);
			}
			if (!Map[x, y].IsOcean)
			{
				// A city founded on Forest/Jungle/Swamp sits on a 1-food centre tile (the
				// city-centre floor only guarantees 1 food, and AddCity's irrigation below
				// skips these terrains). A human clears the forest first; the AI doesn't know
				// to, so its towns starve. Clear it for non-human civs — same Forest/Jungle/
				// Swamp → Grassland conversion the Settler's "change to plains" action performs.
				// Olvir excluded: refugees have their own jungle-tolerant placement (SpawnOlvir).
				if (!player.IsHuman && !(player.Civilization is Civilizations.Olvir)
				    && (Map[x, y] is Forest || Map[x, y] is Jungle || Map[x, y] is Swamp))
				{
					Map[x, y].Irrigation = false;
					Map[x, y].Mine = false;
					Map.ChangeTileType(x, y, Terrain.Grassland1);
				}
				if ((Map[x, y] is Desert) || (Map[x, y] is Grassland) || (Map[x, y] is Hills) || (Map[x, y] is Plains) || (Map[x, y] is River))
					Map[x, y].Irrigation = true;
				if (!Map[x, y].RailRoad)
					Map[x, y].Road = true;
			}
			_cities.Add(city);
			Game.UpdateResources(city.Tile);
			if (Game.Started)
				_replayData.Add(new ReplayData.CityBuilt(_gameTurn, city.Owner, _cities.Count - 1, nameId, x, y));
			return city;
		}

		// Invalidate the food/shield/trade cache for every city that currently works the
		// tile at (x, y). Call this after any tile mutation (irrigation, railroad, pollution
		// removal, terrain conversion) so cached FoodRaw/ShieldRaw values stay accurate.
		internal static void InvalidateCitiesAt(int x, int y)
		{
			if (_instance is null) return;
			foreach (City c in _instance._cities)
			{
				if (c.ResourceTiles.Any(t => t.X == x && t.Y == y))
					c.InvalidateCache();
			}
		}

		public void DestroyCity(City city)
		{
			int cityIdx = _cities.IndexOf(city);
			_replayData.Add(new ReplayData.CityDestroyed(_gameTurn, cityIdx, city.NameId, city.X, city.Y, city.Owner));
			foreach (IUnit unit in _units.Where(u => u.Home == city).ToArray())
			{
				unit.SetHome(null);
				_units.Remove(unit);
			}
			_cities.Remove(city);
			city.X = 255;
			city.Y = 255;
			city.Owner = 0;
		}
		
		internal City? GetCity(int x, int y)
		{
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x-= Map.WIDTH;
			if (y < 0) return null;
			if (y >= Map.HEIGHT) return null;
			return _cities.Where(c => c.X == x && c.Y == y && c.Size > 0).FirstOrDefault();
		}
		
		internal static IUnit? PeekUnit(UnitType type) => CreateUnit(type, 0, 0);

		private static IUnit? CreateUnit(UnitType type, int x, int y)
		{
			IUnit unit;
			switch (type)
			{
				case UnitType.Settlers: unit = new Settlers(); break; 
				case UnitType.Militia: unit = new Militia(); break;
				case UnitType.Phalanx: unit = new Phalanx(); break;
				case UnitType.Legion: unit = new Legion(); break;
				case UnitType.Musketeers: unit = new Musketeers(); break;
				case UnitType.Riflemen: unit = new Riflemen(); break;
				case UnitType.Cavalry: unit = new Cavalry(); break;
				case UnitType.Knights: unit = new Knights(); break;
				case UnitType.Catapult: unit = new Catapult(); break;
				case UnitType.Cannon: unit = new Cannon(); break;
				case UnitType.Chariot: unit = new Chariot(); break;
				case UnitType.Armor: unit = new Armor(); break;
				case UnitType.MechInf: unit = new MechInf(); break;
				case UnitType.Artillery: unit = new Artillery(); break;
				case UnitType.Fighter: unit = new Fighter(); break;
				case UnitType.Bomber: unit = new Bomber(); break;
				case UnitType.Trireme: unit = new Trireme(); break;
				case UnitType.Sail: unit = new Sail(); break;
				case UnitType.Frigate: unit = new Frigate(); break;
				case UnitType.Ironclad: unit = new Ironclad(); break;
				case UnitType.Cruiser: unit = new Cruiser(); break;
				case UnitType.Battleship: unit = new Battleship(); break;
				case UnitType.Submarine: unit = new Submarine(); break;
				case UnitType.Carrier: unit = new Carrier(); break;
				case UnitType.Transport: unit = new Transport(); break;
				case UnitType.Nuclear: unit = new Nuclear(); break;
				case UnitType.Diplomat: unit = new Diplomat(); break;
				case UnitType.Caravan: unit = new Caravan(); break;
				case UnitType.Explorer: unit = new Explorer(); break;
				case UnitType.HydroEngineer: unit = new HydroEngineer(); break;
				case UnitType.SeaCaravan: unit = new SeaCaravan(); break;
				case UnitType.HoverTank: unit = new HoverTank(); break;
				case UnitType.FusionInf: unit = new FusionInf(); break;
				default: return null;
			}
			unit.X = x;
			unit.Y = y;
			unit.MovesLeft = unit.Move;
			return unit;
		}

		public IUnit? CreateUnit(UnitType type, int x, int y, byte owner, bool endTurn = false)
		{
			IUnit? unit = CreateUnit((UnitType)type, x, y);
			if (unit is null) return null;

			unit.Owner = owner;
			if (unit.Class == UnitClass.Water)
			{
				Player player = GetPlayer(owner);
				if ((player.HasWonder<Lighthouse>() && !WonderObsolete<Lighthouse>()) ||
					(player.HasWonder<MagellansExpedition>() && !WonderObsolete<MagellansExpedition>()))
				{
					unit.MovesLeft++;
				}
			}
			if (endTurn)
				unit.SkipTurn();
			_instance!._units.Add(unit);
			return unit;
		}
		
		internal IUnit[]? GetUnits(int x, int y)
		{
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x-= Map.WIDTH;
			if (y < 0) return null;
			if (y >= Map.HEIGHT) return null;
			// Use the raw index field, not the ActiveUnit property, to avoid the
			// circular: ActiveUnit → IsAboard → tile.Units → GetUnits → ActiveUnit
			IUnit? cur = (_activeUnit >= 0 && _activeUnit < _units.Count) ? _units[_activeUnit] : null;
			return _units.Where(u => u.X == x && u.Y == y).OrderBy(u => (u == cur) ? 0 : (u.Fortify || u.FortifyActive ? 1 : 2)).ToArray();
		}

		internal IUnit[] GetUnits() => _units.ToArray();

		internal void UpdateResources(ITile tile, bool ownerCities = true)
		{
			for (int relY = -3; relY <= 3; relY++)
			for (int relX = -3; relX <= 3; relX++)
			{
				if (tile[relX, relY] is null) continue;
				City city = tile[relX, relY].City;
				if (city is null) continue;
				if (!ownerCities && CurrentPlayer == city.Owner) continue;
				city.UpdateResources();
			}
		}

		public City[] GetCities() => _cities.ToArray();

		// True when the tile is currently worked by a city belonging to a different owner.
		public bool IsWorkedByOther(int x, int y, byte owner) =>
			_cities.Any(c => c.Owner != owner &&
			                 !(c.X == x && c.Y == y) &&
			                 c.ResourceTiles.Any(t => t.X == x && t.Y == y));

		// Returns the Player whose city works (x,y) for someone other than `owner`, or null.
		public Player GetWorkerOfTile(int x, int y, byte owner) =>
			_cities
				.Where(c => c.Owner != owner && !(c.X == x && c.Y == y) && c.ResourceTiles.Any(t => t.X == x && t.Y == y))
				.Select(c => GetPlayer(c.Owner))
				.FirstOrDefault();

		public IWonder[] BuiltWonders => _cities.SelectMany(c => c.Wonders).ToArray();

		public bool WonderBuilt<T>() where T : IWonder => BuiltWonders.Any(w => w is T);

		public bool WonderBuilt(IWonder wonder) => BuiltWonders.Any(w => w.Id == wonder.Id);

		// Wonder effects must check BOTH HasWonder<T>() AND !WonderObsolete<T>().
		// HasWonder alone is insufficient: the wonder object remains in the city after
		// its ObsoleteTech is researched by any player. WonderObsolete fires as soon as
		// any player — not just the owner — discovers the obsoleting advance.
		public bool WonderObsolete<T>() where T : IWonder, new() => WonderObsolete(new T());

		public bool WonderObsolete(IWonder wonder) => (wonder.ObsoleteTech is not null && _players.Any(x => x.HasAdvance(wonder.ObsoleteTech)));

		// Calculates probe mission quality (0-100) from the human player's civilisation state.
		// Four equal-weight dimensions: science depth, happiness, cultural coverage, pollution.
		internal static int CalcProbeQuality(Player player)
		{
			City[] cities = player.Cities;
			if (cities.Length == 0) return 0;

			// Science (0-25): advance count, capping at 60 (full tree is ~88, 60 covers
			// late-game depth well enough).
			int scienceScore = Math.Min(25, player.Advances.Length * 25 / 60);

			// Happiness (0-25): fraction of citizens who are happy or content.
			int totalPop  = cities.Sum(c => c.Size);
			int happyPop  = cities.Sum(c => c.HappyCitizens + c.ContentCitizens);
			int happyScore = totalPop > 0 ? happyPop * 25 / totalPop : 0;

			// Culture (0-25): fraction of cities with both a Temple and a Library.
			int cultured      = cities.Count(c => c.HasBuilding<Temple>() && c.HasBuilding<Library>());
			int cultureScore  = cities.Length > 0 ? cultured * 25 / cities.Length : 0;

			// Clean (0-25): each pollution tile subtracts 3; floor at 0.
			int cleanScore = Math.Max(0, 25 - player.Pollution * 3);

			return scienceScore + happyScore + cultureScore + cleanScore;
		}

		// Maps quality + archetype to outcome tier 0-4.
		internal static int CalcProbeOutcomeTier(int quality, VisitorArchetype archetype)
		{
			int bonus = archetype == VisitorArchetype.Refugees   ?  15
			          : archetype == VisitorArchetype.Evaluators ?   5
			          : archetype == VisitorArchetype.Owners     ? -10
			          : archetype == VisitorArchetype.Conquerors ? -20
			          : 0;
			int adj = quality + bonus;
			if (adj < 20) return 0;
			if (adj < 40) return 1;
			if (adj < 60) return 2;
			if (adj < 80) return 3;
			return 4;
		}

		// Assign dome components to eligible civs (round-robin, shuffled).
		// Excludes Barbarians and Olvir. If fewer than 5 eligible civs, some get multiple.
		// Called once when the Tau Ceti approach warning fires.
		private void AssignDomeComponents()
		{
			if (DomeAssignments.Count > 0) return; // already assigned

			Player[] eligible = _players
				.Where(p => !p.IsDestroyed()
				         && !(p.Civilization is Civilizations.Barbarian)
				         && !(p.Civilization is Civilizations.Olvir))
				.OrderByDescending(p => p.Advances.Length)
				.ToArray();
			if (eligible.Length == 0) return;

			// Shuffle the five components so the assignment isn't always the same
			var components = _domeFiveWonderIds.ToList();
			for (int i = components.Count - 1; i > 0; i--)
			{
				int j = Common.Random.Next(i + 1);
				(components[i], components[j]) = (components[j], components[i]);
			}

			for (int i = 0; i < components.Count; i++)
			{
				byte pid = PlayerNumber(eligible[i % eligible.Length]);
				if (!DomeAssignments.TryGetValue(pid, out var list))
					DomeAssignments[pid] = list = new List<Enums.Wonder>();
				list.Add(components[i]);
			}
		}

		// Removes dome assignments held by Barbarians, Olvir, or destroyed civs,
		// then redistributes only those orphaned components. Valid assignments are preserved.
		// Called on COS load after _instance is set.
		internal void FixDomeAssignmentsIfNeeded()
		{
			if (DomeAssignments.Count == 0) return;

			var badKeys = DomeAssignments.Keys
				.Where(pid =>
				{
					if (pid >= _players.Count || _players[pid] is null) return true;
					var p = _players[pid];
					return p.IsDestroyed()
					    || p.Civilization is Civilizations.Barbarian
					    || p.Civilization is Civilizations.Olvir;
				})
				.ToList();

			if (badKeys.Count == 0) return;

			// Collect orphaned components, skipping any already built.
			var orphaned = new List<Enums.Wonder>();
			foreach (var key in badKeys)
			{
				foreach (var w in DomeAssignments[key])
				{
					var comp = DomeFiveComponents.FirstOrDefault(c => (Enums.Wonder)c.Id == w);
					if (comp is not null && !WonderBuilt(comp))
						orphaned.Add(w);
				}
				DomeAssignments.Remove(key);
			}

			if (orphaned.Count == 0) return;

			// Shuffle, then distribute to eligible civs.
			for (int i = orphaned.Count - 1; i > 0; i--)
			{
				int j = Common.Random.Next(i + 1);
				(orphaned[i], orphaned[j]) = (orphaned[j], orphaned[i]);
			}

			Player[] eligible = _players
				.Where(p => p is not null && !p.IsDestroyed()
				         && !(p.Civilization is Civilizations.Barbarian)
				         && !(p.Civilization is Civilizations.Olvir))
				.OrderByDescending(p => p.Advances.Length)
				.ToArray();

			if (eligible.Length == 0) return;

			for (int i = 0; i < orphaned.Count; i++)
			{
				byte pid = PlayerNumber(eligible[i % eligible.Length]);
				if (!DomeAssignments.TryGetValue(pid, out var list))
					DomeAssignments[pid] = list = new List<Enums.Wonder>();
				list.Add(orphaned[i]);
			}
		}

		// Returns the dome components assigned to this player (empty if none / not yet assigned).
		internal IEnumerable<Enums.Wonder> GetDomeAssignments(Player player)
		{
			byte id = PlayerNumber(player);
			return DomeAssignments.TryGetValue(id, out var list) ? list : Enumerable.Empty<Enums.Wonder>();
		}

		// ── Olvir arrival ────────────────────────────────────────────────────

		// Chebyshev distance on a horizontally-wrapping map.
		private static int TileDistance(int x1, int y1, int x2, int y2)
		{
			int dx = Math.Abs(x1 - x2);
			if (dx > Map.WIDTH / 2) dx = Map.WIDTH - dx;
			return Math.Max(dx, Math.Abs(y1 - y2));
		}

		// Test/dev override: set CIVONE_VISITOR=Owners|Refugees|Evaluators|Conquerors to force
		// which Tau Ceti archetype arrives. Dynamic, quality-based selection is still TODO.
		private static VisitorArchetype? VisitorOverride()
		{
			string? v = System.Environment.GetEnvironmentVariable("CIVONE_VISITOR");
			if (!string.IsNullOrWhiteSpace(v) && System.Enum.TryParse(v, ignoreCase: true, out VisitorArchetype a) && a != VisitorArchetype.None)
				return a;
			return null;
		}

		// Quality-weighted choice of which Tau Ceti archetype arrives, judged from the human
		// civilization's character at the moment first contact is made. A peaceful, enlightened,
		// clean civilization tilts toward the Refugees (who seek a welcoming ally); a polluted,
		// embattled, autocratic one tilts toward the Owners (whose reclamation falls hardest on a
		// careless, undefended world). The draw is only WEIGHTED, never fixed — your civilization
		// changes the odds, not the outcome, so replays still surprise. Only Refugees and Owners
		// have arcs today; Evaluators/Conquerors join the draw as their content is built.
		private VisitorArchetype SelectVisitorArchetype()
		{
			Player h = HumanPlayer;
			int score = 0; // positive = enlightened (Refugees), negative = harsh (Owners)

			// Government — the clearest read on a civilization's character.
			if (h.Government is CivOne.Governments.Democracy)      score += 3;
			else if (h.Government is CivOne.Governments.Republic)  score += 2;
			else if (h.Government is CivOne.Governments.Monarchy)  score -= 1;
			else                                                  score -= 2; // Despotism / Anarchy / Communism

			// Wars — an aggressive, embattled civ leans Owners.
			int wars = _players.Count(p => p != null && p != h && !p.IsDestroyed()
				&& !(p.Civilization is Civilizations.Barbarian) && h.IsAtWar(p));
			score -= Math.Min(wars, 3);

			// Happiness / culture — Temple coverage across the empire leans Refugees.
			if (h.Cities.Length > 0)
			{
				double temples = h.Cities.Count(c => c.HasBuilding<Temple>()) / (double)h.Cities.Length;
				if (temples >= 0.6) score += 2;
				else if (temples <= 0.2) score -= 1;
			}

			// Pollution — a smoke-choked world is loud and careless; leans Owners.
			if (h.Pollution >= 8)      score -= 2;
			else if (h.Pollution == 0) score += 1;

			// Map the character score to P(Refugees), clamped so it is never deterministic.
			double pRefugees = 0.5 + score * 0.07;
			if (pRefugees < 0.20) pRefugees = 0.20;
			if (pRefugees > 0.80) pRefugees = 0.80;

			return Common.Random.Next(100) < (int)System.Math.Round(pRefugees * 100)
				? VisitorArchetype.Refugees
				: VisitorArchetype.Owners;
		}

		// The Owners arrival is an ending, forked on whether the planetary defence dome was built.
		private void ArriveOwners()
		{
			bool domeHeld = DomeComplete;
			string gameDate = GameYear;
			RecordTransmission("OwnersArrival", gameDate);

			// Art hook: drop an Owners image (e.g. event_art/theothers.png) and show it here with
			// an EventArtScreen, exactly like the Olvir arrival. Text-only for now.
			GameTask.Enqueue(Show.Screen(new Screens.OwnersArrivalTransmission(gameDate, domeHeld)));

			if (domeHeld)
			{
				// The dome held: humanity survives as a negotiating peer, not recovered cargo.
				HumanPlayer.AwardMilestone(150);
				DecisionLogger.EndGame(HumanPlayer.Score, "Disputed Claim", humanWon: true, turns: _gameTurn);
				int fame = EndSequence.SaveAndGetIndex(HumanPlayer, "Disputed Claim");
				GameTask ft;
				GameTask.Enqueue(ft = Show.Screen(new FinalScore("Disputed Claim")));
				ft.Done += (s, a) => EndSequence.ChainAfterFinal(fame, () => Runtime.Quit());
			}
			else
			{
				// No defence stood ready: the cargo is recovered. Humanity loses.
				DecisionLogger.EndGame(HumanPlayer.Score, "Reclamation", humanWon: false, turns: _gameTurn);
				GameTask.Enqueue(Turn.GameOver(HumanPlayer));
			}
		}

		private void SpawnOlvir()
		{
			ICivilization olvirCiv = Common.Civilizations.First(c => c is Civilizations.Olvir);

			// Compute where Olvir city names start in the flat AllCityNames array.
			int nameStart = Common.Civilizations
				.Where(c => c.Id < olvirCiv.Id)
				.Sum(c => c.CityNames.Length);

			bool CityFree(int x, int y) => !_cities.Any(c => c.X == x && c.Y == y && c.Size > 0);
			// Habitable land only: never settle the Olvir on Arctic (the poles), Mountains,
			// or right up against the polar ice bands — that's how an Olvir city ended up
			// stranded on "Antarctica" amid barbarians, unreachable and doomed.
			bool IsLand(int x, int y)
			{
				ITile t = Map[x, y];
				return t is not null && !(t is Ocean) && !(t is Arctic) && !(t is Mountains)
				    && y > Map.HEIGHT / 10 && y < Map.HEIGHT - Map.HEIGHT / 10;
			}
			bool CoastalTile(ITile t) => t.GetBorderTiles().Any(b => b is Ocean);
			bool IsEquatorial(int y) => y > Map.HEIGHT / 4 && y < 3 * Map.HEIGHT / 4;

			// Spawn spread: 8 tiles between initial landing sites (was 12). The Olvir
			// are a refugee fleet that packs in wherever it can — not a civ spacing out
			// its empire. Tighter spread means more initial cities and faster coverage.
			const int MinSpread = 8;
			var chosen = new List<(int x, int y)>();

			bool FarEnough(int x, int y) =>
				chosen.All(p => TileDistance(x, y, p.x, p.y) >= MinSpread);

			// 1) One ocean city — equatorial, away from poles, far from existing civs.
			var oceans = Enumerable.Range(0, Map.WIDTH)
				.SelectMany(x => Enumerable.Range(1, Map.HEIGHT - 2).Select(y => (x, y)))
				.Where(t => Map[t.x, t.y] is Ocean && IsEquatorial(t.y) && CityFree(t.x, t.y))
				.OrderBy(_ => Common.Random.Next(10000))
				.ToList();

			(int x, int y) oceanCity = oceans.FirstOrDefault(t => FarEnough(t.x, t.y));
			if (oceanCity == default) oceanCity = oceans.FirstOrDefault();

			if (oceanCity != default)
				chosen.Add(oceanCity);

			// 2) One jungle city.
			var jungles = Enumerable.Range(0, Map.WIDTH)
				.SelectMany(x => Enumerable.Range(1, Map.HEIGHT - 2).Select(y => (x, y)))
				.Where(t => Map[t.x, t.y] is Jungle && IsLand(t.x, t.y) && CityFree(t.x, t.y))
				.OrderBy(_ => Common.Random.Next(10000))
				.ToList();

			(int x, int y) jungleCity = jungles.FirstOrDefault(t => FarEnough(t.x, t.y));
			if (jungleCity == default) jungleCity = jungles.FirstOrDefault();

			if (jungleCity != default)
				chosen.Add(jungleCity);

			// 3) Fill remaining 4 slots: prefer populated continents, then coastal land.
			// Landing 6 initial cities (up from 4) gives the Olvir enough mass to survive
			// early conflict and keeps expansion pressure on immediately.
			var populatedContinents = new HashSet<byte>(_cities.Select(c => Map[c.X, c.Y].ContinentId));
			IEnumerable<(int x, int y)> CoastalFirst() =>
				Enumerable.Range(0, Map.WIDTH)
					.SelectMany(x => Enumerable.Range(1, Map.HEIGHT - 2).Select(y => (x, y)))
					.Where(t => IsLand(t.x, t.y) && CityFree(t.x, t.y) && FarEnough(t.x, t.y)
					         && !chosen.Any(p => p.x == t.x && p.y == t.y))
					.OrderByDescending(t => populatedContinents.Contains(Map[t.x, t.y].ContinentId) ? 1 : 0)
					.ThenByDescending(t => CoastalTile(Map[t.x, t.y]) ? 1 : 0)
					.ThenBy(_ => Common.Random.Next(10000));

			foreach (var (x, y) in CoastalFirst().Take(6 - chosen.Count))
				chosen.Add((x, y));

			if (chosen.Count == 0) return; // safety

			// Create the Olvir player.
			var olvirPlayer = new Player(olvirCiv, "The Council");
			AddPlayer(olvirPlayer);
			byte owner = PlayerNumber(olvirPlayer);

			// Place cities. Each starts at size 2 with a Granary so they hit the ground
			// running: a size-1 city with no food store takes many turns to grow, blunting
			// the refugee-fleet pressure the player should feel immediately after landfall.
			for (int i = 0; i < chosen.Count; i++)
			{
				int nameId = nameStart + (i % olvirCiv.CityNames.Length);
				City? city = AddCity(olvirPlayer, nameId, chosen[i].x, chosen[i].y);
				if (city is null) continue;

				city.Size = 2;
				city.AddBuilding(new Buildings.Granary());

				OlvirImprovements[(chosen[i].x, chosen[i].y)] = Enums.OlvirImprovementType.SettlementCluster;

				// Seed surrounding tiles so the size-2 city has positive food income from
				// turn 1. Without this, ocean/jungle cities have FoodIncome < 0 and shrink
				// back to size 1 immediately.
				for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					int nx = (chosen[i].x + dx + Map.WIDTH) % Map.WIDTH;
					int ny = chosen[i].y + dy;
					if (ny <= 0 || ny >= Map.HEIGHT - 1) continue;
					if (OlvirImprovements.ContainsKey((nx, ny))) continue;
					ITile nt = Map[nx, ny];
					if (nt is Tiles.Arctic) continue;
					Enums.OlvirImprovementType nbImp = nt.IsOcean
						? Enums.OlvirImprovementType.Aquafarm
						: (nt is Tiles.Forest || nt is Tiles.Jungle)
							? Enums.OlvirImprovementType.CanopyArray
							: Enums.OlvirImprovementType.SettlementCluster;
					OlvirImprovements[(nx, ny)] = nbImp;
				}

				// Each land city gets a settler immediately for expansion.
				// Ocean cities don't — land settlers can't work ocean tiles.
				if (!Map[chosen[i].x, chosen[i].y].IsOcean)
				{
					IUnit? settler = CreateUnit(UnitType.Settlers, chosen[i].x, chosen[i].y, owner);
					if (settler is not null)
						settler.SkipTurn();
				}
			}

			// 5) Gift Xenobiology to all surviving civs — contact with the Olvir makes
			//    the advance immediately researchable through observation.
			IAdvance xenobiology = Common.Advances.FirstOrDefault(a => a is Xenobiology);
			if (xenobiology is not null)
			{
				foreach (Player p in _players.Where(p => p != null && !p.IsDestroyed() && p != olvirPlayer))
					if (!p.HasAdvance<Xenobiology>())
						p.AddAdvance(xenobiology, false);
			}
		}

		internal void PerformAutoSave()
		{
			try { SaveCos(Settings.Instance.AutoSavePath); }
			catch (Exception ex) { Log($"Autosave failed: {ex.GetType().Name}: {ex.Message}"); }
		}

		public void UpgradeUnit(IUnit unit, UnitType targetType, int cost)
		{
			if (unit is null || !_units.Contains(unit)) return;
			Player player = GetPlayer(unit.Owner);
			if (player.Gold < cost) return;

			IUnit? upgraded = CreateUnit(targetType, unit.X, unit.Y);
			if (upgraded is null) return;

			player.Gold -= (short)cost;
			upgraded.Owner   = unit.Owner;
			upgraded.Veteran = unit.Veteran;
			upgraded.SetHome(unit.Home);
			upgraded.SkipTurn();

			// Drop the old unit from its home city's support list before discarding it.
			// Without this the upgraded-away unit lingers in the city's _homeUnits as a ghost
			// and keeps charging shield upkeep (City.ShieldCosts) — every upgrade leaked one,
			// which is how a city with a single Mech. Inf. ended up paying 7 shields of upkeep.
			unit.SetHome(null);
			_units.Remove(unit);
			_units.Add(upgraded);
		}

		private void ApplyLeonardoUpgrade(Player owner)
		{
			// One free unit upgrade per turn for the wonder owner.
			(UnitType from, UnitType to, IAdvance req)[] chain =
			{
				(UnitType.Militia,    UnitType.Musketeers, new Gunpowder()),
				(UnitType.Phalanx,    UnitType.Musketeers, new Gunpowder()),
				(UnitType.Legion,     UnitType.Musketeers, new Gunpowder()),
				(UnitType.Musketeers, UnitType.Riflemen,   new Conscription()),
				(UnitType.Riflemen,   UnitType.MechInf,    new LaborUnion()),
				(UnitType.Chariot,    UnitType.Knights,    new Chivalry()),
				(UnitType.Knights,    UnitType.Cavalry,    new HorsebackRiding()),
				(UnitType.Catapult,   UnitType.Cannon,     new Metallurgy()),
				(UnitType.Cannon,     UnitType.Artillery,  new Robotics()),
			};
			byte ownerNum = (byte)PlayerNumber(owner);
			foreach (var (from, to, req) in chain)
			{
				if (!owner.HasAdvance(req)) continue;
				IUnit target = _units.FirstOrDefault(u => u.Owner == ownerNum && u.Type == from);
				if (target is null) continue;
				UpgradeUnit(target, to, 0);
				return;
			}
		}

		public void DisbandUnit(IUnit unit)
		{
			IUnit? activeUnit = ActiveUnit;

			if (unit is null) return;
			if (!_units.Contains(unit)) return;
			if (unit.Tile is Ocean && unit is IBoardable)
			{
				int totalCargo = unit.Tile.Units.Where(u => u is IBoardable).Sum(u => (u as IBoardable)!.Cargo) - (unit as IBoardable)!.Cargo;
				while (unit.Tile.Units.Count(u => u.Class != UnitClass.Water) > totalCargo)
				{
					IUnit subUnit = unit.Tile.Units.First(u => u.Class != UnitClass.Water);
					subUnit.SetHome(null);
					subUnit.X = 255;
					subUnit.Y = 255;
					_units.Remove(subUnit);
				} 
			}
			unit.SetHome(null);
			unit.X = 255;
			unit.Y = 255;
			_units.Remove(unit);

			GetPlayer(unit.Owner).IsDestroyed();

			if (activeUnit is not null && _units.Contains(activeUnit))
			{
				_activeUnit = _units.IndexOf(activeUnit);
			}
		}

		public void UnitWait()
		{
			if (_activeUnit < _units.Count)
				_waitingUnits.Add(_units[_activeUnit]);
			_activeUnit++;
		}
		
		public IUnit? ActiveUnit
		{
			get
			{
				if (!_units.Any(u => u.Owner == _currentPlayer && !u.Busy && (!IsAboard(u) || _activeUnitExplicit)))
					return null;

				if (_activeUnit >= _units.Count)
					_activeUnit = 0;

				var cur = _units[_activeUnit];

				// Fast path: current unit is still valid.
				// Respect _activeUnitExplicit to allow a player-selected cargo unit through.
				if (cur.Owner == _currentPlayer && (cur.MovesLeft > 0 || cur.PartMoves > 0) && !cur.Sentry && !cur.Fortify && !_waitingUnits.Contains(cur) && (_activeUnitExplicit || !IsAboard(cur)))
					return cur;

				// Explicit flag only survives one fast-path miss; the scanning loop picks freely.
				_activeUnitExplicit = false;

				// Task busy — hold position
				if (GameTask.Any())
					return cur;

				// No movable units left this turn (waited units don't count here)
				if (!_units.Any(u => u.Owner == _currentPlayer && (u.MovesLeft > 0 || u.PartMoves > 0) && !u.Busy && !IsAboard(u)))
				{
					if (CurrentPlayer == HumanPlayer && (!EndOfTurn || Settings.Instance.Autopilot) && !GameTask.Any() && (Common.TopScreen is GamePlay))
						GameTask.Enqueue(Turn.End());
					return null;
				}

				// Advance to the next valid unit, skipping waited and aboard units.
				// If we wrap all the way around without finding one, the player has
				// waited every remaining unit — clear the queue and pick freely.
				int startIdx = _activeUnit;
				while (true)
				{
					_activeUnit++;
					if (_activeUnit >= _units.Count) _activeUnit = 0;

					var u = _units[_activeUnit];
					if (u.Owner == _currentPlayer && (u.MovesLeft > 0 || u.PartMoves > 0) && !u.Sentry && !u.Fortify && !_waitingUnits.Contains(u) && !IsAboard(u))
						break;

					if (_activeUnit == startIdx)
					{
						// Full lap with no candidate — release the wait queue
						_waitingUnits.Clear();
						while (_units[_activeUnit].Owner != _currentPlayer || (_units[_activeUnit].MovesLeft == 0 && _units[_activeUnit].PartMoves == 0) || _units[_activeUnit].Sentry || _units[_activeUnit].Fortify || IsAboard(_units[_activeUnit]))
						{
							_activeUnit++;
							if (_activeUnit >= _units.Count) _activeUnit = 0;
						}
						break;
					}
				}
				return _units[_activeUnit];
			}
			internal set
			{
				if (value is null || value.MovesLeft == 0 && value.PartMoves == 0)
					return;
				value.Busy = false;   // clears Sentry, Fortify, and FortifyActive
				_activeUnit = _units.IndexOf(value);
				_activeUnitExplicit = IsAboard(value);
			}
		}

		public IUnit MovingUnit => _units.FirstOrDefault(u => u.Moving);

		// False during map generation and save loading; true once the Game singleton is live.
		// Guards in City, tile, and unit code that read live game state (Player, wonder checks,
		// replay log) must test this flag to avoid running against a partially-initialised world.
		public static bool Started => (_instance is not null);
		
		private static Game? _instance;
		public static Game Instance
		{
			get
			{
				if (_instance is null)
				{
					Log("ERROR: Game instance does not exist");
				}
				return _instance!;
			}
		}
	}
}
