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
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Wonders;

using Gov = CivOne.Governments;

namespace CivOne
{
	public class Player : BaseInstance, ITurn
	{
		private readonly ICivilization _civilization;
		private readonly string? _customLeaderName;
		private readonly string _tribeName, _tribeNamePlural;

		private readonly bool[,] _explored = new bool[Map.WIDTH, Map.HEIGHT];
		private readonly bool[,] _visible = new bool[Map.WIDTH, Map.HEIGHT];
		private readonly List<byte> _advances = new();
		private readonly List<byte> _embassies = new();
		private readonly HashSet<byte> _warWith = new();
		private readonly Dictionary<byte, int> _peaceTreaty  = new(); // AI won't declare war for N turns
		private readonly Dictionary<byte, int> _attitudeBonus = new(); // AI acceptance boosted for N turns
		private readonly Dictionary<byte, int> _defensePact  = new(); // mutual defense pact for N turns (kept symmetric)
		// Tribute relationships. _tributeTo: I pay N gold/turn to this player (my protector).
		// _tributeFrom: this player pays me N gold/turn (I'm their protector). The two maps
		// are always kept in sync between the paired players via EstablishTribute and
		// DissolveTribute. Tribute pays a self-renewing peace and is documented further
		// near those methods below.
		private readonly Dictionary<byte, int> _tributeTo   = new();
		private readonly Dictionary<byte, int> _tributeFrom = new();

		private short _anarchy = 0;
		private short _gold;
		// Map zoom level, basis points (1000 = 100%, default). Persisted in COS save
		// so reloading a save returns the player to their chosen zoom. Clamping to the
		// preset range happens in MapZoomSettings.NormalizeBasisPoints — read it
		// through that helper rather than via this field directly.
		public int MapZoomBasisPoints { get; set; } = 1000;
		private IAdvance? _currentResearch = null;
		private int _futureTechs = 0;

		// Transient: shields contributed this turn by Infrastructure-Bond donor cities under
		// Adam Smith's Trading House. Collected during city NewTurn passes, distributed and
		// zeroed in Player.NewTurn. Never persisted — only nonzero between the city loop and
		// the player loop within a single turn.
		internal int BondPool = 0;

		public event EventHandler? Destroyed;

		internal int CityNamesSkipped = 0;
		internal short AnarchyTurnsLeft { get => _anarchy; set => _anarchy = value; }

		internal short StartX { get; set; }
		
		internal bool AnarchyDespotism => Game.Started && (Government is Anarchy || Government is Despotism);

		internal bool MonarchyCommunist => Game.Started && (Government is Gov.Monarchy || Government is Gov.Communism);

		internal bool RepublicDemocratic => Game.Started && (Government is Republic || Government is Gov.Democracy);

		public ICivilization Civilization => _civilization;
		
		public string LeaderName => _customLeaderName ?? _civilization.Leader.Name;
		public string TribeName => _tribeName;
		public string TribeNamePlural => _tribeNamePlural;
		public string Capital => Game.GetCities().FirstOrDefault(x => this == x.Owner && x.HasBuilding<Palace>())?.Name ?? "NONE";

		public byte Handicap { get; internal set; }

		// Normally AI is exposed only for non-human players. In Autopilot mode the human
		// slot gets one too, so unit moves and other "AI-driven" decisions self-resolve.
		internal AI? AI => (!IsHuman || Settings.Instance.Autopilot) ? AI.Instance(this) : null;
		
		private IGovernment _government = null!;
		public IGovernment Government
		{
			get => _government;
			internal set
			{
				if (value is null) return;
				_government = value;
				InvalidateCityCaches();
			}
		}

		private int _luxuriesRate = 0, _taxesRate = 5, _scienceRate = 5;
		public int LuxuriesRate
		{
			get => _luxuriesRate;
			set
			{
				int diff = _luxuriesRate - value;
				_luxuriesRate = value;
				_scienceRate += diff;
				InvalidateCityCaches();
			}
		}
		public int TaxesRate
		{
			get => _taxesRate;
			set
			{
				int diff = _taxesRate - value;
				_taxesRate = value;
				_scienceRate += diff;
				InvalidateCityCaches();
			}
		}
		public int ScienceRate => _scienceRate;

		internal void InvalidateCityCaches()
		{
			if (!Game.Started) return;
			foreach (City city in Cities)
				city.InvalidateCache();
		}

		public void Revolt()
		{
			_anarchy = (short)((HasWonder<Pyramids>() && !Game.WonderObsolete<Pyramids>()) ? 0 : 4 - (Game.GameTurn % 4) - 1);
			Government = new Anarchy();
			if (!IsHuman) return;
			GameTask.Enqueue(Message.Newspaper(null, $"The {Game.Instance.HumanPlayer.TribeNamePlural} are", "revolting! Citizens", "demand new govt."));
		}

