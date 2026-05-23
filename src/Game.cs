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

		// Outcome tier of the probe mission: 0=Destroyed 1=Partial 2=Identified 3=TechTransfer 4=Pact
		internal int ProbeOutcomeTier;

		// Olvir land-use improvements keyed by map tile (x, y).
		internal readonly Dictionary<(int x, int y), Enums.OlvirImprovementType> OlvirImprovements = new();

		// Dome path: which player (owner byte) is assigned to which dome wonder component.
		// Populated when the Tau Ceti approach warning fires.
		internal readonly Dictionary<byte, Enums.Wonder> DomeAssignments = new();

		// Set when any player builds the first dome component (hard exclusivity gate).
		internal bool DomePathCommitted => BuiltWonders.Any(w => w is Wonders.IDomeComponent);

		// Set when all five dome components are built — triggers victory sequence.
		internal bool DomeComplete => _domeVictoryFired || _domeFiveComponents.All(w => WonderBuilt(w));

		private bool _domeVictoryFired = false;

		private static readonly Wonders.IWonder[] _domeFiveComponents =
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
		private byte[,] _firstExplorer;
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

		private IUnit _lastMovedUnit = null;
		private int _sameUnitMoveCount = 0;

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
		public bool Sound { get; set; }
		public bool CivilopediaText { get; set; }
		public bool EndOfTurn { get; set; }
		public bool InstantAdvice { get; set; }

		public bool EnemyMoves { get; set; }
		public bool Palace { get; set; }
		public bool Circuses { get; set; } = true;
		public bool Barricades { get; set; } = true;

		public void SetAdvanceOrigin(IAdvance advance, Player player)
		{
			if (_advanceOrigin.ContainsKey(advance.Id))
				return;
			byte playerNumber = 0;
			if (player is not null)
				playerNumber = PlayerNumber(player);
			_advanceOrigin.Add(advance.Id, playerNumber);
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
		
		internal Player HumanPlayer { get; set; }
		
		internal Player CurrentPlayer => _players[_currentPlayer];

		internal ReplayData[] GetReplayData() => _replayData.ToArray();
		internal T[] GetReplayData<T>() where T : ReplayData => _replayData.Where(x => x is T).Select(x => (x as T)).ToArray();
		internal void AddReplayEvent(ReplayData entry) => _replayData.Add(entry);

		private void PlayerDestroyed(object sender, EventArgs args)
		{
			Player player = (sender as Player);

			ICivilization destroyed = player.Civilization;
			ICivilization destroyedBy = Game.CurrentPlayer.Civilization;
			if (destroyedBy == destroyed) destroyedBy = Game.GetPlayer(0).Civilization;

			_replayData.Add(new ReplayData.CivilizationDestroyed(_gameTurn, destroyed.Id, destroyedBy.Id));

			if (player.IsHuman)
			{
				// TODO: Move Game Over code here
				return;
			}

			// Before 0 AD, respawn destroyed AI civs using their alternate civilization variant.
			// Each civ has a "buddy" with Id offset by 7 (e.g. Romans Id=1 <-> Mongols Id=8).
			// If the buddy hasn't already been destroyed this game, spawn it in the same player slot.
			if (!(destroyed is Barbarian) && Common.TurnToYear(_gameTurn) < 0)
			{
				byte playerSlot = (byte)destroyed.PreferredPlayerNumber;
				int buddyId = destroyed.Id >= 8 ? destroyed.Id - 7 : destroyed.Id + 7;
				bool buddyDestroyed = _replayData.OfType<ReplayData.CivilizationDestroyed>()
					.Any(rd => rd.DestroyedId == buddyId);
				ICivilization buddyCiv = Common.Civilizations.FirstOrDefault(c => c.Id == buddyId);
				if (!buddyDestroyed && buddyCiv is not null)
				{
					_players[playerSlot] = new Player(buddyCiv);
					_players[playerSlot].Destroyed += PlayerDestroyed;
					AddStartingUnits(playerSlot);
				}
			}

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

		internal Player GetPlayer(byte number)
		{
			if (_players.Count < number)
				return null;
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
					bool isDesertOrPlains = tile.Type == Terrain.Desert || tile.Type == Terrain.Plains;
					Map.ChangeTileType(tile.X, tile.Y, isDesertOrPlains ? Terrain.Desert : Terrain.Plains);
					tile.Irrigation = false;
				}
			}

			GameTask.Enqueue(Show.EventArt("globalwarming", "Global warming! Icecaps melt."));
		}

		public void EndTurn()
		{
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

				// Fire the satellite-anomaly intelligence report once Apollo is built
				if (!MapRevealedNotified && WonderBuilt<ApolloProgram>())
				{
					MapRevealedNotified = true;
					SouthPoleExpeditionLog.EnsureConfigFile();
					string gameYear = GameYear;
					RecordTransmission("SouthPoleIntel", gameYear);
					GameTask.Enqueue(Show.Screen(new SouthPoleIntelReport(gameYear)));
				}

				// Fire the SETI signal transmission 5 turns after SETI Program is built
				if (SETISignalTurn > 0 && _gameTurn >= SETISignalTurn)
				{
					SETISignalTurn = 0;
					SETISignalReceived = true;
					if (VisitorType == VisitorArchetype.None)
						VisitorType = VisitorArchetype.Refugees; // TEMP: force Olvir path for testing
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
						if (phase == 3)
							GameTask.Enqueue(Show.Screen(new EventArtScreen(
								EventArtScreen.FindPath("OlvirInSpace"), "VISUAL CONTACT — TAU CETI")));
						GameTask.Enqueue(Show.Screen(new Screens.ProbeInterimTransmission(gameDate, phase)));
					}
					else if (ProbeInterimPhase == 3 && _gameTurn >= resultTurn)
					{
						ProbeInterimPhase = 4;
						string gameDate = GameYear;
						int tier = ProbeOutcomeTier;
						string[] techNames = null;
						if (ProbeGrantedAdvanceIds.Length > 0)
						{
							var grants = ProbeGrantedAdvanceIds
								.Select(id => HumanPlayer.Advances.Concat(HumanPlayer.AvailableResearch)
									.FirstOrDefault(a => a.Id == id))
								.Where(a => a is not null)
								.ToArray();
							techNames = grants.Select(a => (a as ICivilopedia)?.Name).ToArray();
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

				// Olvir arrival scene
				if (OlvirArrivalTurn > 0 && _gameTurn >= OlvirArrivalTurn)
				{
					OlvirArrivalTurn = 0;
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
						EventArtScreen.FindPath("MeetTheOlvir"), artCaption)));
					GameTask.Enqueue(Show.Screen(new Screens.OlvirArrivalTransmission(gameDate, VisitorType, probeWasSent, landfallYear)));
				}

				// Check for dome victory (all five components built)
				if (!_domeVictoryFired && _domeFiveComponents.All(w => WonderBuilt(w)))
				{
					_domeVictoryFired = true;
					HumanPlayer.AwardMilestone(150);
					DecisionLogger.EndGame(HumanPlayer.Score, "Dome", humanWon: true, turns: _gameTurn);
					string gameDate = GameYear;
					RecordTransmission("DomeComplete", gameDate);
					int domeFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Dome Victory");
					var doneScreen = new Screens.DomeCompleteTransmission(gameDate, VisitorType);
					var finalScore = new Screens.Reports.FinalScore("Dome Victory");
					GameTask.Enqueue(Show.Screen(doneScreen));
					GameTask.Enqueue(Show.Screen(finalScore));
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
					if (_players[p] == HumanPlayer)
					{
						PlaySound("wintune");
						GameTask.Enqueue(Message.Newspaper(null, "Our spaceship has", "launched!", $"Arrival: {eta}"));
					}
					else
					{
						GameTask.Enqueue(Message.Advisor(Advisor.Foreign, false,
							$"The {_players[p].TribeNamePlural}",
							"have launched a spaceship!",
							$"Arrival: {eta}"));
					}
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
							GameTask.Enqueue(Show.EventArt("spaceshiparrived",
								"Your colony ship reaches Alpha Centauri — but the game is far from over."));
							SpaceshipArrivalTurn[PlayerNumber(HumanPlayer)] = 0;
						}
						else
						{
							for (int p = 1; p < _players.Count; p++)
							{
								if (SpaceshipArrivalTurn[p] != bestArrival) continue;
								GameTask.Enqueue(Message.Newspaper(null, $"The {_players[p].TribeNamePlural}", "have reached", "Alpha Centauri!"));
								SpaceshipArrivalTurn[p] = 0;
								break;
							}
						}
					}
					else
					{
						if (humanWins)
						{
							PlaySound("wintune");
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
								GameTask.Enqueue(Message.Newspaper(null, $"The {_players[p].TribeNamePlural}", "have reached", "Alpha Centauri!"));
								break;
							}
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
						PlaySound("wintune");
						DecisionLogger.EndGame(HumanPlayer.Score, "Score", humanWon: true, turns: _gameTurn);
						int scoreFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Score Victory");
						GameTask.Enqueue(Message.Newspaper(null, "The year is 2100!", $"Your score: {HumanPlayer.Score}", "You lead the world!"));
						GameTask scoreFt;
						GameTask.Enqueue(scoreFt = Show.Screen(new FinalScore("Score Victory")));
						scoreFt.Done += (s, a) => EndSequence.ChainAfterFinal(scoreFame, () => Runtime.Quit());
					}
					else
					{
						GameTask.Enqueue(Turn.GameOver(HumanPlayer));
					}
					return;
				}

				PerformAutoSave();

				IEnumerable<City> disasterCities = _cities.OrderBy(o => Common.Random.Next(0,1000)).Take(2).AsEnumerable();
				foreach (City city in disasterCities)
					city.Disaster();

				if (Barbarian.IsSeaSpawnTurn)
				{
					ITile tile = Barbarian.SeaSpawnPosition;
					if (tile is not null)
					{
						foreach (UnitType unitType in Barbarian.SeaSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}

				if (Barbarian.IsLandSpawnTurn)
				{
					ITile tile = Barbarian.LandSpawnPosition;
					if (tile is not null)
					{
						foreach (UnitType unitType in Barbarian.LandSpawnUnits)
							CreateUnit(unitType, tile.X, tile.Y, 0, false);
					}
				}
			}

			if (!_players.Any(x => Game.PlayerNumber(x) != 0 && x != Human && !x.IsDestroyed()))
			{
				PlaySound("wintune");
				DecisionLogger.EndGame(HumanPlayer.Score, "Conquest", humanWon: true, turns: _gameTurn);
				int conquestFame = EndSequence.SaveAndGetIndex(HumanPlayer, "Conquest Victory");
				GameTask conquest;
				GameTask.Enqueue(Message.Newspaper(null, "Your civilization", "has conquered", "the entire planet!"));
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
			IUnit unit = ActiveUnit;
			if (CurrentPlayer == HumanPlayer)
			{
				if (unit is not null && !unit.Goto.IsEmpty)
				{
					ITile next = Common.GotoStep(unit);
					if (next is null)
					{
						unit.Goto = Point.Empty;
						return;
					}
					// Don't let a GoTo move initiate war — stop peacefully at the border.
					Player owner = HumanPlayer;
					Player nextCityOwner = (next.City is not null && next.City.Owner != unit.Owner) ? GetPlayer(next.City.Owner) : null;
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

		internal City AddCity(Player player, int nameId, int x, int y)
		{
			if (_cities.Any(c => c.X == x && c.Y == y))
				return null;

			byte ownerNum = PlayerNumber(player);
			City city = new City(ownerNum)
			{
				X = (byte)x,
				Y = (byte)y,
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
			if ((Map[x, y] is Desert) || (Map[x, y] is Grassland) || (Map[x, y] is Hills) || (Map[x, y] is Plains) || (Map[x, y] is River))
			{
				Map[x, y].Irrigation = true;
			}
			if (!Map[x, y].RailRoad)
			{
				Map[x, y].Road = true;
			}
			_cities.Add(city);
			Game.UpdateResources(city.Tile);
			if (Game.Started)
				_replayData.Add(new ReplayData.CityBuilt(_gameTurn, city.Owner, _cities.Count - 1, nameId, x, y));
			return city;
		}

		public void DestroyCity(City city)
		{
			int cityIdx = _cities.IndexOf(city);
			_replayData.Add(new ReplayData.CityDestroyed(_gameTurn, cityIdx, city.NameId, city.X, city.Y));
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
		
		internal City GetCity(int x, int y)
		{
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x-= Map.WIDTH;
			if (y < 0) return null;
			if (y >= Map.HEIGHT) return null;
			return _cities.Where(c => c.X == x && c.Y == y && c.Size > 0).FirstOrDefault();
		}
		
		internal static IUnit PeekUnit(UnitType type) => CreateUnit(type, 0, 0);

		private static IUnit CreateUnit(UnitType type, int x, int y)
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
				default: return null;
			}
			unit.X = x;
			unit.Y = y;
			unit.MovesLeft = unit.Move;
			return unit;
		}

		public IUnit CreateUnit(UnitType type, int x, int y, byte owner, bool endTurn = false)
		{
			IUnit unit = CreateUnit((UnitType)type, x, y);
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
			_instance._units.Add(unit);
			return unit;
		}
		
		internal IUnit[] GetUnits(int x, int y)
		{
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x-= Map.WIDTH;
			if (y < 0) return null;
			if (y >= Map.HEIGHT) return null;
			// Use the raw index field, not the ActiveUnit property, to avoid the
			// circular: ActiveUnit → IsAboard → tile.Units → GetUnits → ActiveUnit
			IUnit cur = (_activeUnit >= 0 && _activeUnit < _units.Count) ? _units[_activeUnit] : null;
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

		// Assign one dome component to each surviving player (round-robin, shuffled).
		// Called once when the Tau Ceti approach warning fires.
		private void AssignDomeComponents()
		{
			if (DomeAssignments.Count > 0) return; // already assigned

			Player[] survivors = _players.Where(p => !p.IsDestroyed()).ToArray();
			if (survivors.Length == 0) return;

			// Shuffle the five components so the assignment isn't always the same
			var components = _domeFiveWonderIds.ToList();
			for (int i = components.Count - 1; i > 0; i--)
			{
				int j = Common.Random.Next(i + 1);
				(components[i], components[j]) = (components[j], components[i]);
			}

			for (int i = 0; i < components.Count; i++)
				DomeAssignments[PlayerNumber(survivors[i % survivors.Length])] = components[i];
		}

		// Returns the dome component assigned to this player, or null if none / not yet assigned.
		internal Enums.Wonder? GetDomeAssignment(Player player)
		{
			byte id = PlayerNumber(player);
			return DomeAssignments.TryGetValue(id, out var w) ? w : (Enums.Wonder?)null;
		}

		// ── Olvir arrival ────────────────────────────────────────────────────

		// Chebyshev distance on a horizontally-wrapping map.
		private static int TileDistance(int x1, int y1, int x2, int y2)
		{
			int dx = Math.Abs(x1 - x2);
			if (dx > Map.WIDTH / 2) dx = Map.WIDTH - dx;
			return Math.Max(dx, Math.Abs(y1 - y2));
		}

		private void SpawnOlvir()
		{
			ICivilization olvirCiv = Common.Civilizations.First(c => c is Civilizations.Olvir);

			// Compute where Olvir city names start in the flat AllCityNames array.
			int nameStart = Common.Civilizations
				.Where(c => c.Id < olvirCiv.Id)
				.Sum(c => c.CityNames.Length);

			// All non-ocean non-edge land tiles that don't already host a city.
			bool CityFree(int x, int y) => !_cities.Any(c => c.X == x && c.Y == y && c.Size > 0);
			bool IsLand(int x, int y) => !(Map[x, y] is Ocean) && y > 0 && y < Map.HEIGHT - 1;
			bool CoastalTile(ITile t) => t.GetBorderTiles().Any(b => b is Ocean);

			const int MinSpread = 12; // Chebyshev distance between Olvir cities
			var chosen = new List<(int x, int y)>();

			bool FarEnough(int x, int y) =>
				chosen.All(p => TileDistance(x, y, p.x, p.y) >= MinSpread);

			// 1) Jungle city first.
			var jungles = Enumerable.Range(0, Map.WIDTH)
				.SelectMany(x => Enumerable.Range(1, Map.HEIGHT - 2).Select(y => (x, y)))
				.Where(t => Map[t.x, t.y] is Jungle && IsLand(t.x, t.y) && CityFree(t.x, t.y))
				.OrderBy(_ => Common.Random.Next(10000))
				.ToList();

			(int x, int y) jungleCity = jungles.FirstOrDefault(t => FarEnough(t.x, t.y));
			if (jungleCity == default) jungleCity = jungles.FirstOrDefault(); // fallback: any jungle

			if (jungleCity != default)
				chosen.Add(jungleCity);

			// 2) Three more cities: prefer coastal, then any habitable land.
			IEnumerable<(int x, int y)> CoastalFirst() =>
				Enumerable.Range(0, Map.WIDTH)
					.SelectMany(x => Enumerable.Range(1, Map.HEIGHT - 2).Select(y => (x, y)))
					.Where(t => IsLand(t.x, t.y) && CityFree(t.x, t.y) && FarEnough(t.x, t.y)
					         && !chosen.Any(p => p.x == t.x && p.y == t.y))
					.OrderByDescending(t => CoastalTile(Map[t.x, t.y]) ? 1 : 0)
					.ThenBy(_ => Common.Random.Next(10000));

			foreach (var (x, y) in CoastalFirst().Take(4 - chosen.Count))
				chosen.Add((x, y));

			if (chosen.Count == 0) return; // safety: nothing found

			// 3) Create the Olvir player.
			var olvirPlayer = new Player(olvirCiv, "The Council");
			AddPlayer(olvirPlayer);
			byte owner = PlayerNumber(olvirPlayer);

			// 4) Place cities, settlement overlays, and settlers.
			for (int i = 0; i < chosen.Count; i++)
			{
				int nameId = nameStart + (i % olvirCiv.CityNames.Length);
				City city = AddCity(olvirPlayer, nameId, chosen[i].x, chosen[i].y);
				if (city is null) continue;

				OlvirImprovements[(chosen[i].x, chosen[i].y)] = Enums.OlvirImprovementType.SettlementCluster;

				IUnit settler = CreateUnit(UnitType.Settlers, chosen[i].x, chosen[i].y, owner);
				if (settler is not null)
					settler.SkipTurn();
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
			catch { }
		}

		public void UpgradeUnit(IUnit unit, UnitType targetType, int cost)
		{
			if (unit is null || !_units.Contains(unit)) return;
			Player player = GetPlayer(unit.Owner);
			if (player.Gold < cost) return;

			player.Gold -= (short)cost;

			IUnit upgraded = CreateUnit(targetType, unit.X, unit.Y);
			if (upgraded is null) return;
			upgraded.Owner   = unit.Owner;
			upgraded.Veteran = unit.Veteran;
			upgraded.SetHome(unit.Home);
			upgraded.SkipTurn();

			_units.Remove(unit);
			_units.Add(upgraded);
		}

		public void DisbandUnit(IUnit unit)
		{
			IUnit activeUnit = ActiveUnit;

			if (unit is null) return;
			if (!_units.Contains(unit)) return;
			if (unit.Tile is Ocean && unit is IBoardable)
			{
				int totalCargo = unit.Tile.Units.Where(u => u is IBoardable).Sum(u => (u as IBoardable).Cargo) - (unit as IBoardable).Cargo;
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

			if (_units.Contains(activeUnit))
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
		
		public IUnit ActiveUnit
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
					if (CurrentPlayer == HumanPlayer && !EndOfTurn && !GameTask.Any() && (Common.TopScreen is GamePlay))
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

		public static bool Started => (_instance is not null);
		
		private static Game _instance;
		public static Game Instance
		{
			get
			{
				if (_instance is null)
				{
					Log("ERROR: Game instance does not exist");
				}
				return _instance;
			}
		}
	}
}
