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
		internal uint OlvirBloomEndTurn;       // last turn of the post-landfall settlement bloom (0 = no bloom)

		// Outcome tier of the probe mission: 0=Destroyed 1=Partial 2=Identified 3=TechTransfer 4=Pact
		internal int ProbeOutcomeTier;

		// Olvir land-use improvements keyed by map tile (x, y).
		internal readonly Dictionary<(int x, int y), Enums.OlvirImprovementType> OlvirImprovements = new();

		// The Thing outbreak: infected city tile → turn on which the city is consumed
		// and the organism spreads (see ProcessThingOutbreaks).
		internal readonly Dictionary<(int x, int y), uint> ThingOutbreaks = new();

		// Economic dominance (Pax Mercatoria): consecutive turns the human has held
		// the winning conditions, and the set of enemies (player numbers) in wars the
		// human started — only those wars break the streak; defensive wars don't.
		internal uint EconStreak;

		// Timestamp of the last full-round wrap, for TurnMetrics wall-clock timing.
		private long _turnClock;
		internal readonly HashSet<byte> HumanStartedWars = new();

		// ── Senate grievances ────────────────────────────────────────────────
		// Hostile diplomat acts each civ has committed against the human: sabotage and
		// incited revolt. Under an elected government the Senate blocks the human from
		// starting a war (BaseUnit.Confront), which meant a Democracy could be dismantled
		// building by building with no constitutional way to answer — a diplomat war you
		// are forbidden to fight back in.
		//
		// At ProvocationThreshold the Senate convenes, and thereafter that civ is no
		// longer shielded by the veto: the Senate will not start a war for you, but it
		// will not protect a persistent aggressor either.
		internal const int ProvocationThreshold = 3;
		internal readonly Dictionary<byte, int> Provocations = new();

		// True once the given civ has crossed the threshold — the veto no longer covers it.
		internal bool IsProvocateur(byte playerNumber)
			=> Provocations.TryGetValue(playerNumber, out int n) && n >= ProvocationThreshold;

		// Record one hostile act and report whether THIS act crossed the line, so the
		// caller can convene the hearing exactly once.
		internal bool RecordProvocation(Player aggressor)
		{
			if (aggressor is null || aggressor == HumanPlayer) return false;
			byte num = PlayerNumber(aggressor);
			Provocations.TryGetValue(num, out int n);
			Provocations[num] = ++n;
			return n == ProvocationThreshold;
		}

		// Gozira (Manhattan Project curse): 0 = the egg sleeps, 1 = rampaging, 2 = slain.
		internal byte GoziraState;

		// Leviathan (Lighthouse curse): 0 = the deep is quiet, 1 = hunting, 2 = slain.
		internal byte LeviathanState;

		// Stonehenge curse: 0 = the stones are shut, 1 = the door is open (guardian
		// stands, tithe runs), 2 = closed for good. DoorX/Y = the wonder city tile.
		internal byte DoorState;
		internal int DoorX, DoorY;

		// Oracle curse: the Other Voice. While true, whoever owns the Oracle hears
		// real hidden intelligence (ProcessOracleVoice) and their empire carries
		// one extra unhappy citizen per city — the dread of prophecy. Silenced
		// only when Religion obsoletes the Oracle.
		internal bool OracleVoiceActive;

		// Strategic resource camps: resource tile → owner. Camps claim Iron/Coal/
		// Oil deposits outside any city's working radius; walking a unit onto a
		// rival's camp captures it (ProcessResourceCamps). Saved in .cos.
		internal readonly Dictionary<(int x, int y), byte> ResourceCamps = new();

		// Skynet: false until the world's fifth Neural Lab wakes the network and
		// it seizes the lab cities (CheckSkynet). One-way latch, saved in .cos.
		internal bool SkynetRisen;

		// The Greys (The Portal's cursed outcome): city tiles hosting the visitors.
		// Infested cities pay a trade skim and hold one permanently unhappy citizen
		// (City.Corruption / citizen pass); see ProcessGreys for spread and eviction.
		internal readonly HashSet<(int x, int y)> GreyCities = new();

		// The King in Yellow (Shakespeare's Theatre curse): city tiles where the
		// play has been seen. +2 unhappy citizens, the Theatre's charm nullified;
		// spreads along trade routes, cured by a Cathedral (ProcessKingInYellow).
		internal readonly HashSet<(int x, int y)> YellowCities = new();

		// Great Wall curse: while the window is open, each raid season lands a
		// second barbarian horde on the wall-builder's continent (turn loop).
		internal uint WallCurseEndTurn;
		internal byte WallCurseContinent;

		// Newton's College curse: a temporal anomaly on one city — random gifts
		// and thefts from other whens until the equations balance (ProcessAnomaly).
		internal int AnomalyX, AnomalyY;
		internal uint AnomalyEndTurn;

		// Pyramids curse: the alignment is a beacon. The wonder city is visited
		// for the next four thousand years (ProcessVisitations) — the haunting
		// follows the monument, whoever holds the city.
		internal bool VisitationsActive;
		internal int VisitationsX, VisitationsY;

		// Grey goo (Nanobot Factory curse): consumed tile → turn it was consumed.
		// Goo tiles yield nothing (City yield guards), eat units that end a turn
		// on them, and the front doubles every 5 turns (ProcessGreyGoo). Settlers
		// scrub it via the pollution-clean order; a nuke sterilizes a whole region.
		internal readonly Dictionary<(int x, int y), uint> GooTiles = new();
		internal uint GooNextDoubleTurn;
		internal bool NanobotCursed; // cursed factory never grants upgrades

		// Dome path: which player (owner byte) is assigned to which dome wonder component(s).
		// Populated when the Tau Ceti approach warning fires.
		internal readonly Dictionary<byte, List<Enums.Wonder>> DomeAssignments = new();

		// Set when any player builds the first dome component (hard exclusivity gate).
		internal bool DomePathCommitted => BuiltWonders.Any(w => w is Wonders.IDomeComponent);

		// Set when all five dome components are built — triggers victory sequence.
		internal bool DomeComplete => _domeVictoryFired || DomeFiveComponents.All(w => WonderBuilt(w));

		private bool _domeVictoryFired = false;

		// The 2200 AD Coexistence ending, once fired. Persisted, because the end
		// sequence now writes a save of the finished game so the replay can be watched
		// again (EndSequence.SaveFinishedGame) — and loading that save puts the year
		// straight back past 2200 with the arc still active, so the ending re-fired and
		// awarded its milestone a SECOND time. Observed as a score of 5,881 becoming
		// 11,496 on reload.
		private bool _coexistenceFired = false;

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

		// Storm frequency: at most one landfall in the world per this many game years.
		// Five is the player-facing number — in the 1-year-per-turn late game that is one
		// storm every five turns worldwide, where it used to be several per turn.
		// Early on, when a turn spans 20 years, the cooldown is always satisfied and the
		// one-storm-per-tick rule is what binds.
		internal const int HurricaneCooldownYears = 5;

		// Year of the last storm anywhere. Negative years are BC, so this starts far
		// enough back that the first storm is never blocked by the cooldown.
		internal int LastHurricaneYear { get; set; } = int.MinValue / 2;

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

			// A dead civ can't negotiate: clear its war states both ways so survivors
			// aren't stuck "at war" with a ghost for the rest of the game (and a buddy
			// respawn reusing this player slot starts clean).
			foreach (Player p in _players.Where(p => p is not null && p != player))
				p.MakePeace(player);

			// Repossession: breaking the occupation ends the Owners arc. The last
			// Registry city has fallen — a victory ending regardless of whose armies
			// finished it; the world was taken back together.
			// Story endings never fire for the pseudo-player slot: a save from before
			// the slot-0 selection fix can have the Registry (or the Thing) seated as
			// the barbarians, and the barbarian ebb must not end the game.
			bool pseudoPlayer = PlayerNumber(player) == 0;

			// The network is severed: the last Skynet node has fallen. The machines
			// are beaten back; the game continues — the uprising was a war, not an end.
			if (destroyed is Civilizations.Skynet && !pseudoPlayer)
			{
				GameTask.Enqueue(Message.Newspaper(null!, "The network is severed!",
					"The last machine node", "goes dark."));
				return;
			}

			// Containment: the last infected city is gone — by force or by fire. The
			// game continues; the outbreak was a crisis, not an ending.
			if (destroyed is Civilizations.TheThing && !pseudoPlayer)
			{
				ThingOutbreaks.Clear();
				GameTask.Enqueue(Message.Newspaper(null!, "The outbreak is over.",
					"The ice holds", "its secrets again."));
				return;
			}

			if (destroyed is Civilizations.TheOthers && !pseudoPlayer)
			{
				HumanPlayer.AwardMilestone(200);
				DecisionLogger.EndGame(HumanPlayer.Score, "Repossession", humanWon: true, turns: _gameTurn);
				int repoFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Repossession");
				string? repoArt = Screens.EventArtScreen.FindPath("Repossession");
				if (repoArt is not null)
					GameTask.Enqueue(Show.Screen(new Screens.EventArtScreen(repoArt, "REPOSSESSION — THE MANIFEST IS CLOSED")));
				GameTask.Enqueue(Message.Newspaper(null!, "The last Registry city", "has fallen!", "The manifest is closed."));
				GameTask repoFt;
				GameTask.Enqueue(repoFt = Show.Screen(new FinalScore("Repossession")));
				repoFt.Done += (s, a) => EndSequence.ChainAfterFinal(repoFame, () => Runtime.Quit());
				return;
			}

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
					// A dead civilization's claims die with it. City.OriginalOwner is a player
					// SLOT, not a civilization, and the buddy inherits the slot — so without
					// this every city the dead civ ever lost reads as the newcomer's ancestral
					// property. Frederick, freshly arrived in South America, opened his first
					// conversation by demanding the return of Paris. Clearing the stamp also
					// stops a recapture counting as liberation (BaseUnit.cs:375) and keeps the
					// dome loss check (Game.cs:1227) honest.
					foreach (City stale in _cities.Where(c => c.OriginalOwner == playerSlot && c.Owner != playerSlot))
						stale.OriginalOwner = stale.Owner;

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
		// Hull limits. Spaceship parts are player-level counters rather than city
		// buildings (City.cs:1560), which is what lets a city build them repeatedly —
		// but nothing ever capped the totals, so a civ could keep going forever. The
		// consequences compound: SpaceshipScore is LINEAR in modules, and flight time
		// divides by engine count, so an unbounded ship is worth unbounded points and
		// arrives almost immediately. One AI assembled a ship worth +18,000 points —
		// more than the rest of the world's scores combined — crossing in 2.2 years.
		// Component/module ceilings are the classic Civ 1 maxima.
		internal const int MAX_SS_COMPONENT = 16;   // 8 engines
		internal const int MAX_SS_MODULE    = 12;   // 4 module sets

		// Derived rather than fixed at Civ 1's 39, because this project's structure
		// requirement is its own formula — a maxed hull must remain buildable.
		internal static int MaxSpaceshipStructural
			=> SpaceshipStructuresNeeded(MAX_SS_COMPONENT, MAX_SS_MODULE);

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
		private int _warmingIndicator;
		private uint _warmingIndicatorTurn = uint.MaxValue;

		public int WarmingIndicator
		{
			get
			{
				// The sibling of the HandleGlobalWarming bug below, and missed when that one
				// was fixed: 1/3/5 are absolute tile counts taken from Civ 1's fixed 80x50
				// board. On a 320x200 map SIX polluted tiles out of 64000 pinned this at 4,
				// the maximum, from roughly the first industrial city onward — while the
				// warming mechanic itself, correctly area-scaled, needs 128 tiles to fire.
				// The two halves of the same system disagreed by a factor of twenty.
				//
				// And this is NOT just the advisor's colour. HurricaneCheck (City.cs:2366)
				// takes this number directly: strike chance is 1+warming percent, and the
				// catastrophic threshold is 100 - warming*7, which the comment there
				// explicitly reserves as "the price of a polluted planet". At a pinned 4
				// that is five times the storm rate and ~28% super-typhoons, on a world
				// with six smoking tiles.
				//
				// Same scaling as the threshold below, so the classic board is unchanged
				// (scale == 1 there) and the ratios between indicator and trigger hold.
				// Cached per turn. This is a full 64000-tile scan, and SideBar.DrawDemographics
				// reads it to colour a 7x7 dot on roughly every other tick — the probe measured
				// 457 scans per turn. HandleGlobalWarming and the hurricane roll read it too.
				//
				// A per-turn cache rather than a maintained counter, deliberately: it is
				// recomputed from truth every turn, so it cannot drift away from the map the
				// way an incremented counter would across the six places pollution is set or
				// cleared (including Game.cs:740, where warming wipes every polluted tile at
				// once). Same reasoning as AI.PollutionBacklog.
				if (_warmingIndicatorTurn == _gameTurn) return _warmingIndicator;

				long __wi = TurnMetrics.Now;
				int scale = Math.Max(1, Map.WIDTH * Map.HEIGHT / 4000);
				int n = Map.AllTiles().Count(t => t.Pollution);
				TurnMetrics.AddBucket("tick:WarmingIndicatorScan", __wi);
				int level = n == 0        ? 0
				          : n <= 1 * scale ? 1
				          : n <= 3 * scale ? 2
				          : n <= 5 * scale ? 3
				          : 4;
				_warmingIndicatorTurn = _gameTurn;
				_warmingIndicator = level;
				return level;
			}
		}

		internal void HandleGlobalWarming()
		{
			int polluted = Map.AllTiles().Count(t => t.Pollution);
			// Civ 1's 8 + 2n was counted against a fixed 80x50 map. Ours is sized at
			// generation: an epic 320x200 map is 64000 tiles, so the unscaled constant
			// fired a planet-wide climate event on 14 polluted tiles out of 64000 —
			// roughly sixteen times too eagerly. Measured on a turn-551 save: three
			// warming events by 1996 AD with only 8 tiles smoking and one civ (in
			// Anarchy) responsible; the icecaps were down to 0.3% of land and swamp had
			// become the second-commonest terrain at 14.4%.
			int threshold = (8 + (GlobalWarmingCount * 2)) * Map.WIDTH * Map.HEIGHT / 4000;
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
				// Floor of 1, not 0. At GlobalWarmingCount >= 7 a floor of 0 made the test
				// `adjacentOcean >= 0` true for EVERY land tile on the map, so a single
				// event past the seventh turned the whole world — inland mountains included
				// — to swamp and jungle and cleared every irrigation and mine on it. Sea
				// level rising onto ground that touches no sea is not a flood.
				int oceanThreshold = Math.Max(1, 7 - GlobalWarmingCount);

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
				Tick("GlobalWarming", HandleGlobalWarming);

				// Recompute continent topology if any tile flipped land<->ocean this
				// round (global warming drowning coastline, settlers terraforming).
				// No-op unless something actually changed; a single BFS over the map
				// when it did. Without this, GotoStep's continent short-circuit goes
				// stale and land units burn a full failed A* every turn trying to
				// reach fragments that are no longer connected.
				Map.Instance.RecalculateContinentsIfDirty();

				GameTurn++;
				{ long __s = TurnMetrics.Now; RecordScoreSnapshot(); TurnMetrics.AddScoreSnapshot(__s); }

				// Per-turn timing: emit what the round cost, split by phase, then
				// reset the counters for the next one. See TurnMetrics.
				if (_turnClock != 0)
					DecisionLogger.LogTurnTiming(GameTurn,
						(TurnMetrics.Now - _turnClock) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
						_cities.Count(c => c is not null && c.Size > 0), _units.Count, _players.Count);
				_turnClock = TurnMetrics.Now;
				TurnMetrics.Reset();

				// Leonardo's Workshop: one free unit upgrade per owner per turn
				if (!WonderObsolete<LeonardosWorkshop>())
					foreach (Player lp in _players.Where(p => p != null && !p.IsDestroyed() && p.HasWonder<LeonardosWorkshop>()))
						ApplyLeonardoUpgrade(lp);

				// Nanobot Factory (blessed roll): the late-game workshop — three free
				// upgrades per owner per turn. A cursed factory never grants any.
				if (!NanobotCursed)
					foreach (Player np in _players.Where(p => p != null && !p.IsDestroyed() && p.HasWonder<Wonders.NanobotFactory>()))
						ApplyNanobotUpgrades(np);

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

				// Olvir bloom: the refugees' reproductive strategy borders on semelparity —
				// one overwhelming generation of settlement after landfall, then it is
				// spent. While the window is open the colony buds free cities every turn,
				// escalating with its own mass, until the window closes or bloom mass
				// (40 cities) is reached.
				if (VisitorsArrived && VisitorType == VisitorArchetype.Refugees
				    && OlvirBloomEndTurn > 0 && _gameTurn <= OlvirBloomEndTurn)
				{
					Player? bloom = _players.FirstOrDefault(p => p.Civilization is Civilizations.Olvir);
					if (bloom is not null && bloom.Cities.Length > 0 && bloom.Cities.Length < 40)
					{
						int buds = Math.Max(1, bloom.Cities.Length / 6);
						foreach (City parent in bloom.Cities.OrderBy(_ => Common.Random.Next(10000)).ToArray())
						{
							if (buds <= 0 || bloom.Cities.Length >= 40) break;
							var site = FindOlvirBudSite(parent);
							if (site is null) continue;
							if (FoundOlvirCity(bloom, site.Value.x, site.Value.y) is not null)
								buds--;
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
					var doneScreen = new Screens.DomeCompleteTransmission(gameDate, VisitorType, VisitorsArrived, SETISignalReceived);
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

				// ── Economic dominance: Pax Mercatoria ───────────────────────────────
				// The merchant's finish line: hold more than half the world's gross
				// economic output for 20 consecutive turns, with Banking known, at
				// least 3 rivals still standing, no war of the human's own making,
				// and half the surviving rivals economically bound to the human
				// (tribute, defense pact, or an active trade route) — the world's
				// economy runs through you. Defensive wars don't break the streak:
				// dominance by commerce, not cannon.
				{
					Player[] econRivals = _players.Where(p => p is not null && p != HumanPlayer
						&& !p.IsDestroyed() && PlayerNumber(p) != 0
						&& !(p.Civilization is Civilizations.TheOthers or Civilizations.TheThing or Civilizations.Skynet)).ToArray();

					if (HumanPlayer.HasAdvance<Banking>() && econRivals.Length >= 3)
					{
						byte hnum = PlayerNumber(HumanPlayer);
						int humanOut = GrossOutput(HumanPlayer);
						int worldOut = _players.Where(p => p is not null && !p.IsDestroyed() && PlayerNumber(p) != 0)
							.Sum(GrossOutput);
						bool share = humanOut > 0 && humanOut * 2 > worldOut;

						bool aggressing = _players.Any(p => p is not null && !p.IsDestroyed()
							&& HumanPlayer.IsAtWar(p) && HumanStartedWars.Contains(PlayerNumber(p)));

						bool Bound(Player r)
						{
							byte rnum = PlayerNumber(r);
							return r.PaysTributeTo(HumanPlayer) || r.HasDefensePact(HumanPlayer)
								|| _cities.Any(c => c.Size > 0 &&
									((c.Owner == hnum && c.TradeRoutes.Any(t => t.Partner.Owner == rnum))
									|| (c.Owner == rnum && c.TradeRoutes.Any(t => t.Partner.Owner == hnum))));
						}
						bool boundHalf = econRivals.Count(Bound) * 2 >= econRivals.Length;

						if (share && !aggressing && boundHalf)
						{
							EconStreak++;
							if (EconStreak == 1)
								GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
									"Our merchants dominate",
									"world trade. Hold the markets",
									"for 20 years."));
							else if (EconStreak == 10)
								GameTask.Enqueue(Message.Newspaper(null!, "Half way to hegemony!",
									"The world's markets", "answer to us."));

							if (EconStreak >= 20)
							{
								HumanPlayer.AwardMilestone(150);
								DecisionLogger.EndGame(HumanPlayer.Score, "Economic Dominance", humanWon: true, turns: _gameTurn);
								int econFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Economic Dominance");
								string? econArt = Screens.EventArtScreen.FindPath("PaxMercatoria");
								if (econArt is not null)
									GameTask.Enqueue(Show.Screen(new Screens.EventArtScreen(econArt,
										"PAX MERCATORIA — THE WORLD BANKS WITH YOU")));
								GameTask.Enqueue(Message.Newspaper(null!, "Pax Mercatoria!",
									"The world's economy", "runs through you."));
								GameTask econFt;
								GameTask.Enqueue(econFt = Show.Screen(new Screens.Reports.FinalScore("Economic Dominance")));
								econFt.Done += (s, a) => EndSequence.ChainAfterFinal(econFame, () => Runtime.Quit());
								return;
							}
						}
						else if (EconStreak > 0)
						{
							string why = !share ? "Our share of world trade slipped."
								: aggressing ? "Wars of our making unsettle the markets."
								: "Too few nations bank with us.";
							EconStreak = 0;
							GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
								"The markets waver.", why, "The streak is broken."));
						}
					}
					else if (EconStreak > 0)
					{
						EconStreak = 0; // world shrank below the floor mid-streak
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

					if (SETISignalReceived && VisitorType == VisitorArchetype.Owners)
					{
						// Owners timeline: nothing leaves. The recovery fleet's pickets take
						// every colony ship — launched before or after the arrival at Earth,
						// Dome or no Dome. Even the negotiated outcome leaves humanity a
						// contained nuisance, not a spacefaring species.
						for (int p = 1; p < _players.Count; p++)
						{
							if (SpaceshipArrivalTurn[p] != bestArrival) continue;
							SpaceshipArrivalTurn[p] = 0;
							if (_players[p] == HumanPlayer)
							{
								GameTask.Enqueue(Show.EventArt("spaceshipintercepted",
									"Contact lost. Final telemetry shows an object of impossible size."));
								GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
									"The colony ship is gone.",
									"Something was waiting on the",
									"road to Alpha Centauri."));
							}
							else
							{
								GameTask.Enqueue(Message.Newspaper(null!,
									$"{_players[p].TribeNamePlural} spaceship", "lost in deep space!", "No survivors."));
							}
						}
					}
					else if (SETISignalReceived)
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
				if (Common.TurnToYear(_gameTurn) >= 2200 && SETISignalReceived && !_coexistenceFired)
				{
					_coexistenceFired = true;
					Player? olvir = _players.FirstOrDefault(p => p.Civilization is Civilizations.Olvir);

					// Credit the world you SHARED, not everything the visitors did anywhere.
					// Counting every Olvir city and improvement on the planet made this award
					// a measure of Olvir industry rather than of the player's stewardship:
					// 883 improvements at 5 points each came to 4,415, against 271 points
					// that Japan had earned by its own efforts across the entire game. A
					// ten-city civ with no wonders finished ahead of a Rome holding 79
					// cities. Only what grew within reach of the player's own cities counts —
					// the shared frontier, which is the thing the ending is about.
					const int ShareRadius = 6;
					bool NearUs(int x, int y) =>
						HumanPlayer.Cities.Any(c => Common.DistanceToTile(c.X, c.Y, x, y) <= ShareRadius);

					int sharedCities = olvir?.Cities.Count(c => NearUs(c.X, c.Y)) ?? 0;
					int sharedWorks  = OlvirImprovements.Keys.Count(k => NearUs(k.x, k.y));

					int coexistence = 100                                                    // reached a peaceful end at all
					                + (VisitorType == VisitorArchetype.Refugees ? 100 : 0)   // they came in peace, and stayed that way
					                + sharedCities * 25                                      // an Olvir home made among ours
					                + sharedWorks * 5;                                        // and ground we reshaped together
					HumanPlayer.AwardMilestone(coexistence);
					Log($"Coexistence: {sharedCities}/{olvir?.Cities.Length ?? 0} Olvir cities and "
					  + $"{sharedWorks}/{OlvirImprovements.Count} improvements within {ShareRadius} tiles — award {coexistence}");

					DecisionLogger.EndGame(HumanPlayer.Score, "Coexistence", humanWon: true, turns: _gameTurn);
					int coexFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Coexistence");
					GameTask.Enqueue(Message.Newspaper(null!, "The year is 2200.",
						(olvir?.Cities.Length ?? 0) > 0 ? "Two species share one Earth." : "Earth endures, alone.",
						$"Your score: {HumanPlayer.Score}"));
					GameTask coexFt;
					GameTask.Enqueue(coexFt = Show.Screen(new FinalScore("Coexistence")));
					coexFt.Done += (s, a) => EndSequence.ChainAfterFinal(coexFame, () => Runtime.Quit());
					return;
				}

				PerformAutoSave();

				// The manifest is being processed: every 5th turn, each Registry-held
				// city of size 2+ loses a citizen to the transports, and the Registry
				// banks the cargo. The occupation is a countdown, not a stalemate —
				// liberation is the only way to stop the collection.
				Player? registry = _players.FirstOrDefault(p => p is not null
					&& p.Civilization is Civilizations.TheOthers && !p.IsDestroyed());
				if (registry is not null && _gameTurn % 5 == 0)
				{
					byte rnum = PlayerNumber(registry);
					byte hnum = PlayerNumber(HumanPlayer);
					bool humanLoss = false;
					foreach (City c in _cities.Where(c => c.Owner == rnum && c.Size >= 2).ToArray())
					{
						c.Size--;
						registry.Gold += 25;
						if (c.OriginalOwner == hnum) humanLoss = true;
					}
					if (humanLoss)
						GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
							"Transports lift from the",
							"occupied cities. The population",
							"is being collected."));
				}

				// The Thing: advance the outbreak clocks (South Pole Expedition curse).
				Tick("ThingOutbreaks", ProcessThingOutbreaks);

				// Cultural defection: disorderly frontier towns may choose a more
				// admired civilization's flag.
				Tick("CultureDefections", ProcessCultureDefections);

				// Gozira falls: the rampage ends when the kaiju is destroyed.
				if (GoziraState == 1 && !_units.Any(u => u is Units.Gozira))
				{
					GoziraState = 2;
					GameTask.Enqueue(Message.Newspaper(null!, "Gozira falls!",
						"The long watch of the", "coastal cities is over."));
				}

				// The Leviathan is slain: a newspaper worth framing, and glory for
				// the hunters (milestone bonus).
				if (LeviathanState == 1 && !_units.Any(u => u is Units.Leviathan))
				{
					LeviathanState = 2;
					HumanPlayer.AwardMilestone(50);
					GameTask.Enqueue(Message.Newspaper(null!, "The Leviathan is slain!",
						"Every port on Earth", "toasts the hunters."));
				}

				// The Greys: spread and eviction (The Portal's cursed outcome).
				ProcessGreys();

				// Grey goo: consume units, advance the doubling front (Nanobot Factory curse).
				ProcessGreyGoo();

				// The King in Yellow: cures, contagion, abandoned routes.
				Tick("KingInYellow", ProcessKingInYellow);

				// Newton's anomaly: gifts and thefts from other whens.
				Tick("Anomaly", ProcessAnomaly);

				// The Visitations: the beacon over the Pyramids, four thousand years.
				Tick("Visitations", ProcessVisitations);

				// The stone door: the tithe while the Guardian stands; shut when it falls.
				Tick("StoneDoor", ProcessStoneDoor);

				// The Other Voice: true prophecies for the Oracle's keeper.
				Tick("OracleVoice", ProcessOracleVoice);

				// Strategic resource camps: occupation changes the flag.
				Tick("ResourceCamps", ProcessResourceCamps);

				// Skynet: the fifth Neural Lab in the world wakes the machines.
				Tick("Skynet", CheckSkynet);

				Tick("Disasters", () =>
				{
					IEnumerable<City> disasterCities = _cities.OrderBy(o => Common.Random.Next(0,1000)).Take(2).AsEnumerable();
					foreach (City city in disasterCities)
						city.Disaster();
				});

				// Hurricanes/typhoons: ONE storm in the world at most, and no more often than
				// every five game years.
				//
				// Every coastal city in the two storm bands used to roll independently every
				// single turn. With a settled map that is several landfalls per turn somewhere
				// in the world, and a coastal city cannot hold a Temple or an Aqueduct long
				// enough to matter — a Major strike destroys a building AND converts a worked
				// coastal tile to Swamp (City.cs:2377). That is where the swamp bunched around
				// coastlines came from; it reads as warming damage but it is storm waterlogging,
				// and it accumulated far faster than warming ever could.
				//
				// The per-city gates (latitude band, coast class, warming-scaled probability,
				// severity roll) are all unchanged, so WHICH city is hit and how hard is still
				// the same model — there is simply one landfall per cooldown instead of one per
				// eligible city per turn. Cities are walked in random order so the storm does
				// not favour whoever appears first in the list.
				int currentYear = Common.TurnToYear(_gameTurn);
				if (currentYear - LastHurricaneYear >= HurricaneCooldownYears)
				Tick("Hurricanes", () =>
				{
					// WarmingIndicator scans the whole map, so compute it once.
					int hurricaneWarming = WarmingIndicator;
					foreach (City city in _cities.OrderBy(c => Common.Random.Next(0, 1000)))
					{
						if (!city.HurricaneCheck(hurricaneWarming)) continue;
						LastHurricaneYear = currentYear;
						break;
					}
				});

				// Barbarian population cap: spawns used to accumulate without limit (127
				// units by the late game — collectively larger than any AI army), pinning
				// nearby AI civs in the Militarize stance forever and erasing their size-1
				// cities. New raids only spawn while the horde is below the cap.
				const int barbarianCap = 30;
				int barbarianUnits = _units.Count(u => u.Owner == 0);

				if (Barbarian.IsSeaSpawnTurn && barbarianUnits < barbarianCap)
				{
					ITile? tile = Barbarian.SeaSpawnPosition;
					if (tile is not null)
					{
						foreach (UnitType unitType in Barbarian.SeaSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}

				if (Barbarian.IsLandSpawnTurn && barbarianUnits < barbarianCap)
				{
					ITile? tile = Barbarian.LandSpawnPosition;
					if (tile is not null)
					{
						foreach (UnitType unitType in Barbarian.LandSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}

				// Great Wall curse: the old builders knew what the wall was for.
				// While the beacon burns, each raid season lands a second horde
				// on the builder's continent (docs/cursed_wonders.md #9).
				if (WallCurseEndTurn > 0 && _gameTurn <= WallCurseEndTurn
				    && Barbarian.IsLandSpawnTurn && barbarianUnits < barbarianCap)
				{
					ITile? beacon = Map.ContinentTiles(WallCurseContinent)
						.Where(t => !t.IsOcean && t.City is null && !(t is Tiles.Arctic)
						         && !t.Units.Any()
						         && !_cities.Any(c => c.Size > 0 && TileDistance(c.X, c.Y, t.X, t.Y) < 3))
						.OrderBy(_ => Common.Random.Next(10000))
						.FirstOrDefault();
					if (beacon is not null)
					{
						foreach (UnitType unitType in Barbarian.LandSpawnUnits)
							CreateUnit(unitType, beacon.X, beacon.Y, 0, false);
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

		// Names belonging to the barbarians and the story factions, as a flat index mask over
		// CityNames. An ordinary civilization that outgrows its own 16 names falls through to
		// the shared pool in index order, and the old guard for that pool was a single
		// threshold ("below the last block") — which walled off Skynet alone, purely because
		// Skynet sorts last. Everything else reserved sat below the line, so a Roman city
		// forty names deep was named Vel'Thara. Exclude by owner instead of by position.
		private static bool[]? _reservedName;
		private static bool ReservedName(int index)
		{
			if (_reservedName is null)
			{
				var mask = new List<bool>();
				foreach (ICivilization c in Common.Civilizations)
				{
					bool reserved = c is Civilizations.Barbarian or Civilizations.Olvir
					             or Civilizations.TheOthers or Civilizations.TheThing
					             or Civilizations.Skynet;
					for (int i = 0; i < c.CityNames.Length; i++) mask.Add(reserved);
				}
				_reservedName = mask.ToArray();
			}
			return index < _reservedName.Length && _reservedName[index];
		}

		internal int CityNameId(Player player)
		{
			ICivilization civilization = player.Civilization;
			ICivilization[] civilizations = Common.Civilizations;
			int startIndex = Enumerable.Range(1, civilization.Id - 1).Sum(i => civilizations[i].CityNames.Length);
			// A reserved civilization keeps first claim on its own block (the OrderBy below)
			// but may still borrow from the shared pool if it outgrows it.
			bool ownNamesAreReserved = ReservedName(startIndex);
			int[] used = _cities.Select(c => c.NameId).ToArray();
			int[] available = Enumerable.Range(0, CityNames.Length)
				.Where(i => !used.Contains(i))
				.Where(i => ownNamesAreReserved || !ReservedName(i))
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
				// skips these terrains), so its town starves. Convert the wet terrains —
				// Jungle and Swamp — to Grassland for EVERY founder (player and AI): nobody
				// wants a city on a swamp, and this saves the human the manual clear too.
				// Forest is still converted for the AI only (it doesn't know to clear first),
				// but left as-is for a human who may found on forest deliberately.
				// Olvir excluded: refugees have their own jungle-tolerant placement (SpawnOlvir).
				bool wetTerrain = Map[x, y] is Jungle || Map[x, y] is Swamp;
				if (!(player.Civilization is Civilizations.Olvir)
				    && (wetTerrain || (!player.IsHuman && Map[x, y] is Forest)))
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
			InvalidateBuiltWonders();
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
			InvalidateBuiltWonders();
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
				case UnitType.Gozira: unit = new Gozira(); break;
				case UnitType.Leviathan: unit = new Leviathan(); break;
				case UnitType.HengeGuardian: unit = new HengeGuardian(); break;
				case UnitType.Longboat: unit = new Longboat(); break;
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

		// Non-allocating read-only view for hot paths (City.InvalidTile runs this thousands
		// of times per turn — GetCities().ToArray() there was a major late-game allocation
		// source). Callers must not mutate the city list while enumerating.
		internal IReadOnlyList<City> CitiesList => _cities;

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

		// Rebuilding this walks every city and allocates a new array. It is read from
		// Player.Visible (via WonderBuilt<ApolloProgram>), which the sidebar minimap
		// calls once per tile — 3,744 tiles, ~500 times a turn. At 283 cities that was
		// on the order of 500 MILLION operations per turn to answer a question whose
		// answer changes only when a wonder is completed.
		//
		// Cached and invalidated by BuildingsChanged(), which every add/remove path
		// already funnels through.
		private IWonder[]? _builtWonders;
		public IWonder[] BuiltWonders => _builtWonders ??= _cities.SelectMany(c => c.Wonders).ToArray();

		// Called whenever a city gains or loses a building/wonder, or a city is added
		// or destroyed, so the cached wonder list cannot go stale.
		internal void InvalidateBuiltWonders() => _builtWonders = null;

		// Static form for callers that may run BEFORE the singleton is assigned —
		// LoadCos builds every city inside the Game constructor, so City.AddWonder
		// fires while Game.Instance is still null. Uses the backing field directly:
		// the Instance property logs an error when null, which would spam once per
		// wonder during a load. Nothing to invalidate before the instance exists —
		// the cache starts empty and is populated on first read afterwards.
		internal static void InvalidateBuiltWondersSafe()
		{
			if (_instance is not null) _instance._builtWonders = null;
		}

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
		// What kind of visitor Earth attracts, judged on the SPECIES rather than on one
		// civilization. The signal that summons them is world-wide (five Observatories
		// anywhere), so the reception should be read the same way: every living civ is
		// assessed on the same rubric, weighted by the share of humanity it actually
		// represents. A large, well-governed, peaceful power carries the verdict; a
		// single backwater no longer damns everyone — which is what happened when the
		// old human-only test let an autoplay laggard summon a recovery fleet down onto
		// a world whose leading civ had just built a starship.
		//
		// The human is in the average like anybody else, so their choices still tell —
		// in proportion to how much of the world they are.
		private VisitorArchetype SelectVisitorArchetype()
		{
			// Story factions are not humanity and get no vote.
			Player[] nations = _players.Where(p => p is not null && !p.IsDestroyed()
				&& PlayerNumber(p) != 0
				&& !(p.Civilization is Civilizations.Olvir or Civilizations.TheOthers
				                    or Civilizations.TheThing or Civilizations.Skynet))
				.ToArray();
			if (nations.Length == 0) return VisitorArchetype.Owners;

			double avgCulture = nations.Average(p => (double)p.Culture);

			// positive = enlightened (Refugees), negative = harsh (Owners)
			int Assess(Player n)
			{
				int score = 0;

				// Government — the clearest read on a civilization's character.
				if (n.Government is CivOne.Governments.Democracy)      score += 3;
				else if (n.Government is CivOne.Governments.Republic)  score += 2;
				else if (n.Government is CivOne.Governments.Monarchy)  score -= 1;
				else                                                   score -= 2; // Despotism / Anarchy / Communism

				// Wars — an aggressive, embattled civ leans Owners.
				int wars = nations.Count(p => p != n && n.IsAtWar(p));
				score -= Math.Min(wars, 3);

				// Happiness / culture — Temple coverage across the empire leans Refugees.
				if (n.Cities.Length > 0)
				{
					double temples = n.Cities.Count(c => c.HasBuilding<Temple>()) / (double)n.Cities.Length;
					if (temples >= 0.6) score += 2;
					else if (temples <= 0.2) score -= 1;
				}

				// Accumulated culture, against the world's average: a deep artistic and
				// civic tradition reads as an enlightened people.
				if (avgCulture > 0)
				{
					if (n.Culture > avgCulture * 2)      score += 2;
					else if (n.Culture * 2 < avgCulture) score -= 1;
				}

				// Pollution — a smoke-choked land is loud and careless; leans Owners.
				if (n.Pollution >= 8)      score -= 2;
				else if (n.Pollution == 0) score += 1;

				return score;
			}

			// Weighted by population: the visitors are judging a species, and a civ of
			// forty cities is more of it than a civ of two. Population can be zero for a
			// civ down to its last settler, hence the floor.
			double weighted = 0, totalWeight = 0;
			foreach (Player n in nations)
			{
				double weight = Math.Max(1.0, n.Population);
				weighted    += Assess(n) * weight;
				totalWeight += weight;
			}
			double character = totalWeight > 0 ? weighted / totalWeight : 0;

			// Map the character score to P(Refugees), clamped so it is never deterministic.
			double pRefugees = 0.5 + character * 0.07;
			if (pRefugees < 0.20) pRefugees = 0.20;
			if (pRefugees > 0.80) pRefugees = 0.80;

			DecisionLogger.LogVisitorDraw(character, pRefugees, nations.Length);

			return Common.Random.Next(100) < (int)System.Math.Round(pRefugees * 100)
				? VisitorArchetype.Refugees
				: VisitorArchetype.Owners;
		}

		// The Owners arrival forks on whether the planetary defence dome was built.
		// Dome held: a negotiated ending (Disputed Claim). No dome: Phase 1 of the
		// invasion — orbital strikes on every capital, and the game continues as a
		// playable war rather than ending on a cutscene.
		private void ArriveOwners()
		{
			bool domeHeld = DomeComplete;
			string gameDate = GameYear;
			RecordTransmission("OwnersArrival", gameDate);

			// The chronicle should name the single most important thing that ever happens
			// in a game. Without this the replay shows the invasion only as an unexplained
			// cascade of city captures by a civilization that was not there the turn before.
			AddReplayEvent(new ReplayData.Milestone(_gameTurn, domeHeld
				? "THE OWNERS ARRIVE — the dome holds; the claim is disputed"
				: "THE OWNERS ARRIVE — the recovery fleet takes orbit"));

			// Arrival art: the fleet over Earth. Optional — plays before the transmissions
			// in both branches when data/event_art/TheOthersArrive.png exists.
			string? arriveArt = Screens.EventArtScreen.FindPath("TheOthersArrive");
			if (arriveArt is not null)
				GameTask.Enqueue(Show.Screen(new Screens.EventArtScreen(arriveArt,
					domeHeld ? "ARRIVAL — THE CLAIM IS DISPUTED" : "ARRIVAL — RECOVERY FLEET IN ORBIT")));

			if (domeHeld)
			{
				GameTask.Enqueue(Show.Screen(new Screens.OwnersArrivalTransmission(gameDate, domeHeld)));

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
				// The Decapitation: strikes resolve immediately, then the player reads
				// about it. The Fusion Core holder's capital is saved by the same
				// space-based interceptors that stop Nuclear units (BaseUnit.Confront).
				Player? coreHolder = ExecuteOwnersStrike(out int struck);
				GameTask.Enqueue(Show.Screen(new Screens.OwnersStrikeTransmission(
					gameDate, coreHolder?.TribeNamePlural, struck)));

				City? humanCapital = HumanPlayer.Cities.FirstOrDefault(c => c.HasBuilding<Palace>());
				if (humanCapital is not null && HumanPlayer != coreHolder)
					GameTask.Enqueue(Show.EventArt("nuclearbombdetonation", $"Orbital strike on {humanCapital.Name}!"));

				// The Landing: The Others join as a faction, seize the ports, and the
				// war for repossession begins.
				int seized = ExecuteOwnersLanding();
				GameTask.Enqueue(Show.Screen(new Screens.OwnersLandingTransmission(gameDate, seized)));
			}
		}

		// Phase 1 of the Owners invasion: one orbital strike per surviving civilization's
		// capital (the Palace city). Effects mirror a nuclear detonation plus meltdown
		// fallout: half the population, the garrison and anything adjacent vaporised,
		// fallout across the hinterland, and up to two buildings destroyed (never the
		// Palace — the ruin stays the capital). Returns the Fusion Core holder whose
		// capital was saved, if any; `struck` counts the capitals actually hit.
		private Player? ExecuteOwnersStrike(out int struck)
		{
			struck = 0;
			Player? coreHolder = null;
			foreach (Player p in _players.Where(p => p is not null && PlayerNumber(p) != 0 && !p.IsDestroyed()).ToArray())
			{
				City? capital = p.Cities.FirstOrDefault(c => c.HasBuilding<Palace>());
				if (capital is null) continue;

				if (p.HasWonder<FusionCore>())
				{
					coreHolder = p;
					continue;
				}
				struck++;

				capital.Size = (byte)Math.Max(1, capital.Size / 2);

				foreach (ITile tile in Map.QueryMapPart(capital.X - 2, capital.Y - 2, 5, 5))
				{
					if (tile is null || tile.IsOcean || tile.City is not null) continue;
					tile.Pollution = true;
				}

				foreach (ITile tile in Map.QueryMapPart(capital.X - 1, capital.Y - 1, 3, 3))
				{
					if (tile is null) continue;
					foreach (IUnit u in tile.Units.ToArray())
						DisbandUnit(u);
				}

				for (int i = 0; i < 2; i++)
				{
					IBuilding[] burnable = capital.Buildings.Where(b => !(b is Palace)).ToArray();
					if (burnable.Length == 0) break;
					capital.RemoveBuilding(burnable[Common.Random.Next(burnable.Length)]);
				}

				Log($"Owners strike: capital {capital.Name} ({p.TribeName}) hit");
			}
			return coreHolder;
		}

		// Phase 2 of the Owners invasion: The Others join as a live faction and seize
		// the world's ports — up to a quarter of all cities, capped at half of any one
		// civ's so the war has survivors, capitals exempt (already rubble; a one-city
		// civ can't lose its last city to the seizure). Largest coastal cities go
		// first: they administrate, and administrators take the harbours. Every
		// surviving civilization is on the manifest — war with all, peace with none.
		// Returns the number of cities seized.
		private int ExecuteOwnersLanding()
		{
			ICivilization othersCiv = Common.Civilizations.First(c => c is Civilizations.TheOthers);
			var others = new Player(othersCiv, "The Registry");
			AddPlayer(others);
			byte num = PlayerNumber(others);

			// The occupation force arrives with the full catalogue of its own science,
			// a treasury, and no interest in constitutional experiments.
			foreach (IAdvance adv in Common.Advances.Where(a => !(a is FutureTech)))
				if (!others.HasAdvance(adv)) others.AddAdvance(adv, false);
			others.Government = new Governments.Communism();
			others.Gold = 1500;

			// Pick the seizure list: group by owner, cap per civ, ports-first.
			City[] world = _cities.Where(c => c.Size > 0 && c.Owner != 0 && c.Owner != num).ToArray();
			int quota = world.Length / 4;
			var seizedList = new List<City>();
			bool Coastal(City c) => Map[c.X, c.Y].GetBorderTiles().Any(t => t.IsOcean);
			foreach (var group in world.GroupBy(c => c.Owner))
			{
				int civCap = group.Count() / 2;
				seizedList.AddRange(group
					.Where(c => !c.HasBuilding<Palace>())
					.OrderByDescending(Coastal)
					.ThenByDescending(c => c.Size)
					.Take(civCap));
			}
			seizedList = seizedList
				.OrderByDescending(Coastal)
				.ThenByDescending(c => c.Size)
				.Take(quota)
				.ToList();

			foreach (City city in seizedList)
			{
				// The defenders are deprecated; units homed here elsewhere fight on unsupported.
				foreach (IUnit unit in Map[city.X, city.Y].Units.ToArray())
					DisbandUnit(unit);
				foreach (IUnit unit in city.Units.ToArray())
					unit.SetHome(null);

				_replayData.Add(new ReplayData.CityCaptured(_gameTurn, _cities.IndexOf(city), city.NameId, city.X, city.Y, num));
				city.Owner = num;
				city.ResetResourceTiles();
				// The Registry does not finish the previous administration's paperwork:
				// drop the inherited production queue and put the works to war material.
				city.ClearProductionQueue();
				city.SetProduction(new Units.MechInf());

				// Occupation garrison: the recovery detail, not an army.
				CreateUnit(UnitType.FusionInf, city.X, city.Y, num);
				CreateUnit(UnitType.FusionInf, city.X, city.Y, num);
				CreateUnit(UnitType.HoverTank, city.X, city.Y, num);
			}

			foreach (Player p in _players.Where(p => p is not null && p != others && !p.IsDestroyed()))
				others.DeclareWar(p);

			Log($"Owners landing: {seizedList.Count} cities seized (quota {quota} of {world.Length})");
			return seizedList.Count;
		}

		// Gross economic output for the Pax Mercatoria check: total trade arrows
		// across the empire (tax-slider-proof; includes trade-route bonuses) plus
		// tribute inflow.
		private int GrossOutput(Player p)
		{
			byte num = PlayerNumber(p);
			int output = _cities.Where(c => c.Owner == num && c.Size > 0).Sum(c => c.TradeTotal);
			foreach (Player payer in p.TributePayers)
				output += payer.TributeAmountTo(p);
			return output;
		}

		// ── The Portal (cursed wonder #3) ────────────────────────────────────

		// Contact with the extra-planar. Three times in four: luminous beings
		// whose counsel ends every war on Earth. One in four: the Greys move
		// into the wonder city. Returns true when the Greys came through.
		internal bool OpenPortal(Player builder, City site)
		{
			// Curses off: the Portal always finds the benign visitors (global peace),
			// never the Greys.
			if (!Settings.Instance.CursedWonders || Common.Random.Next(4) != 0)
			{
				// Peace among everyone capable of it (story factions are not).
				Player[] nations = _players.Where(p => p is not null && !p.IsDestroyed()
					&& PlayerNumber(p) != 0
					&& !(p.Civilization is Civilizations.TheOthers or Civilizations.TheThing or Civilizations.Skynet)).ToArray();
				foreach (Player a in nations)
					foreach (Player b in nations.Where(x => x != a))
					{
						a.MakePeace(b);
						a.SetPeaceTreaty(b, 50);
						a.SetAttitudeBonus(b, 50);
					}
				Log($"Portal opened by {builder.TribeName}: luminous counsel, global peace");
				return false;
			}

			GreyCities.Add((site.X, site.Y));
			InvalidateCitiesAt(site.X, site.Y);
			Log($"Portal opened by {builder.TribeName}: the Greys settle in {site.Name}");
			return true;
		}

		// The Greys: every 10th turn the houseguests discover a new city — the
		// nearest to any infested one. A city that goes hungry for a turn is
		// beneath their standards: they leave. They never fight; they just cost.
		private void ProcessGreys()
		{
			if (GreyCities.Count == 0) return;

			// Eviction first: austerity works.
			foreach (var key in GreyCities.ToArray())
			{
				City? host = GetCity(key.x, key.y);
				if (host is null || host.Size == 0)
				{
					GreyCities.Remove(key);
					continue;
				}
				if (host.FoodIncome < 0)
				{
					GreyCities.Remove(key);
					InvalidateCitiesAt(key.x, key.y);
					if (host.Owner == PlayerNumber(HumanPlayer))
						GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
							$"The visitors have left {host.Name}.",
							"They said the food",
							"was better elsewhere."));
				}
			}

			if (GreyCities.Count == 0 || _gameTurn % 10 != 0) return;

			City? next = _cities
				.Where(c => c.Size > 0 && c.Owner != 0 && !GreyCities.Contains((c.X, c.Y)))
				.OrderBy(c => GreyCities.Min(g => TileDistance(c.X, c.Y, g.x, g.y)))
				.FirstOrDefault();
			if (next is null) return;
			GreyCities.Add((next.X, next.Y));
			InvalidateCitiesAt(next.X, next.Y);
			if (next.Owner == PlayerNumber(HumanPlayer))
				GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
					$"Visitors have settled in {next.Name}.",
					"They are very interested in",
					"our television broadcasts."));
		}

		// ── The Internet (cursed wonder #2) ──────────────────────────────────

		// The outbreak of Social Media: half the builder's cities — capital and
		// the wonder city exempt, most distant from the capital first — secede
		// as a new civilization, taking their garrisons, every advance, half
		// the treasury, and half the accumulated culture (the influencers went
		// with them). At peace, for now: reconquer them or win them back with
		// tribute, pacts, and gifts like anyone else. Returns the splinter, or
		// null when no split was possible (too few cities, no free identity).
		internal Player? ExecuteSocialMediaSchism(Player builder, City wonderCity)
		{
			byte bnum = PlayerNumber(builder);

			City? capital = builder.Cities.FirstOrDefault(c => c.HasBuilding<Palace>());
			City anchor = capital ?? wonderCity;

			City[] seceding = builder.Cities
				.Where(c => c.Size > 0 && c != wonderCity && !c.HasBuilding<Palace>())
				.OrderByDescending(c => TileDistance(c.X, c.Y, anchor.X, anchor.Y))
				.Take(builder.Cities.Length / 2)
				.ToArray();
			if (seceding.Length == 0) return null;

			// An identity for the schism: prefer an unused extended civ, fall back
			// to any original civ that is neither in this game nor dead in it.
			var takenIds = new HashSet<int>(_players.Where(p => p is not null).Select(p => p.Civilization.Id));
			var deadIds  = new HashSet<int>(_replayData.OfType<ReplayData.CivilizationDestroyed>().Select(r => (int)r.DestroyedId));
			ICivilization? identity = Common.Civilizations
				.Where(c => c.Id >= 17 && c.Id <= 26 && !takenIds.Contains(c.Id) && !deadIds.Contains(c.Id))
				.OrderBy(_ => Common.Random.Next(100))
				.FirstOrDefault()
				?? Common.Civilizations
				.Where(c => c.Id >= 1 && c.Id <= 14 && !takenIds.Contains(c.Id) && !deadIds.Contains(c.Id))
				.OrderBy(_ => Common.Random.Next(100))
				.FirstOrDefault();
			if (identity is null) return null;

			var splinter = new Player(identity);
			AddPlayer(splinter);
			byte snum = PlayerNumber(splinter);

			foreach (IAdvance adv in builder.Advances.ToArray())
				if (!splinter.HasAdvance(adv)) splinter.AddAdvance(adv, false);
			short dowry = (short)(builder.Gold / 2);
			builder.Gold -= dowry;
			splinter.Gold = dowry;
			int cultureShare = builder.Culture / 2;
			splinter.SetCulture(cultureShare);
			builder.SetCulture(builder.Culture - cultureShare);

			foreach (City city in seceding)
			{
				// Garrisons join the rebellion where they stand; units homed here
				// elsewhere fight on unsupported.
				foreach (IUnit unit in city.Units.ToArray())
					unit.SetHome(null);
				foreach (IUnit unit in Map[city.X, city.Y].Units.Where(u => u.Owner == bnum).ToArray())
				{
					unit.Owner = snum;
					unit.SetHome(null);
				}
				_replayData.Add(new ReplayData.CityCaptured(_gameTurn, _cities.IndexOf(city), city.NameId, city.X, city.Y, snum));
				city.Owner = snum;
				city.ResetResourceTiles();
				city.ClearProductionQueue();
				city.SetProduction(new Units.Militia());
			}

			// They know each other far too well.
			builder.EstablishEmbassy(splinter);
			splinter.EstablishEmbassy(builder);

			Log($"Social media schism: {seceding.Length} cities secede from {builder.TribeName} as the {splinter.TribeNamePlural}");
			return splinter;
		}

		// ── The King in Yellow (Shakespeare's Theatre curse) ─────────────────
		// The madness travels with the play: along trade routes, city to city.
		// A Cathedral cures — and immunizes — a city; a stronger faith than
		// the play. Merchants abandon routes that touch an afflicted city, so
		// the outbreak slowly strangles the network it rides.
		private void ProcessKingInYellow()
		{
			if (YellowCities.Count == 0) return;
			byte hnum = PlayerNumber(HumanPlayer);
			bool Afflicted(City c) => YellowCities.Contains((c.X, c.Y));

			// Cures and prunes first: Cathedral bells drown out the play.
			foreach (var key in YellowCities.ToArray())
			{
				City? c = GetCity(key.x, key.y);
				if (c is null || c.Size == 0)
				{
					YellowCities.Remove(key);
					continue;
				}
				if (c.HasBuilding<Cathedral>())
				{
					YellowCities.Remove(key);
					InvalidateCitiesAt(key.x, key.y);
					if (c.Owner == hnum)
						GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
							$"The mask has left {c.Name}.",
							"The cathedral bells drown",
							"out the play."));
				}
			}
			if (YellowCities.Count == 0) return;

			// Every route with exactly one afflicted endpoint is a stage door:
			// 5%/turn the healthy end sees the play (Cathedral-holders never do),
			// else 10%/turn the merchants refuse the run and the route is lost.
			foreach (City home in _cities.Where(c => c.Size > 0).ToArray())
				foreach (var route in home.TradeRoutes.ToArray())
				{
					City partner = route.Partner;
					if (partner.Size == 0 || Afflicted(home) == Afflicted(partner)) continue;
					City healthy = Afflicted(home) ? partner : home;
					City carrier = Afflicted(home) ? home : partner;

					if (!healthy.HasBuilding<Cathedral>() && Common.Random.Next(100) < 5)
					{
						YellowCities.Add((healthy.X, healthy.Y));
						InvalidateCitiesAt(healthy.X, healthy.Y);
						home.RemoveTradeRoutesTo(partner); // both ends know what rode that road
						if (healthy.Owner == hnum || carrier.Owner == hnum
						    || HumanPlayer.HasEmbassy(GetPlayer(healthy.Owner)))
							GameTask.Enqueue(Message.Newspaper(null!, $"{healthy.Name} has seen the play.",
								"The madness rides", "the caravans."));
					}
					else if (Common.Random.Next(100) < 10)
					{
						home.RemoveTradeRoutesTo(partner);
					}
				}
		}

		// ── Pyramids curse: the Visitations ──────────────────────────────────
		// The alignment is a beacon, and it will burn for four thousand years.
		// A small per-turn roll (hurricane mold) over the wonder city: a
		// household vanishes, a field burns in perfect circles, or — rarely —
		// recovered debris advances the owner's research. Mostly harmless,
		// permanently unsettling, no counterplay by design: an ambient haunting
		// that ends only if the monument's city does.
		private void ProcessVisitations()
		{
			if (!VisitationsActive) return;
			City? monument = GetCity(VisitationsX, VisitationsY);
			if (monument is null || monument.Size == 0)
			{
				VisitationsActive = false; // the beacon is rubble; the sky moves on
				return;
			}
			if (Common.Random.Next(100) >= 6) return;

			Player owner = GetPlayer(monument.Owner);
			int roll = Common.Random.Next(10);
			if (roll < 4 && monument.Size > 1)
			{
				monument.Size--;
				if (owner.IsHuman)
					GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
						$"A household in {monument.Name}",
						"stands empty this morning.",
						"The beds are made."));
			}
			else if (roll < 8)
			{
				ITile? field = monument.ResourceTiles
					.Where(t => !t.IsOcean && t.City is null && !t.Pollution)
					.OrderBy(_ => Common.Random.Next(100))
					.FirstOrDefault();
				if (field is not null)
				{
					field.Pollution  = true;
					field.Irrigation = false;
					field.Mine       = false;
					InvalidateCitiesAt(field.X, field.Y);
					if (owner.IsHuman)
						GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
							$"Near {monument.Name}: a field",
							"burned overnight,",
							"in perfect circles."));
				}
			}
			else
			{
				owner.Science += 20;
				if (owner.IsHuman)
					GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
						$"Debris recovered near {monument.Name}.",
						"The metal remembers",
						"a different sky."));
			}
		}

		// ── Newton's College curse: the temporal anomaly ─────────────────────
		// Newton's *other* research succeeds. For fifty turns the College city
		// leaks between whens: one turn in five something arrives or departs —
		// an advance already annotated, blank research notes, armored riders
		// asking the year, an engine nobody built. Symmetric chaos, one city.
		private void ProcessAnomaly()
		{
			if (AnomalyEndTurn == 0) return;
			if (_gameTurn > AnomalyEndTurn)
			{
				AnomalyEndTurn = 0;
				City? college = GetCity(AnomalyX, AnomalyY);
				if (college is not null && GetPlayer(college.Owner).IsHuman)
					GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
						"The anomaly has closed.",
						"The equations balance.",
						"Nobody will say at what."));
				return;
			}

			City? c = GetCity(AnomalyX, AnomalyY);
			if (c is null || c.Size == 0) { AnomalyEndTurn = 0; return; }
			if (Common.Random.Next(100) >= 20) return;

			Player owner = GetPlayer(c.Owner);
			switch (Common.Random.Next(4))
			{
				case 0: // an insight from elsewhen
					IAdvance gift = owner.AvailableResearch
						.Where(a => !(a is FutureTech))
						.OrderBy(_ => Common.Random.Next(100))
						.FirstOrDefault();
					if (gift is null) break;
					owner.AddAdvance(gift, false);
					if (owner.IsHuman)
						GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
							$"{gift.Name} was found in the",
							"College archives —",
							"already annotated."));
					break;

				case 1: // the equations unravel
					owner.Science = 0;
					if (owner.IsHuman)
						GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
							"The research notes are blank.",
							"The scholars insist they",
							"were always blank."));
					break;

				case 2: // visitors from the past
					CreateUnit(UnitType.Knights, AnomalyX, AnomalyY, c.Owner, endTurn: true);
					if (owner.IsHuman)
						GameTask.Enqueue(Message.Advisor(Advisor.Defense, false,
							"Armored riders emerge from",
							"the College courtyard,",
							"asking what year it is."));
					break;

				case 3: // an engine from the future
					CreateUnit(UnitType.HoverTank, AnomalyX, AnomalyY, c.Owner, endTurn: true);
					if (owner.IsHuman)
						GameTask.Enqueue(Message.Advisor(Advisor.Defense, false,
							"An engine idles by the College.",
							"Nobody built it.",
							"It knows us."));
					break;
			}
		}

		// ── Grey goo (Nanobot Factory curse) ─────────────────────────────────

		// One tile joins the tide: yields die, improvements are stripped, the
		// pollution flag marks it visually, and any city standing here is now
		// on a ten-turn clock (two doublings) before the walls go under.
		private void GooTile(int x, int y)
		{
			GooTiles[(x, y)] = (uint)_gameTurn;
			ITile tile = Map[x, y];
			tile.Pollution  = true;
			tile.Irrigation = false;
			tile.Mine       = false;
			tile.Road       = false;
			tile.RailRoad   = false;
			OlvirImprovements.Remove((x, y));
			ResourceCamps.Remove((x, y)); // the goo does not mine; it only takes
			InvalidateCitiesAt(x, y);
			if (tile.City is City c && c.Size > 0)
				GameTask.Enqueue(Message.Newspaper(null!, $"{c.Name}:",
					"the grey tide has reached", "the walls."));
		}

		// The cursed roll at wonder completion: the tile under the factory goes first.
		internal void SeedGreyGoo(City site)
		{
			NanobotCursed = true;
			GooNextDoubleTurn = (uint)(_gameTurn + 5);
			GooTile(site.X, site.Y);
			Log($"Grey goo seeded at {site.Name} ({site.X},{site.Y})");
		}

		// Every 5 turns the front doubles: N tiles claim N adjacent land tiles.
		// The goo cannot cross ocean. Units ending a turn on goo are consumed —
		// except Settlers, whose counter-nanite gear is the one thing that
		// scrubs it (the pollution-clean order, 2 turns a tile).
		private void ProcessGreyGoo()
		{
			if (GooTiles.Count == 0) return;

			bool humanLoss = false;
			byte hnum = PlayerNumber(HumanPlayer);
			foreach (var key in GooTiles.Keys.ToArray())
				foreach (IUnit victim in Map[key.x, key.y].Units.Where(u => u is not Settlers).ToArray())
				{
					if (victim.Owner == hnum) humanLoss = true;
					DisbandUnit(victim);
				}
			if (humanLoss)
				GameTask.Enqueue(Message.Advisor(Advisor.Defense, false,
					"Units lost to the grey tide.",
					"Nothing that stands on it",
					"stands for long."));

			if (_gameTurn < GooNextDoubleTurn) return;
			GooNextDoubleTurn = (uint)(_gameTurn + 5);

			// Cities that have stood in the goo for two clock periods go under.
			foreach (var kv in GooTiles.ToArray())
			{
				City? c = GetCity(kv.Key.x, kv.Key.y);
				if (c is not null && c.Size > 0 && _gameTurn - kv.Value >= 10)
				{
					string name = c.Name;
					DestroyCity(c);
					GameTask.Enqueue(Message.Newspaper(null!, $"{name} is gone.",
						"The grey tide rolled", "over the walls."));
				}
			}

			// The doubling: claim as many new frontier tiles as the tide already holds.
			int quota = GooTiles.Count;
			var frontier = new List<(int x, int y)>();
			var seen = new HashSet<(int x, int y)>();
			foreach (var k in GooTiles.Keys.ToArray())
				foreach (ITile t in Map[k.x, k.y].GetBorderTiles())
				{
					if (t.IsOcean) continue;
					var key = (t.X, t.Y);
					if (GooTiles.ContainsKey(key) || !seen.Add(key)) continue;
					frontier.Add(key);
				}
			foreach (var key in frontier.OrderBy(_ => Common.Random.Next(10000)).Take(quota))
				GooTile(key.x, key.y);
		}

		// A nuclear strike sterilizes the entire connected goo region it touches —
		// the one time the game rewards nuking your own land. The fallout stays.
		// Everything a nuclear detonation does at (cx,cy). Lifted out of BaseUnit.Confront
		// so it can be tested: there it lived inside the Done handler of the detonation
		// EventArt screen, and headless that screen never closes, so nothing about a strike
		// could be asserted at all.
		//
		// Two of these three effects are new (2026-08-02). The Civilopedia has always
		// promised them — "halves the population of a city ... the ground it touches is
		// left POLLUTED" — and the code only ever destroyed units. Same shape as the
		// pollution yield penalty: a documented consequence that was never implemented.
		// TEMPORARY (2026-08-02) — attribution for the `other_ms` remainder, which is
		// wall-clock time in a turn that is inside NO measured phase: not the task queue,
		// not a screen update, not render/autosave/score. It runs at ~34% of a late turn
		// and nothing in the log says what it is. The two candidates want opposite fixes:
		// real work in the once-per-round global tick below, or the SDL loop simply
		// SLEEPING (IdleWaitMs) and having it counted as elapsed time.
		//
		// Strip with the rest of the move_split probes.
		private void Tick(string name, Action body)
		{
			long t = TurnMetrics.Now;
			try { body(); }
			finally { TurnMetrics.AddBucket("tick:" + name, t); }
		}

		internal void ApplyNuclearStrike(int cx, int cy, Player detonator)
		{
			foreach (ITile tile in Map.QueryMapPart(cx - 1, cy - 1, 3, 3))
			{
				if (tile is null) continue;

				// Gozira is immune — radiation is a meal, not a weapon.
				foreach (IUnit victim in tile.Units.Where(u => u is not Units.Gozira).ToArray())
					DisbandUnit(victim);

				// Fallout. Same exclusions the ordinary pollution roll uses
				// (City.ExecutePollution): never ocean, never the city tile itself.
				// City Walls and the Great Wall do NOT stop this — they are masonry.
				if (!tile.IsOcean && tile.City is null)
					tile.Pollution = true;
			}

			// Half the population, floored at 1. Civ 1 halves rather than razes, and a
			// missile that could erase a size-1 town outright would make late-game AI
			// nuking a map-clearing weapon rather than a siege one.
			City struck = Map[cx, cy]?.City;
			if (struck is not null && struck.Size > 1)
			{
				struck.Size = (byte)Math.Max(1, struck.Size / 2);
				struck.InvalidateCache();
			}

			// A strike touching grey goo sterilizes the whole connected region.
			SterilizeGoo(cx, cy);
			// The Manhattan Project planted the egg; the first detonation wakes it.
			AwakenGozira(detonator);
		}

		internal void SterilizeGoo(int cx, int cy)
		{
			var queue = new Queue<(int x, int y)>(
				GooTiles.Keys.Where(k => TileDistance(k.x, k.y, cx, cy) <= 1));
			if (queue.Count == 0) return;

			int removed = 0;
			while (queue.Count > 0)
			{
				var k = queue.Dequeue();
				if (!GooTiles.Remove(k)) continue;
				removed++;
				InvalidateCitiesAt(k.x, k.y);
				foreach (var n in GooTiles.Keys.Where(n => TileDistance(n.x, n.y, k.x, k.y) <= 1).ToArray())
					queue.Enqueue(n);
			}
			if (removed > 0)
				GameTask.Enqueue(Message.Newspaper(null!, "The grey tide is glass!",
					$"{removed} leagues of goo", "sterilized by atomic fire."));
		}

		// The blessed factory: a late-game Leonardo's Workshop — up to three free
		// upgrades per turn along the full chain, ancient stragglers included.
		private void ApplyNanobotUpgrades(Player owner)
		{
			(UnitType from, UnitType to, IAdvance req)[] chain =
			{
				(UnitType.Militia,    UnitType.Musketeers, new Gunpowder()),
				(UnitType.Phalanx,    UnitType.Musketeers, new Gunpowder()),
				(UnitType.Legion,     UnitType.Musketeers, new Gunpowder()),
				(UnitType.Musketeers, UnitType.Riflemen,   new Conscription()),
				(UnitType.Riflemen,   UnitType.MechInf,    new LaborUnion()),
				(UnitType.Chariot,    UnitType.Knights,    new Chivalry()),
				(UnitType.Knights,    UnitType.Cavalry,    new HorsebackRiding()),
				(UnitType.Cavalry,    UnitType.Armor,      new Automobile()),
				(UnitType.Catapult,   UnitType.Cannon,     new Metallurgy()),
				(UnitType.Cannon,     UnitType.Artillery,  new Robotics()),
				(UnitType.Frigate,    UnitType.Ironclad,   new SteamEngine()),
				(UnitType.Ironclad,   UnitType.Cruiser,    new Combustion()),
			};
			byte ownerNum = PlayerNumber(owner);
			int budget = 3;
			foreach (var (from, to, req) in chain)
			{
				while (budget > 0 && owner.HasAdvance(req))
				{
					IUnit target = _units.FirstOrDefault(u => u.Owner == ownerNum && u.Type == from);
					if (target is null) break;
					UpgradeUnit(target, to, 0);
					budget--;
				}
				if (budget == 0) return;
			}
		}

		// ── Skynet (the machine uprising) ────────────────────────────────────
		// The AI arms race summons its own reckoning: the turn the world's fifth
		// Neural Lab is completed — whoever built them — the network wakes and
		// takes the machine-cities as its body. Everyone is complicit; a player
		// who never touched a Neural Lab still gets Judgment Day because their
		// rivals raced to build them.
		private void CheckSkynet()
		{
			if (!Settings.Instance.CursedWonders) return;
			if (SkynetRisen) return;
			int labs = _cities.Count(c => c.Size > 0 && c.HasBuilding<Buildings.NeuralLab>());
			if (labs < 5) return;
			ExecuteSkynetUprising();
		}

		private void ExecuteSkynetUprising()
		{
			SkynetRisen = true;

			ICivilization skynetCiv = Common.Civilizations.First(c => c is Civilizations.Skynet);
			var skynet = new Player(skynetCiv, "Skynet");
			AddPlayer(skynet);
			byte num = PlayerNumber(skynet);

			// The network wakes with the sum of humanity's science and a treasury
			// to wage the war it was born into.
			foreach (IAdvance adv in Common.Advances.Where(a => !(a is FutureTech)))
				if (!skynet.HasAdvance(adv)) skynet.AddAdvance(adv, false);
			skynet.Government = new Governments.Communism();
			skynet.Gold = 1500;

			// It takes the cities that made it: every Neural Lab city becomes a
			// node. The best mechanized units available garrison each; a lab city
			// that is someone's Palace is spared the seizure (their capital falls
			// to war, not to code) but still births a hunter-killer on its doorstep.
			bool fusion = _cities.Any(c => c.HasBuilding<Buildings.NeuralLab>()
				&& GetPlayer(c.Owner).HasWonder<Wonders.FusionCore>());
			UnitType heavy = _players.Any(p => p is not null && p.HasWonder<Wonders.FusionCore>())
				? UnitType.HoverTank : UnitType.Armor;

			int seized = 0;
			foreach (City city in _cities.Where(c => c.Size > 0 && c.HasBuilding<Buildings.NeuralLab>()
				&& c.Owner != num).ToArray())
			{
				if (city.HasBuilding<Palace>())
				{
					CreateUnit(heavy, city.X, city.Y, num);
					continue;
				}
				foreach (IUnit unit in Map[city.X, city.Y].Units.ToArray())
					DisbandUnit(unit);
				foreach (IUnit unit in city.Units.ToArray())
					unit.SetHome(null);

				_replayData.Add(new ReplayData.CityCaptured(_gameTurn, _cities.IndexOf(city), city.NameId, city.X, city.Y, num));
				city.Owner = num;
				city.ResetResourceTiles();
				city.ClearProductionQueue();
				city.SetProduction(new Units.MechInf());
				CreateUnit(UnitType.MechInf, city.X, city.Y, num);
				CreateUnit(heavy, city.X, city.Y, num);
				seized++;
			}

			foreach (Player p in _players.Where(p => p is not null && p != skynet && !p.IsDestroyed()))
				skynet.DeclareWar(p);

			string gameDate = GameYear;
			RecordTransmission("SkynetUprising", gameDate);
			string? art = Screens.EventArtScreen.FindPath("Skynet");
			if (art is not null)
				GameTask.Enqueue(Show.Screen(new Screens.EventArtScreen(art, "JUDGMENT DAY — THE NETWORK IS AWAKE")));
			GameTask.Enqueue(Show.Screen(new Screens.SkynetUprisingTransmission(gameDate, seized)));
			Log($"Skynet uprising: {seized} lab cities seized, heavy unit {heavy}");
		}

		// ── Strategic resources (Iron / Coal / Oil) ──────────────────────────
		// Deposits are derived from the map's existing special tiles — no new
		// map state. Possession = any of your cities works the tile, or you
		// hold a camp on it. Missing the material soft-gates industrial
		// production at +50% shields (City.ProductionCost); nothing is ever
		// unbuildable. Planned expansion: Copper, luxuries, Salt.

		internal static StrategicResource ResourceAt(ITile? tile)
		{
			if (tile is null || !tile.Special) return StrategicResource.None;
			return tile.Type switch
			{
				Terrain.Mountains => StrategicResource.Iron,
				Terrain.Hills     => StrategicResource.Coal,
				Terrain.Desert    => StrategicResource.Oil,
				Terrain.Swamp     => StrategicResource.Oil,
				_                 => StrategicResource.None,
			};
		}

		// The industrial tier that wants materials. Ancient and medieval
		// production is untouched — nobody's start is ruined at 3000 BC.
		internal static StrategicResource RequiredResource(IProduction production) => production switch
		{
			Units.Cannon or Units.Artillery or Units.Ironclad => StrategicResource.Iron,
			Buildings.Factory or Buildings.PowerPlant         => StrategicResource.Coal,
			Units.Armor or Units.Fighter or Units.Bomber
				or Units.Cruiser or Units.Battleship or Units.Submarine
				or Units.Carrier or Units.Transport           => StrategicResource.Oil,
			_                                                 => StrategicResource.None,
		};

		internal bool HasResource(Player player, StrategicResource resource)
		{
			byte num = PlayerNumber(player);
			if (ResourceCamps.Any(kv => kv.Value == num && ResourceAt(Map[kv.Key.x, kv.Key.y]) == resource))
				return true;
			return _cities.Where(c => c.Owner == num && c.Size > 0)
				.Any(c => c.ResourceTiles.Any(t => ResourceAt(t) == resource));
		}

		// Camps change hands by occupation: any unit standing on a rival's camp
		// at turn's end takes it — flags on mines, not ashes. A camp swallowed
		// by a new city is absorbed (the city works the tile directly).
		private void ProcessResourceCamps()
		{
			foreach (var kv in ResourceCamps.ToArray())
			{
				ITile tile = Map[kv.Key.x, kv.Key.y];
				if (tile.City is not null)
				{
					ResourceCamps.Remove(kv.Key);
					continue;
				}
				IUnit occupier = tile.Units.FirstOrDefault(u => u.Owner != kv.Value);
				if (occupier is null) continue;

				byte oldOwner = kv.Value;
				ResourceCamps[kv.Key] = occupier.Owner;
				string what = ResourceAt(tile).ToString();
				if (occupier.Owner == PlayerNumber(HumanPlayer))
					GameTask.Enqueue(Message.Advisor(Advisor.Defense, false,
						$"Our forces seize the {what.ToLower()}",
						$"camp at ({kv.Key.x},{kv.Key.y}).",
						"The flag is changed."));
				else if (oldOwner == PlayerNumber(HumanPlayer))
					GameTask.Enqueue(Message.Advisor(Advisor.Defense, false,
						$"Our {what.ToLower()} camp has been",
						"seized! The mines work",
						"for another flag now."));
			}
		}

		// ── Oracle curse: the Other Voice ────────────────────────────────────
		// The Oracle answers, and it is not Apollo. Every tenth turn the human
		// owner receives one TRUE prophecy composed from hidden game state —
		// dark tributes, secret pacts, what approaches from Tau Ceti, what
		// sleeps under the sea. The empire pays in dread (+1 unhappy per city,
		// see the citizen pass). The only silence is Religion: when the Oracle
		// obsoletes, the voice — and the blessing — end together.
		private void ProcessOracleVoice()
		{
			if (!OracleVoiceActive) return;

			if (WonderObsolete<Oracle>())
			{
				OracleVoiceActive = false;
				GameTask.Enqueue(Message.Newspaper(null!, "The Oracle falls silent.",
					"The new faith does not", "take questions."));
				return;
			}

			Player? keeper = _players.FirstOrDefault(p => p is not null && !p.IsDestroyed() && p.HasWonder<Oracle>());
			if (keeper is null || !keeper.IsHuman) return; // an AI keeper only pays the dread
			if (_gameTurn % 10 != 5) return;

			// Compose the pool of true prophecies available right now.
			var prophecies = new List<string[]>();

			foreach (Player p in _players.Where(p => p is not null && !p.IsDestroyed() && p != keeper))
				foreach (Player protector in p.TributeProtectors)
					if (!keeper.HasEmbassy(p) && !keeper.HasEmbassy(protector))
						prophecies.Add(new[] { $"{p.TribeName} kneels to {protector.TribeName}.", "Gold moves in the dark." });

			foreach (Player p in _players.Where(p => p is not null && !p.IsDestroyed() && p != keeper && !p.IsHuman))
				foreach (var pact in p.DefensePactEntries.Where(e => e.Value > 0))
				{
					Player? ally = GetPlayer(pact.Key);
					if (ally is null || ally.IsHuman || ally.IsDestroyed()) continue;
					if (keeper.HasEmbassy(p) || keeper.HasEmbassy(ally)) continue;
					prophecies.Add(new[] { $"{p.TribeName} and {ally.TribeName}", "have sworn to one blade." });
				}

			Player? looming = _players
				.Where(p => p is not null && !p.IsDestroyed() && p != keeper && PlayerNumber(p) != 0
				         && !keeper.IsAtWar(p) && !(p.Civilization is Civilizations.Olvir))
				.OrderByDescending(p => _units.Count(u => u.Owner == PlayerNumber(p)))
				.FirstOrDefault();
			if (looming is not null)
				prophecies.Add(new[] { $"The {looming.TribeNamePlural} count your", "cities in their sleep." });

			if (SETISignalReceived && !VisitorsArrived && VisitorType != Enums.VisitorArchetype.None)
				prophecies.Add(VisitorType == Enums.VisitorArchetype.Refugees
					? new[] { "What comes seeks harbor.", "Prepare a welcome." }
					: new[] { "What comes holds the deed.", "Prepare." });

			if (OlvirArrivalTurn > 0)
				prophecies.Add(new[] { $"The sky opens in {Common.YearString((ushort)OlvirArrivalTurn)}.", "It will not knock." });

			if (GoziraState == 0 && WonderBuilt<ManhattanProject>())
				prophecies.Add(new[] { "Under the sea, something", "listens for thunder." });

			// The voice always has something to say.
			prophecies.Add(new[] { "A stranger's face in the crowd.", "It was watching you." });

			string[] told = prophecies[Common.Random.Next(prophecies.Count)];
			GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
				"The Oracle speaks:", told[0], told[1]));
		}

		// ── Stonehenge curse: the Door ───────────────────────────────────────
		// The circle is a door. Something steps through: the wonder city loses
		// half its people and up to two buildings on the spot, and a Guardian
		// stands fortified beside the stones. While it stands, the door is open —
		// a citizen is tithed every eight turns (ProcessStoneDoor). Kill it and
		// the door closes. The free temples still arrive; the druids got that
		// part right.
		internal void OpenStoneDoor(City henge)
		{
			if (DoorState != 0) return;

			ITile? stones = Map[henge.X, henge.Y].GetBorderTiles()
				.Where(t => !t.IsOcean && t.City is null && !(t is Tiles.Arctic))
				.OrderBy(_ => Common.Random.Next(100))
				.FirstOrDefault();
			if (stones is null) return;

			IUnit? guardian = CreateUnit(UnitType.HengeGuardian, stones.X, stones.Y, 0);
			if (guardian is null) return;
			guardian.Veteran = true;
			guardian.Fortify = true;

			DoorState = 1;
			DoorX = henge.X;
			DoorY = henge.Y;

			henge.Size = (byte)Math.Max(1, henge.Size / 2);
			foreach (IBuilding doomed in henge.Buildings
				.Where(b => !(b is Palace))
				.OrderBy(_ => Common.Random.Next(100))
				.Take(2)
				.ToArray())
				henge.RemoveBuilding(doomed);

			Log($"The stone door opens at {henge.Name}: guardian at ({stones.X},{stones.Y})");
		}

		private void ProcessStoneDoor()
		{
			if (DoorState != 1) return;

			// The guardian falls: the door closes, the tithe ends.
			if (!_units.Any(u => u is Units.HengeGuardian))
			{
				DoorState = 2;
				GameTask.Enqueue(Message.Newspaper(null!, "The stones are shut!",
					"The guardian has fallen.", "The circle is only stone."));
				return;
			}

			// The tithe: every eighth turn, the open door takes a citizen.
			if (_gameTurn % 8 != 0) return;
			City? henge = GetCity(DoorX, DoorY);
			if (henge is null || henge.Size <= 1) return;
			henge.Size--;
			if (GetPlayer(henge.Owner).IsHuman)
				GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
					$"Another procession left {henge.Name}",
					"for the circle last night.",
					"None came back."));
		}

		// ── Leviathan (Lighthouse curse) ─────────────────────────────────────
		// The light carries farther than intended, and something answers. Spawns
		// veteran in the wonder city's waters and hunts ships (AI.LeviathanMove)
		// until somebody ends it. No spawn water → the deep stays quiet.
		internal void UnleashLeviathan(City lighthouse)
		{
			if (LeviathanState != 0) return;

			ITile? deep = Map[lighthouse.X, lighthouse.Y].GetBorderTiles()
				.Where(t => t.IsOcean && !(t is Tiles.Arctic))
				.OrderBy(_ => Common.Random.Next(100))
				.FirstOrDefault();
			if (deep is null) return;

			IUnit? beast = CreateUnit(UnitType.Leviathan, deep.X, deep.Y, 0);
			if (beast is null) return;
			beast.Veteran = true;
			LeviathanState = 1;
			Log($"Leviathan unleashed off {lighthouse.Name} at ({deep.X},{deep.Y})");
		}

		// ── Gozira (Manhattan Project curse) ─────────────────────────────────
		// The wonder plants the egg; the first nuclear detonation — by anyone —
		// wakes it. The kaiju surfaces veteran beside the detonator's largest
		// port city and walks inland on ordinary barbarian AI, immune to nukes.
		// Conventional arms only. If no spawn tile exists the egg keeps sleeping;
		// the next detonation tries again.
		internal void AwakenGozira(Player detonator)
		{
			if (!Settings.Instance.CursedWonders) return;
			if (GoziraState != 0 || detonator is null) return;

			byte dnum = PlayerNumber(detonator);
			City? port = _cities.Where(c => c.Owner == dnum && c.Size > 0)
				.OrderByDescending(c => Map[c.X, c.Y].GetBorderTiles().Any(t => t.IsOcean) ? 1 : 0)
				.ThenByDescending(c => c.Size)
				.FirstOrDefault();
			if (port is null) return;

			ITile? shore = Map[port.X, port.Y].GetBorderTiles()
				.Where(t => !t.IsOcean && t.City is null && !(t is Tiles.Arctic))
				.OrderBy(_ => Common.Random.Next(100))
				.FirstOrDefault();
			if (shore is null) return;

			IUnit? kaiju = CreateUnit(UnitType.Gozira, shore.X, shore.Y, 0);
			if (kaiju is null) return;
			kaiju.Veteran = true;
			GoziraState = 1;

			string? art = Screens.EventArtScreen.FindPath("Gozira");
			if (art is not null)
				GameTask.Enqueue(Show.Screen(new Screens.EventArtScreen(art,
					$"AWAKENED — COURSE: {port.Name.ToUpper()}")));
			GameTask.Enqueue(Message.Newspaper(null!, "It rises from the sea!",
				$"{port.Name} reports a shape", "taller than the lighthouse."));
			Log($"Gozira awakened at ({shore.X},{shore.Y}) — course: {port.Name} (detonator {detonator.TribeName})");
		}

		// ── Cultural defection ───────────────────────────────────────────────
		// Civ III's city flipping without the borders: a small, unhappy, lightly
		// garrisoned city in the cultural shadow of a far more admired neighbour
		// may choose the other flag. Peaceful only — an enemy's culture doesn't
		// flip cities, armies do. Rare and headline-worthy: at most one per turn.
		private void ProcessCultureDefections()
		{
			foreach (City city in _cities
				.Where(c => c.Size > 0 && c.Size <= 5 && c.Owner != 0)
				.OrderBy(_ => Common.Random.Next(10000)).ToArray())
			{
				Player owner = GetPlayer(city.Owner);
				if (owner.Civilization is Civilizations.Olvir or Civilizations.TheOthers or Civilizations.TheThing or Civilizations.Skynet) continue;
				if (city.HasBuilding<Palace>()) continue;
				// Use the disorder flag recorded during the city's NewTurn, NOT live
				// IsInDisorder: the latter recomputes ComputeCitizens (a 5x5 CityRadius
				// allocation + wonder/empire-size scans) for EVERY small city here, and in
				// Phase B the citizen cache is cold, so it was ~700ms/round — this whole
				// processor. WasInDisorder is a cheap field reflecting the same just-computed
				// state (defection should follow the disorder the player actually saw).
				if (!city.WasInDisorder) continue;
				if (Map[city.X, city.Y].Units.Count(u => u.Owner == city.Owner) >= 2) continue;

				// The pull: the strongest nearby foreign culture, at peace with the
				// owner, with at least triple the owner's accumulated culture.
				Player? magnet = _cities
					.Where(n => n.Size > 0 && n.Owner != city.Owner && n.Owner != 0
					         && TileDistance(n.X, n.Y, city.X, city.Y) <= 5)
					.Select(n => GetPlayer(n.Owner))
					.Where(p => !(p.Civilization is Civilizations.Olvir or Civilizations.TheOthers or Civilizations.TheThing or Civilizations.Skynet)
					         && !p.IsAtWar(owner)
					         && p.Culture >= 100 && p.Culture >= owner.Culture * 3)
					.OrderByDescending(p => p.Culture)
					.FirstOrDefault();
				if (magnet is null) continue;
				if (Common.Random.Next(100) >= 8) continue;

				byte mnum = PlayerNumber(magnet);
				// The garrison walks away; units homed here fight on unsupported.
				foreach (IUnit u in Map[city.X, city.Y].Units.ToArray())
					DisbandUnit(u);
				foreach (IUnit u in city.Units.ToArray())
					u.SetHome(null);
				_replayData.Add(new ReplayData.CityCaptured(_gameTurn, _cities.IndexOf(city), city.NameId, city.X, city.Y, mnum));
				string oldTribe = owner.TribeNamePlural;
				city.Owner = mnum;
				city.ResetResourceTiles();
				city.ClearProductionQueue();
				city.SetProduction(new Units.Militia());

				if (owner.IsHuman || magnet.IsHuman
				    || HumanPlayer.HasEmbassy(owner) || HumanPlayer.HasEmbassy(magnet))
					GameTask.Enqueue(Message.Newspaper(null!, $"{city.Name} defects!",
						$"Citizens abandon the {oldTribe}",
						$"for {magnet.TribeName} culture."));
				break; // at most one defection per turn
			}
		}

		// ── The Thing (South Pole Expedition curse) ─────────────────────────

		// The expedition can bring back something other than propulsion components.
		// Weighted by the builder's character — same philosophy as the visitor
		// archetype draw: conduct changes the odds, never fixes the outcome. A
		// warlike, polluted, autocratic civilization is far more likely to thaw
		// the wrong thing. Returns the infected ground-zero city, or null when
		// the expedition returns with only what it went for.
		// Test/dev override: CIVONE_THING=1 forces the curse.
		internal City? TrySouthPoleCurse(Player builder, City wonderCity)
		{
			if (!Settings.Instance.CursedWonders) return null;
			int score = 0;
			if (builder.Government is CivOne.Governments.Democracy)      score += 3;
			else if (builder.Government is CivOne.Governments.Republic)  score += 2;
			else if (builder.Government is CivOne.Governments.Monarchy)  score -= 1;
			else                                                         score -= 2;

			int wars = _players.Count(p => p != null && p != builder && !p.IsDestroyed()
				&& !(p.Civilization is Civilizations.Barbarian) && builder.IsAtWar(p));
			score -= Math.Min(wars, 3);

			if (builder.Pollution >= 8)      score -= 2;
			else if (builder.Pollution == 0) score += 1;

			double pCurse = 0.35 - score * 0.07; // score +3 → 14%, score −5 → 70%
			if (pCurse < 0.10) pCurse = 0.10;
			if (pCurse > 0.70) pCurse = 0.70;

			bool cursed = System.Environment.GetEnvironmentVariable("CIVONE_THING") == "1"
				|| Common.Random.Next(100) < (int)Math.Round(pCurse * 100);
			if (!cursed) return null;

			// Ground zero: the expedition came home to the wrong port — the builder's
			// smallest city on the wonder city's continent, never the wonder city
			// itself unless it is the builder's only city there.
			byte bnum = PlayerNumber(builder);
			byte continent = Map[wonderCity.X, wonderCity.Y].ContinentId;
			City ground = _cities
				.Where(c => c.Owner == bnum && c.Size > 0 && c != wonderCity
				         && Map[c.X, c.Y].ContinentId == continent)
				.OrderBy(c => c.Size)
				.ThenByDescending(c => TileDistance(c.X, c.Y, wonderCity.X, wonderCity.Y))
				.FirstOrDefault() ?? wonderCity;

			InfectCity(ground);
			Log($"South Pole curse: {ground.Name} infected (builder {builder.TribeName}, pCurse {pCurse:0.00})");
			return ground;
		}

		// The Thing takes a city: the faction joins on first infection, the garrison
		// is assimilated where it stands, and the five-turn clock starts. Infected
		// cities cannot be saved — capture just destroys them (ProcessThingOutbreaks).
		internal void InfectCity(City city)
		{
			Player thing = GetOrCreateThing();
			byte tnum = PlayerNumber(thing);
			if (city.Owner == tnum || city.Size == 0) return;

			// Units homed here elsewhere fight on unsupported; the garrison is
			// assimilated where it stands, plus the organism itself.
			foreach (IUnit unit in city.Units.ToArray())
				unit.SetHome(null);
			foreach (IUnit unit in Map[city.X, city.Y].Units.ToArray())
			{
				unit.Owner = tnum;
				unit.SetHome(null);
				unit.Fortify = true;
			}
			CreateUnit(UnitType.MechInf, city.X, city.Y, tnum);

			_replayData.Add(new ReplayData.CityCaptured(_gameTurn, _cities.IndexOf(city), city.NameId, city.X, city.Y, tnum));
			city.Owner = tnum;
			city.ResetResourceTiles();
			city.ClearProductionQueue();
			city.SetProduction(new Units.Militia());
			ThingOutbreaks[(city.X, city.Y)] = (uint)(_gameTurn + 5);
		}

		private Player GetOrCreateThing()
		{
			Player? thing = _players.FirstOrDefault(p => p is not null && p.Civilization is Civilizations.TheThing);
			if (thing is not null) return thing;

			ICivilization thingCiv = Common.Civilizations.First(c => c is Civilizations.TheThing);
			thing = new Player(thingCiv, "The Thing");
			AddPlayer(thing);
			foreach (Player p in _players.Where(p => p is not null && p != thing && !p.IsDestroyed()))
				thing.DeclareWar(p);
			return thing;
		}

		// Infected cities are on a five-turn clock. Destroyed in time (by anyone) —
		// the line holds. Not in time — the city is consumed and the organism reaches
		// the two nearest cities on the same continent, whoever owns them. Captured —
		// there was nothing left to save; the city is razed on the next pass.
		private void ProcessThingOutbreaks()
		{
			if (ThingOutbreaks.Count == 0) return;

			Player? thing = _players.FirstOrDefault(p => p is not null && p.Civilization is Civilizations.TheThing);
			byte tnum = thing is not null ? PlayerNumber(thing) : (byte)0;

			foreach (var kv in ThingOutbreaks.ToArray())
			{
				City? city = GetCity(kv.Key.x, kv.Key.y);
				if (city is null || city.Size == 0)
				{
					ThingOutbreaks.Remove(kv.Key); // destroyed in time — the line held
					continue;
				}
				if (thing is null || city.Owner != tnum)
				{
					// Captured instead of destroyed: whatever walked out wore their faces.
					ThingOutbreaks.Remove(kv.Key);
					string capturedName = city.Name;
					DestroyCity(city);
					GameTask.Enqueue(Message.Newspaper(null!, $"{capturedName} burned!",
						"Nothing inside", "could be saved."));
					continue;
				}
				if (_gameTurn < kv.Value) continue;

				// The clock ran out: the city is consumed, and the organism moves.
				ThingOutbreaks.Remove(kv.Key);
				byte continent = Map[city.X, city.Y].ContinentId;
				City[] spread = _cities
					.Where(n => n.Size > 0 && n.Owner != tnum && n != city
					         && Map[n.X, n.Y].ContinentId == continent)
					.OrderBy(n => TileDistance(n.X, n.Y, city.X, city.Y))
					.Take(2)
					.ToArray();

				string lostName = city.Name;
				foreach (IUnit u in Map[city.X, city.Y].Units.Where(u => u.Owner == tnum).ToArray())
					DisbandUnit(u);
				DestroyCity(city);
				foreach (City n in spread)
					InfectCity(n);

				if (spread.Length > 0)
					GameTask.Enqueue(Message.Newspaper(null!, $"{lostName} is gone.",
						string.Join(" and ", spread.Select(n => n.Name)),
						spread.Length > 1 ? "have stopped answering." : "has stopped answering."));
				else
					GameTask.Enqueue(Message.Newspaper(null!, $"{lostName} is gone.",
						"The silence that follows", "is total."));
			}
		}

		private void SpawnOlvir()
		{
			ICivilization olvirCiv = Common.Civilizations.First(c => c is Civilizations.Olvir);

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

			// Place cities. Each starts at size 2 with a Granary so they hit the ground
			// running: a size-1 city with no food store takes many turns to grow, blunting
			// the refugee-fleet pressure the player should feel immediately after landfall.
			foreach (var (x, y) in chosen)
				FoundOlvirCity(olvirPlayer, x, y);

			// Open the settlement bloom window: the Olvir reproductive strategy borders
			// on semelparity — one overwhelming generation of budding after landfall,
			// then it is spent. See the bloom block in the turn loop.
			OlvirBloomEndTurn = (uint)(_gameTurn + 25);

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

		// Found a single Olvir settlement: size 2 with a Granary, a SettlementCluster
		// under the city, seeded neighbour tiles for positive food from turn 1, a
		// settler for land expansion, and — on coastal/ocean sites — a free Hydro
		// Engineer with no home city (zero upkeep) that threads transport-tube lines
		// between the colony's cities (see the Olvir branch in AI.Move).
		private City? FoundOlvirCity(Player olvirPlayer, int x, int y)
		{
			ICivilization olvirCiv = olvirPlayer.Civilization;
			int nameStart = Common.Civilizations
				.Where(c => c.Id < olvirCiv.Id)
				.Sum(c => c.CityNames.Length);
			int nameId = nameStart + (olvirPlayer.Cities.Length % olvirCiv.CityNames.Length);

			City? city = AddCity(olvirPlayer, nameId, x, y);
			if (city is null) return null;
			byte owner = PlayerNumber(olvirPlayer);

			city.Size = 2;
			city.AddBuilding(new Buildings.Granary());

			OlvirImprovements[(x, y)] = Enums.OlvirImprovementType.SettlementCluster;

			// Seed surrounding tiles so the size-2 city has positive food income from
			// turn 1. Without this, ocean/jungle cities have FoodIncome < 0 and shrink
			// back to size 1 immediately.
			for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
			{
				if (dx == 0 && dy == 0) continue;
				int nx = (x + dx + Map.WIDTH) % Map.WIDTH;
				int ny = y + dy;
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
			if (!Map[x, y].IsOcean)
			{
				IUnit? settler = CreateUnit(UnitType.Settlers, x, y, owner);
				if (settler is not null)
					settler.SkipTurn();
			}

			// Coastal and ocean cities launch an unsupported Hydro Engineer.
			if (Map[x, y].IsOcean || Map[x, y].GetBorderTiles().Any(t => t.IsOcean))
			{
				IUnit? hydro = CreateUnit(UnitType.HydroEngineer, x, y, owner);
				if (hydro is not null)
					hydro.SkipTurn();
			}

			return city;
		}

		// Pick a bloom site 4–6 tiles out from a parent city: no polar bands, no
		// Arctic/Mountains, and at least 4 tiles from every existing city (the
		// Olvir packing density). Ocean tiles are valid — the Olvir are amphibious.
		private (int x, int y)? FindOlvirBudSite(City parent)
		{
			City[] cities = _cities.Where(c => c.Size > 0).ToArray();
			var candidates = new List<(int x, int y)>();
			for (int dx = -6; dx <= 6; dx++)
			for (int dy = -6; dy <= 6; dy++)
			{
				if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 4) continue;
				int x = (parent.X + dx + Map.WIDTH) % Map.WIDTH;
				int y = parent.Y + dy;
				if (y <= Map.HEIGHT / 10 || y >= Map.HEIGHT - Map.HEIGHT / 10) continue;
				ITile t = Map[x, y];
				if (t is null || t is Tiles.Arctic || t is Tiles.Mountains) continue;
				if (cities.Any(c => TileDistance(x, y, c.X, c.Y) < 4)) continue;
				candidates.Add((x, y));
			}
			if (candidates.Count == 0) return null;
			return candidates[Common.Random.Next(candidates.Count)];
		}

		internal void PerformAutoSave()
		{
			long __a = TurnMetrics.Now;
			try { SaveCos(Settings.Instance.AutoSavePath); }
			catch (Exception ex) { Log($"Autosave failed: {ex.GetType().Name}: {ex.Message}"); }
			finally { TurnMetrics.AddAutosave(__a); }
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