		public bool IsHuman => (Game.Instance.HumanPlayer == this);

		public City[] Cities => Game.Instance.GetCities().Where(c => this == c.Owner && c.Size > 0).ToArray();

		public int Population => Cities.Sum(c => c.Population);

		public int FutureTechs => _futureTechs;
		internal void SetFutureTechs(int count) => _futureTechs = count;

		private int _milestoneScore;
		public int MilestoneScore => _milestoneScore;
		internal void AwardMilestone(int points) => _milestoneScore += points;
		internal void SetMilestoneScore(int score) => _milestoneScore = score;

		internal int ExplorationCredits;

		// Culture: accumulated cultural weight of the empire's buildings and wonders
		// (City.CultureRate, accrued in NewTurn). Read by diplomacy (AIAccepts), the
		// visitor-archetype draw, and the cultural-defection check; the Evaluators
		// arc will read it too.
		private int _culture;
		public int Culture => _culture;
		public int CultureRate => Cities.Sum(c => c.CultureRate);
		internal void SetCulture(int culture) => _culture = culture; // COS load

		public int Score =>
			Population / 5000 +                      // 2 pts per 10,000 people
			_advances.Count * 3 +                    // 3 pts per advance
			Cities.Sum(c => c.Wonders.Length) * 4 +  // 4 pts per wonder
			_futureTechs * 5 +                       // 5 pts per future tech
			ExplorationCredits / 10 +                // 1 pt per 10 tiles first explored
			_milestoneScore;                         // narrative milestone bonuses

		public short Gold
		{
			get
			{
				return _gold;
			}
			internal set
			{
				if (value < 0)
				{
					//TODO: Implement sold improvements task
					value = 0;
				}
				if (value > 30000)
					value = 30000;
				_gold = value;
			}
		}

		internal short ScienceCost
		{
			get
			{
				// Difficulty only slows the human player; AI always pays the Chieftain rate.
				int diffFactor = IsHuman ? Game.Instance.Difficulty + 3 : 3;
				short cost = (short)(diffFactor * 2 * (_advances.Count() + 1) * (Common.TurnToYear(Game.Instance.GameTurn) > 0 ? 2 : 1));
				if (cost < 12)
					return 12;
				return cost;
			}
		}
		
		public short Science { get; internal set; }

		public int Pollution => Cities.Sum(c => c.SmokeStacks);

		public void AddAdvance(IAdvance? advance, bool setOrigin = true)
		{
			if (advance is null) return;
			if (advance is FutureTech)
			{
				_futureTechs++;
				return;
			}
			if (Game.Started && Game.CurrentPlayer.CurrentResearch?.Id == advance.Id)
				GameTask.Enqueue(new TechSelect(Game.CurrentPlayer));
			_advances.Add(advance.Id);
			if (Game.Started)
			{
				Game.Instance.AddReplayEvent(new ReplayData.TechDiscovered(Game.GameTurn, Game.PlayerNumber(this), (advance as ICivilopedia).Name));
				InvalidateCityCaches();
			}
			if (!setOrigin) return;
			Game.Instance.SetAdvanceOrigin(advance, this);
		}

		public void DeleteAdvance(IAdvance advance) => _advances.RemoveAll(x => x == advance.Id);
		
		public string LatestAdvance
		{
			get
			{
				if (_advances.Count == 0)
					return "Irrigation";
				return Reflect.GetAdvances().First(a => a.Id == _advances.Last()).Name;
			}
		}

		public IAdvance[] Advances => _advances.Select(a => Common.Advances.First(x => x.Id == a)).ToArray();

		// Per-type id cache so HasAdvance<T>() is a plain id lookup. The old form
		// materialized the full Advances array on every call, and the tile-yield
		// methods (City.FoodValue/TradeValue) call this per worked tile.
		private static class AdvanceId<T> where T : IAdvance
		{
			public static readonly byte Id = Common.Advances.FirstOrDefault(a => a is T)?.Id ?? byte.MaxValue;
		}

		public bool HasAdvance<T>() where T : IAdvance => _advances.Contains(AdvanceId<T>.Id);

		public bool HasAdvance(IAdvance? advance) => (advance is null || _advances.Contains(advance.Id));

		public Player[] Embassies => _embassies.Select(e => Game.Players.FirstOrDefault(p => e == Game.PlayerNumber(p))).Where(p => p is not null).ToArray();

		public bool HasEmbassy(Player player) => _embassies.Any(e => e == Game.PlayerNumber(player));

		public void EstablishEmbassy(Player player) => EstablishEmbassy(Game.PlayerNumber(player));

		internal void EstablishEmbassy(byte playerNumber)
		{
			if (_embassies.Contains(playerNumber)) return;
			_embassies.Add(playerNumber);
		}

		public bool IsAtWar(Player player) => _warWith.Contains(Game.PlayerNumber(player));

		internal void SetPeaceTreaty(Player other, int turns)  => _peaceTreaty[(byte)Game.PlayerNumber(other)]  = turns;
		internal bool HasPeaceTreaty(Player other)             => _peaceTreaty.TryGetValue((byte)Game.PlayerNumber(other),  out int t) && t > 0;
		internal void SetAttitudeBonus(Player other, int turns) => _attitudeBonus[(byte)Game.PlayerNumber(other)] = turns;
		internal bool HasAttitudeBonus(Player other)            => _attitudeBonus.TryGetValue((byte)Game.PlayerNumber(other), out int t) && t > 0;
		// Serial generosity accumulates: extend the existing window instead of
		// overwriting it, capped so a lavish dowry can't buy goodwill forever.
		// (SetAttitudeBonus still overwrites — tribute/pact renewals rely on that.)
		internal void AddAttitudeBonus(Player other, int turns)
		{
			byte k = (byte)Game.PlayerNumber(other);
			int existing = _attitudeBonus.TryGetValue(k, out int t) && t > 0 ? t : 0;
			_attitudeBonus[k] = Math.Min(200, existing + turns);
		}
		internal void SetDefensePact(Player other, int turns)  => _defensePact[(byte)Game.PlayerNumber(other)]  = turns;
		internal bool HasDefensePact(Player other)             => _defensePact.TryGetValue((byte)Game.PlayerNumber(other),  out int t) && t > 0;

		// Byte-keyed overloads for the COS load path, which runs inside the Game
		// constructor — Game.Instance does not exist yet there, so the Player-typed
		// setters above (which resolve numbers via Game.PlayerNumber) would NRE.
		internal void SetPeaceTreaty(byte playerNumber, int turns)   => _peaceTreaty[playerNumber]   = turns;
		internal void SetAttitudeBonus(byte playerNumber, int turns) => _attitudeBonus[playerNumber] = turns;
		internal void SetDefensePact(byte playerNumber, int turns)   => _defensePact[playerNumber]   = turns;

		// Enumeration accessors for the COS save layer. The dictionaries are private
		// state; the save loop in Game.Cos.cs uses these to write a snapshot, and
		// reloads them by replaying SetPeaceTreaty/SetAttitudeBonus per entry.
		internal IEnumerable<KeyValuePair<byte, int>> PeaceTreatyEntries  => _peaceTreaty;
		internal IEnumerable<KeyValuePair<byte, int>> AttitudeBonusEntries => _attitudeBonus;
		internal IEnumerable<KeyValuePair<byte, int>> DefensePactEntries  => _defensePact;

		// ── tribute ─────────────────────────────────────────────────────────────────
		// A tribute pact is established when a militarily outclassed civ sues for survival
		// from a stronger neighbour with whom it has an embassy. The weaker civ pays a
		// fixed annual sum in gold; in exchange the protector accepts peace and refuses
		// to declare war for as long as the tribute is paid. The pact dissolves if the
		// payer can't afford the gold (gold falls below the annual due) or if either side
		// is destroyed.
		//
		// Internal accessor maps are kept paired between the two players: EstablishTribute
		// writes both _tributeTo on the payer and _tributeFrom on the protector; the same
		// for DissolveTribute. Saving persists only _tributeTo per player; the inverse map
		// is reconstructed on load.

		internal bool PaysTributeTo(Player protector) => _tributeTo.ContainsKey((byte)Game.PlayerNumber(protector));
		internal int  TributeAmountTo(Player protector) => _tributeTo.TryGetValue((byte)Game.PlayerNumber(protector), out int g) ? g : 0;
		internal IEnumerable<Player> TributeProtectors => _tributeTo.Keys.Select(k => Game.Instance.GetPlayer(k)).Where(p => p is not null);
		internal IEnumerable<Player> TributePayers     => _tributeFrom.Keys.Select(k => Game.Instance.GetPlayer(k)).Where(p => p is not null);

		// Establish a tribute relationship. Caller is the *payer*; `protector` is the
		// stronger civ. annualGold is locked in for the duration of the pact. The pact
		// also installs a 100-turn renewable peace treaty in both directions; we re-up
		// it each turn the tribute flows so the protector is permanently barred from
		// declaring war as long as payment continues.
		internal void EstablishTribute(Player protector, int annualGold)
		{
			byte myIdx = (byte)Game.PlayerNumber(this);
			byte prIdx = (byte)Game.PlayerNumber(protector);
			if (myIdx == 0 || prIdx == 0 || myIdx == prIdx) return;  // no barbarian tribute, no self
			_tributeTo[prIdx]            = annualGold;
			protector._tributeFrom[myIdx] = annualGold;
			MakePeace(protector);
			SetPeaceTreaty(protector, 100);
			protector.SetPeaceTreaty(this, 100);
			SetAttitudeBonus(protector, 100);
			protector.SetAttitudeBonus(this, 100);
		}

		// COS load-path restore: writes the paired tribute maps directly by player
		// number. Runs inside the Game constructor, where Game.Instance is not yet
		// set, so EstablishTribute (which resolves numbers via Game.PlayerNumber)
		// cannot be used. Its peace-treaty/attitude side effects are not replayed
		// here either — those countdowns are persisted and restored separately.
		internal void RestoreTribute(byte payerNumber, Player protector, byte protectorNumber, int annualGold)
		{
			_tributeTo[protectorNumber]         = annualGold;
			protector._tributeFrom[payerNumber] = annualGold;
		}

		// End tribute, e.g. payer ran out of gold or one side destroyed. Doesn't re-declare
		// war by itself; the protector is just no longer barred from doing so by this map.
		internal void DissolveTribute(Player protector)
		{
			_tributeTo.Remove((byte)Game.PlayerNumber(protector));
			protector._tributeFrom.Remove((byte)Game.PlayerNumber(this));
		}

		internal void SetAtWar(byte playerNumber, bool atWar)
		{
			if (atWar) _warWith.Add(playerNumber);
			else _warWith.Remove(playerNumber);
		}

		public void DeclareWar(Player enemy)
		{
			byte enemyNumber = Game.PlayerNumber(enemy);
			byte ownNumber = Game.PlayerNumber(this);

			// Barbarians (player 0) are always hostile — no formal war state needed
			if (ownNumber == 0 || enemyNumber == 0) return;

			// Olvir refugees seek coexistence and never initiate war
			if (Civilization is Civilizations.Olvir) return;

			if (_warWith.Contains(enemyNumber)) return;
			if (_peaceTreaty.TryGetValue(enemyNumber, out int pt) && pt > 0) return;

			_warWith.Add(enemyNumber);
			enemy._warWith.Add(ownNumber);

			// Aggressor bookkeeping for the economic-dominance streak: only wars the
			// human starts break it — being dragged in by a pact doesn't count.
			if (this == Human && !_honoringPacts)
				Game.HumanStartedWars.Add(enemyNumber);

			// Break all trade routes between the two civs
			foreach (City city in Game.GetCities().Where(c => c.Owner == ownNumber))
				city.RemoveTradeRoutesTo(enemy);
			foreach (City city in Game.GetCities().Where(c => c.Owner == enemyNumber))
				city.RemoveTradeRoutesTo(this);

			// Notify the human player
			if (this == Human)
				GameTask.Insert(Message.Advisor(Advisor.Foreign, true, $"You have declared", $"war on the {enemy.TribeNamePlural}!"));
			else if (enemy == Human)
				GameTask.Insert(Message.Advisor(Advisor.Foreign, true, $"The {TribeNamePlural}", "have declared war on us!"));

			// Mutual defense pacts: everyone bound to the victim joins against the
			// aggressor — automatically, the human included; the treaty was signed.
			// One hop only: declarations made while honoring pacts don't trigger
			// further pacts, so blocs can't cascade into an accidental world war.
			// An ally's peace treaty with the aggressor wins (DeclareWar's early
			// return) — treaty-bound partners sit the war out.
			if (!_honoringPacts)
			{
				_honoringPacts = true;
				try
				{
					foreach (Player ally in Game.Players.Where(p => p is not null && p != this && p != enemy
						&& !p.IsDestroyed() && p.HasDefensePact(enemy) && !p.IsAtWar(this)))
					{
						ally.DeclareWar(this);
						if (!ally.IsAtWar(this)) continue; // blocked (peace treaty, Olvir, …)
						if (ally == Human)
							GameTask.Insert(Message.Advisor(Advisor.Foreign, true,
								$"We honor our pact with",
								$"the {enemy.TribeNamePlural}!"));
						else if (this == Human)
							GameTask.Insert(Message.Advisor(Advisor.Foreign, true,
								$"The {ally.TribeNamePlural} honor their",
								$"pact with the {enemy.TribeNamePlural}!"));
					}
				}
				finally { _honoringPacts = false; }
			}
		}

		// Reentrancy guard for pact honoring (single-threaded game loop): while an
		// ally is being pulled into a war by a pact, its own declaration must not
		// recursively pull in further pacts.
		private static bool _honoringPacts;

		public void MakePeace(Player enemy)
		{
			byte enemyNumber = Game.PlayerNumber(enemy);
			byte ownNumber = Game.PlayerNumber(this);
			_warWith.Remove(enemyNumber);
			enemy._warWith.Remove(ownNumber);

			// A settled war is no longer held against the human's economic streak.
			if (this == Human) Game.HumanStartedWars.Remove(enemyNumber);
			else if (enemy == Human) Game.HumanStartedWars.Remove(ownNumber);
		}

		public IAdvance? CurrentResearch
		{
			get => _currentResearch;
			set => _currentResearch = value;
		}

		public IEnumerable<IAdvance> AvailableResearch
		{
			get
			{
				// Post-contact advances (alien biology, transit conduits, …) require having
				// actually met the visitors — not just detecting their SETI signal. Detecting
				// the signal lets you prepare (probe, dome); arrival lets you study them.
				bool contacted = Game.Started && (Game.Instance?.VisitorsArrived ?? false);
				bool any = false;
				foreach (IAdvance advance in Common.Advances.Where(a => !_advances.Contains(a.Id) && !(a is FutureTech)))
				{
					if (advance is Advances.BasePostContactAdvance pc && !pc.AvailablePreContact && !contacted) continue;
					if (advance.RequiredTechs.Length > 0 && !advance.RequiredTechs.All(a => _advances.Contains(a.Id))) continue;
					any = true;
					yield return advance;
				}
				if (!any)
					yield return new FutureTech();
			}
		}

		public IEnumerable<IGovernment> AvailableGovernments
		{
			get
			{
				bool allGovernments = !Game.WonderObsolete<Pyramids>() && HasWonder<Pyramids>();
				foreach (IGovernment government in Reflect.GetGovernments().Where(g => g.Id > 0))
				{
					if (!allGovernments && !HasAdvance(government.RequiredTech)) continue;
					yield return government; 
				}
			}
		}

		private bool UnitAvailable(IUnit unit)
		{
			// Determine if the unit is obsolete
			if (_advances.Any(a => unit.ObsoleteTech is not null && unit.ObsoleteTech.Id == a))
				return false;
			
			// Require Manhattan Project to be built for Nuclear unit
			if ((unit is Nuclear) && !Game.Instance.WonderBuilt<ManhattanProject>())
				return false;

			// The fusion war machine is unlocked by the builder's OWN Fusion Core wonder.
			if ((unit is HoverTank || unit is FusionInf) && !HasWonder<FusionCore>())
				return false;

			// The kaiju is not yours. It is nobody's. Neither is the thing in the
			// sea, nor the one standing in the stones.
			if (unit is Units.Gozira || unit is Units.Leviathan || unit is Units.HengeGuardian)
				return false;
			
			// Determine if the unit requires a tech
			if (unit.RequiredTech is null)
				return true;
			
			// Determine if the Player has the required tech
			if (_advances.Any(a => unit.RequiredTech.Id == a))
				return true;
			
			return false;
		}

		private bool BuildingAvailable(IBuilding building)
		{
			if (building is Colosseum && !Game.Instance.Circuses)
				return false;
			if (building is CityWalls && !Game.Instance.Barricades)
				return false;

			if (building is ISpaceShip)
			{
				// Spaceship is a response to the Tau Ceti threat — not available until
				// the approach warning has fired and the decision is contextualized.
				if (!Game.Instance.SETISignalReceived || Game.Instance.DomeAssignments.Count == 0)
					return false;
				// Requires Apollo Program
				if (!Game.Instance.WonderBuilt<ApolloProgram>())
					return false;
				// No new SS parts once launched
				if (Game.SpaceshipLaunchTurn[Game.PlayerNumber(this)] != 0)
					return false;
			}

			// Determine if the building requires a tech
			if (building.RequiredTech is null)
				return true;
			
			// Determine if the Player has the required tech
			if (_advances.Any(a => building.RequiredTech.Id == a))
				return true;
			
			return false;
		}

		private bool WonderAvailable(IWonder wonder)
		{
			// Determine if the wonder has already been built
			if (Game.Instance.BuiltWonders.Any(w => w.Id == wonder.Id))
				return false;

			// South Pole Expedition requires the Apollo Program to be built first
			if (wonder is Wonders.SouthPoleExpedition && !Game.Instance.WonderBuilt<Wonders.ApolloProgram>())
				return false;

			// Interstellar Probe is only available once the SETI signal has been received
			if (wonder is Wonders.InterstellarProbe && !Game.Instance.SETISignalReceived)
				return false;

			byte owner = (byte)this;
			if (wonder is Wonders.IDomeComponent)
			{
				// Dome components only become available once the Tau Ceti approach warning has
				// fired and dome assignments have been distributed across civilizations.
				if (Game.Instance.DomeAssignments.Count == 0)
					return false;

				// AI players each build only their assigned component(s), which spreads the
				// dome across civilizations. The human may build ANY of the five: when the
				// assigned AI civs stall, can't be reached, or get wiped out, that is the only
				// way the dome ever gets completed.
				if (!IsHuman && !Game.Instance.GetDomeAssignments(this).Any(w => wonder.Id == (byte)w))
					return false;

				// Spaceship launch does not block dome — both paths can coexist.
			}
			if (wonder is Buildings.ISpaceShip)
			{
				if (Cities.Any(c => c.HasDomeWonder()))
					return false;
			}

			// Secondary prerequisite checks (wonders that need two techs)
			if (wonder is Wonders.MarcoPoloVoyage && !HasAdvance<MapMaking>()) return false;
			if (wonder is Wonders.ZhengHeVoyage  && !HasAdvance<Writing>())   return false;

			// Determine if the building requires a tech
			if (wonder.RequiredTech is null)
				return true;

			// Determine if the Player has the required tech
			if (_advances.Any(a => wonder.RequiredTech.Id == a))
				return true;

			return false;
		}
		
		public bool HasWonder<T>() where T : IWonder => Cities.Any(c => c.HasWonder<T>());

		public bool ProductionAvailable(IProduction production)
		{
			if (production is IUnit)
				return UnitAvailable((production as IUnit)!);
			if (production is IBuilding)
				return BuildingAvailable((production as IBuilding)!);
			if (production is IWonder)
				return WonderAvailable((production as IWonder)!);
			return true;
		}

		private bool _destroyed = false;
		public bool IsDestroyed()
		{
			if (this == 0) return false;
			if (_destroyed) return true;
			if (Cities.Length == 0 && !Game.GetUnits().Any(x => this == x.Owner && (x is Settlers && x.Home is null)))
			{
				while (true)
				{
					IUnit unit = Game.GetUnits().FirstOrDefault(x => this == x.Owner);
					if (unit is null) break;
					Game.DisbandUnit(unit);
				}
				_destroyed = true;
				// Don't re-fire if this destruction was already recorded in a previous session
				bool alreadyRecorded = Game.GetReplayData<ReplayData.CivilizationDestroyed>()
					.Any(rd => rd.DestroyedId == Civilization.Id);
				if (!alreadyRecorded)
					Destroyed?.Invoke(this, EventArgs.Empty);
				return true;
			}
			return false;
		}

		public void Explore(int x, int y, int range = 1, bool sea = false, bool noCorners = false)
		{
			for (int relX = -range; relX <= range; relX++)
			for (int relY = -range; relY <= range; relY++)
			{
				if (noCorners && Math.Abs(relX) == range && Math.Abs(relY) == range) continue;
				int xx = x + relX;
				int yy = y + relY;
				if (yy < 0 || yy >= Map.HEIGHT) continue;
				while (xx < 0) xx += Map.WIDTH;
				while (xx >= Map.WIDTH) xx -= Map.WIDTH;
				if (sea && !Map[xx, yy].IsOcean && (Math.Abs(relX) > 1 || Math.Abs(relY) > 1))
					continue;
				if (!_visible[xx, yy] && Game.Started)
				{
					var gameInst = Game.Instance;
					if (gameInst.ClaimTile(xx, yy, gameInst.PlayerNumber(this)))
						ExplorationCredits++;
				}
				_visible[xx, yy] = true;
				// Mark every tile in the seen radius as "explored", not just the unit's tile.
				// Without this, MergeVisibility (used by map-trade diplomacy) only shares the
				// path a unit walked rather than what it actually saw.
				_explored[xx, yy] = true;
			}
		}

		// Raw visibility, without the Apollo-Program full-map override that Visible()
		// applies. Used by the COS save layer so the override isn't baked into saves.
		internal bool RawVisible(int x, int y) => _visible[x, y];

		public bool Visible(int x, int y)
		{
			if (y < 0 || y >= Map.HEIGHT) return false;
			while (x < 0) x += Map.WIDTH;
			while (x >= Map.WIDTH) x -= Map.WIDTH;
			if (Game.WonderBuilt<ApolloProgram>()) return true;
			return _visible[x, y];
		}

		// Fraction of land tiles this player has explored, 0.0 to 1.0. Used by the AI
		// production planner to stop queueing Explorers once the map is mostly known —
		// otherwise large empires keep building scouts deep into the late game (8.8% of
		// late-game builds in the analytics we did 2026-06-06). Iterates the whole
		// _visible array but it's a 64000-bool sweep at Epic size — cheap relative to
		// the rest of one PlanProduction pass.
		internal double ExploredLandFraction
		{
			get
			{
				// Apollo reveals the whole map to everyone (see Visible), but _visible
				// still only records tiles this civ actually walked — so without this
				// the fraction understates by everything Apollo granted, and the AI
				// keeps building scouts for a map it can already see in full.
				if (Game.WonderBuilt<ApolloProgram>()) return 1.0;

				int land = 0, seen = 0;
				for (int y = 0; y < Map.HEIGHT; y++)
				for (int x = 0; x < Map.WIDTH; x++)
				{
					ITile t = Map.Instance[x, y];
					if (t is null || t.IsOcean) continue;
					land++;
					if (_visible[x, y]) seen++;
				}
				return land == 0 ? 1.0 : (double)seen / land;
			}
		}

		// Fraction of our HOME CONTINENTS' land that we have seen. Explorers are land
		// units, so world-wide exploration is the wrong measure of whether there is
		// anything left for them to walk to: a civ on one continent of an Earth map
		// can never reach 70% of the world's land on foot, so a global test never
		// closes and the civ keeps building explorers for the whole game.
		//
		// Cached per turn — this walks the map, and the production planner asks once
		// per city per turn.
		private int _continentSeenTurn = -1;
		private double _continentSeenFraction;
		internal double ExploredHomeContinentFraction
		{
			get
			{
				if (Game.WonderBuilt<ApolloProgram>()) return 1.0;   // see ExploredLandFraction
				if (_continentSeenTurn == (int)Game.Instance.GameTurn) return _continentSeenFraction;
				_continentSeenTurn = (int)Game.Instance.GameTurn;

				HashSet<byte> home = new HashSet<byte>();
				foreach (City c in Cities)
				{
					ITile? ct = Map.Instance[c.X, c.Y];
					if (ct is not null) home.Add(ct.ContinentId);
				}
				if (home.Count == 0) return _continentSeenFraction = 1.0;

				int land = 0, seen = 0;
				for (int y = 0; y < Map.HEIGHT; y++)
				for (int x = 0; x < Map.WIDTH; x++)
				{
					ITile t = Map.Instance[x, y];
					if (t is null || t.IsOcean || !home.Contains(t.ContinentId)) continue;
					land++;
					if (_visible[x, y]) seen++;
				}
				return _continentSeenFraction = (land == 0 ? 1.0 : (double)seen / land);
			}
		}

		public bool Visible(ITile? tile)
		{
			if (tile is null) return false;
			return Visible(tile.X, tile.Y);
		}

		public bool Visible(ITile tile, Direction direction)
		{
			if (tile is null) return false;
			return Visible(tile.GetBorderTile(direction));
		}

		internal void RevealTiles(IEnumerable<ITile> tiles)
		{
			foreach (ITile t in tiles)
			{
				_explored[t.X, t.Y] = true;
				_visible[t.X, t.Y]  = true;
			}
		}

		public void MergeVisibility(Player other)
		{
			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
			{
				if (!other._explored[x, y]) continue;
				_visible[x, y]  = true;
				_explored[x, y] = true;
			}
		}

		public bool HasNewVisibilityFor(Player other)
		{
			for (int x = 0; x < Map.WIDTH; x++)
			for (int y = 0; y < Map.HEIGHT; y++)
				if (_explored[x, y] && !other._explored[x, y])
					return true;
			return false;
		}

		// Even-split distribution of the BondPool to non-donor cities, capped at each
		// recipient's completion threshold. Excess and integer-division remainder are
		// discarded. If no recipients qualify, the entire pool converts 1:1 to gold.
		private void DistributeBondPool()
		{
			if (BondPool <= 0) return;

			City[] recipients = Cities
				.Where(c => c.CurrentProduction is not null && !(c.CurrentProduction is InfrastructureBond))
				.ToArray();

			if (recipients.Length == 0)
			{
				int newGold = Gold + BondPool;
				Gold = (short)Math.Min(short.MaxValue, newGold);
			}
			else
			{
				int share = BondPool / recipients.Length;
				if (share > 0)
				{
					foreach (City c in recipients)
					{
						int cap = c.ProductionCost(c.CurrentProduction);
						int delta = Math.Min(share, Math.Max(0, cap - c.Shields));
						c.Shields += delta;
					}
				}
			}

			BondPool = 0;
		}

		public void NewTurn()
		{
			DistributeBondPool();

			if (!Game.GetCities().Any(x => this == x.Owner) && !Game.Instance.GetUnits().Any(x => this == x.Owner))
			{
				if (IsHuman)
					DecisionLogger.EndGame(Score, "Destroyed", humanWon: false, turns: Game.Instance.GameTurn);
				GameTask.Enqueue(Turn.GameOver(this));
			}

			if (_anarchy == 0 && Government is Anarchy)
			{
				if (Human == Game.CurrentPlayer)
					GameTask.Enqueue(Show.ChooseGovernment);
				else
					AI?.ChooseGovernment();
			}
			if (_anarchy > 0) _anarchy--;

			// Culture accrues from the empire's civic fabric.
			_culture += CultureRate;

			foreach (byte k in _peaceTreaty.Keys.ToArray())
				if (--_peaceTreaty[k] <= 0) _peaceTreaty.Remove(k);
			foreach (byte k in _attitudeBonus.Keys.ToArray())
				if (--_attitudeBonus[k] <= 0) _attitudeBonus.Remove(k);
			foreach (byte k in _defensePact.Keys.ToArray())
				if (--_defensePact[k] <= 0) _defensePact.Remove(k);

			// Tribute settlement. For each protector this player owes, transfer gold (if
			// solvent) and re-up the peace-treaty + attitude-bonus timers so the pact stays
			// active as long as payment continues. If the payer can't cover the full annual
			// gold, the pact dissolves — but war is not auto-declared; the protector simply
			// loses the diplomatic lock and may declare on their next strategy tick.
			foreach (byte k in _tributeTo.Keys.ToArray())
			{
				int annualGold = _tributeTo[k];
				Player protector = Game.Instance.GetPlayer(k);
				if (protector is null || protector.IsDestroyed())
				{
					_tributeTo.Remove(k);
					continue;
				}
				if (_gold < annualGold)
				{
					DissolveTribute(protector);
					continue;
				}
				_gold     -= (short)annualGold;
				protector._gold = (short)Math.Min(short.MaxValue, protector._gold + annualGold);
				// Renew the peace lock so the protector stays barred from declaring war.
				SetPeaceTreaty(protector, 100);
				protector.SetPeaceTreaty(this, 100);
			}

			// Great Library: auto-acquire any advance that 2+ other civs already possess
			if (HasWonder<Wonders.GreatLibrary>() && !Game.WonderObsolete<Wonders.GreatLibrary>())
			{
				Player[] others = Game.Players.Where(p => p != this && !p.IsDestroyed() && !(p.Civilization is Barbarian)).ToArray();
				foreach (IAdvance advance in Common.Advances.Where(a => !(a is FutureTech) && !HasAdvance(a)))
				{
					if (others.Count(p => p.HasAdvance(advance)) >= 2)
						AddAdvance(advance, false);
				}
			}

			AI?.ConsiderGovernment();
			AI?.ConsiderSliders();
			AI?.ConsiderGarrisonUpkeep();
			AI?.ConsiderRushBuy();
			AI?.ConsiderWar();
			AI?.ConsiderDiplomacy();
			AI?.ConsiderMapTrade();
		}

		public override bool Equals (object obj)
		{
			if (obj is byte)
				return Game.PlayerNumber(this) == (byte)obj;
			if (obj is Player)
				return Game.PlayerNumber(this) == Game.PlayerNumber((obj as Player)!);
			return false;
		}
		
		public override int GetHashCode() => Game.PlayerNumber(this);

		public static explicit operator Player(byte playerNumber) => Game.GetPlayer(playerNumber);
		public static explicit operator byte(Player player) => Game.PlayerNumber(player);
		
		public static bool operator ==(Player? p1, byte p2) => p1 is not null && Game.PlayerNumber(p1) == p2;
		public static bool operator !=(Player? p1, byte p2) => p1 is null || Game.PlayerNumber(p1) != p2;
		
		public Player(ICivilization civilization, string? customLeaderName = null, string? customTribeName = null, string? customTribeNamePlural = null)
		{
			_civilization = civilization;
			// Kept on the Player rather than written into the (shared) civilization
			// instance — Common.Civilizations is cached, so mutating Leader.Name here
			// would leak a custom name into menus and later games.
			_customLeaderName = customLeaderName;
			_tribeName = customTribeName ?? _civilization.Name;
			_tribeNamePlural = customTribeNamePlural ?? _civilization.NamePlural;
			Government = new Despotism();
			
			for (int xx = 0; xx < Map.WIDTH; xx++)
			for (int yy = 0; yy < Map.HEIGHT; yy++)
			{
				_explored[xx, yy] = false;
				_visible[xx, yy] = false;
			}
		}
	}
}