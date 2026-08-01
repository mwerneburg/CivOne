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
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Screens;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Wonders;

using UniversityBuilding = CivOne.Buildings.University;
using ObservatoryBuilding = CivOne.Buildings.Observatory;
using LibraryBuilding = CivOne.Buildings.Library;

namespace CivOne
{
	public class City : BaseInstance, ITurn
	{
		internal int NameId { get; set; }
		// int, not byte: the Epic map is 320 wide, so X exceeds a byte's 0-255 range
		// in its eastern third. A byte here wrapped far-east cities ~256 tiles west,
		// usually into the ocean. Destroyed-city sentinel is still (255,255) — valid
		// only because Y=255 is outside the 0-199 row range, so Tile resolves to null.
		internal int X;
		internal int Y;
		internal byte OriginalOwner { get; set; }
		private byte _owner;
		internal byte Owner
		{
			get
			{
				return _owner;
			}
			set
			{
				if (Game.Started && _owner != value)
				{
					foreach (City other in Game.GetCities().Where(c => c != this))
						other.RemoveTradeRoutesTo(this);
					_tradeRoutes.Clear();
				}
				_owner = value;
				ResetResourceTiles();
				InvalidateCache();
			}
		}
		internal string Name => Game.CityNames[NameId];
		private byte _size;
		internal byte Size
		{
			get
			{
				return _size;
			}
			set
			{
				if (X == 255 || Y == 255) return;

				_size = value;
				InvalidateCache();
				if (_size == 0)
				{
					Map[X, Y].Road = false;
					Map[X, Y].Irrigation = false;
					if (Game.Started) Game.DestroyCity(this);
					return;
				}
				if (Food > FoodRequired) Food = FoodRequired;
				SetResourceTiles();
			}
		}
		internal int Shields { get; set; }
		internal int Food { get; set; }
		internal IProduction CurrentProduction { get; private set; } = null!;
		// Persisted in the COS save file (see Game.Cos.cs).
		private readonly List<IProduction> _productionQueue = new();
		private List<ITile> _resourceTiles = new();
		private List<IBuilding> _buildings = new();
		private List<IWonder> _wonders = new();

		internal class TradeRoute
		{
			private readonly City _home;
			internal City Partner { get; }
			internal string Commodity { get; }
			internal int Value => _home.RouteBonus(Partner);
			internal TradeRoute(City home, City partner, string commodity) { _home = home; Partner = partner; Commodity = commodity; }
		}

		private readonly List<TradeRoute> _tradeRoutes = new();
		internal IEnumerable<TradeRoute> TradeRoutes => _tradeRoutes;
		internal int TradeRouteCount => _tradeRoutes.Count;

		// Cached computed values; call InvalidateCache() on any state mutation.
		private int?          _cachedFoodRaw;
		private int?          _cachedShieldRaw;
		private int?          _cachedRawTrade;
		private int?          _cachedCorruption;
		private int?          _cachedBaseTrade;
		private int?          _cachedTradeRouteBonus;
		private int?          _cachedTradeTotal;
		private short?        _cachedTradeTaxes;
		private short?        _cachedTradeLuxuries;
		private short?        _cachedTradeScience;
		private short?        _cachedLuxuries;
		private short?        _cachedTaxes;
		private short?        _cachedScience;
		private List<Citizen>? _cachedCitizens;

		internal void InvalidateCache()
		{
			_cachedFoodRaw        = null;
			_cachedShieldRaw      = null;
			_cachedRawTrade       = null;
			_cachedCorruption     = null;
			_cachedBaseTrade      = null;
			_cachedTradeRouteBonus = null;
			_cachedTradeTotal     = null;
			_cachedTradeTaxes     = null;
			_cachedTradeLuxuries  = null;
			_cachedTradeScience   = null;
			_cachedLuxuries       = null;
			_cachedTaxes          = null;
			_cachedScience        = null;
			_cachedCitizens       = null;
		}

		internal void AddTradeRoute(City partner, string commodity)
		{
			if (partner is null) return;
			if (_tradeRoutes.Count >= 3) _tradeRoutes.RemoveAt(0);
			_tradeRoutes.Add(new TradeRoute(this, partner, commodity));
			InvalidateCache();
		}

		internal void RemoveTradeRoutesTo(City city)
		{
			_tradeRoutes.RemoveAll(r => r.Partner == city);
			InvalidateCache();
		}

		internal void RemoveTradeRoutesTo(Player enemy)
		{
			_tradeRoutes.RemoveAll(r => r.Partner.Owner == Game.PlayerNumber(enemy));
			InvalidateCache();
		}

		public IBuilding[] Buildings => _buildings.OrderBy(b => b.Id).ToArray();
		public IWonder[] Wonders => _wonders.OrderBy(b => b.Id).ToArray();

		public bool HasBuilding(IBuilding building) => _buildings.Any(b => b.Id == building.Id);
		public bool HasBuilding(Type type) => _buildings.Any(b => b.GetType() == type);
		public bool HasBuilding<T>() where T : IBuilding => _buildings.Any(b => b is T);

		public bool HasWonder(IWonder wonder) => _wonders.Any(w => w.Id == wonder.Id);
		public bool HasWonder(Type type) => _wonders.Any(w => w.GetType() == type);
		public bool HasWonder<T>() where T : IWonder => _wonders.Any(w => w is T);
		public bool HasDomeWonder() => _wonders.Any(w => w is Wonders.IDomeComponent);

		// True when a friendly city on the same continent holds the Hoover Dam
		private bool HooverDamActive => Tile is not null && Game.Started
			&& Map.ContentCities(Tile.ContinentId).Any(c => c.Owner == Owner && c.HasWonder<HooverDam>());

		public int HappyCitizens => Citizens.Count(c => c == Citizen.HappyMale || c == Citizen.HappyFemale);
		public int UnhappyCitizens => Citizens.Count(c => c == Citizen.UnhappyMale || c == Citizen.UnhappyFemale);

		public int ContentCitizens => Citizens.Count(c => c == Citizen.ContentFemale || c == Citizen.ContentMale);
 		public bool IsInDisorder => _size > 0 && UnhappyCitizens > HappyCitizens;
		public int  DisorderTurns {get; set;} = 0;
		public bool WasInDisorder { get => DisorderTurns > 0; set { if (value && DisorderTurns == 0) DisorderTurns = 1; else if (!value) DisorderTurns = 0; } }
		public bool WasWeLoveKing {get; set;} = false;
		// After a diplomat steals tech here, the city is locked against further theft for
		// TechStealCooldown turns (then it's fair game again — Civ1 allows repeat espionage).
		// Stored as the turn of the last theft so the lock expires on its own; 0 = never/expired.
		// The public bool keeps the rest of the codebase (and the COS save) unchanged: it reads
		// true only while still on cooldown, and `= true` stamps the current turn.
		private const int TechStealCooldown = 20;
		private int _techStolenTurn = 0;
		public bool TechStolen
		{
			// Game.Instance is null while a save is still being deserialized (the singleton
			// isn't published until Game's constructor returns), so guard both accessors. The
			// load path uses LoadTechStolen below to restore the stamp without it.
			get => _techStolenTurn > 0 && Game.Instance is not null && Game.Instance.GameTurn - _techStolenTurn < TechStealCooldown;
			set => _techStolenTurn = (value && Game.Instance is not null) ? Game.Instance.GameTurn : 0;
		}
		// Restore the cooldown from a COS save using the loaded turn, since Game.Instance
		// isn't available yet during construction.
		internal void LoadTechStolen(bool stolen, ushort currentTurn) => _techStolenTurn = stolen ? currentTurn : 0;

		internal int ShieldCosts
		{
			get
			{
				IGovernment government = Game.GetPlayer(_owner).Government;
				int supported = Units.Count(u => (!(u is Diplomat) && !(u is ICaravan)));
				int free = government.FreeUnitSupport < 0 ? _size : government.FreeUnitSupport;
				return Math.Max(0, supported - free);
			}
		}

		internal int ShieldIncome => ShieldTotal - ShieldCosts;
		
		internal int FoodCosts
		{
			get
			{
				int costs = (_size * 2);
				IGovernment government = Game.GetPlayer(_owner).Government;
				costs += Units.Count(u => (u is Settlers)) * government.SettlerFoodCost;
				return costs;
			}
		}

		private int FoodRaw => (int)(_cachedFoodRaw ??= ResourceTiles.Sum(t => FoodValue(t)));
		internal int FoodIncome => (HasBuilding<Buildings.MassTransit>() ? (int)(FoodRaw * 1.2) : FoodRaw) - FoodCosts;
		internal int FoodRequired => (Game.Started && Player.Civilization is Civilizations.Olvir)
			? (int)(Size + 1) * 5
			: (int)(Size + 1) * 10;
		internal int FoodTotal => HasBuilding<Buildings.MassTransit>() ? (int)(FoodRaw * 1.2) : FoodRaw;

		// ── Olvir improvement yield bonuses ────────────────────────────────────
		// Alien technology operates outside normal government efficiency penalties.

		private static int OlvirFoodBonus(OlvirImprovementType imp) => imp switch
		{
			OlvirImprovementType.Aquafarm         => 2, // coastal aquaculture
			OlvirImprovementType.BiofilterWall    => 1, // bio-engineered soil
			OlvirImprovementType.SettlementCluster => 1, // integrated colony
			OlvirImprovementType.CanopyArray       => 1, // managed canopy ecology
			_ => 0
		};

		private static int OlvirShieldBonus(OlvirImprovementType imp) => imp switch
		{
			OlvirImprovementType.CanopyArray => 1, // managed forest output
			OlvirImprovementType.RepairBay   => 1, // industrial extraction boost
			_ => 0
		};

		private static int OlvirTradeBonus(OlvirImprovementType imp) => imp switch
		{
			OlvirImprovementType.ExchangeNode      => 1, // trade network hub
			OlvirImprovementType.SettlementCluster => 1, // colony trade post
			_ => 0
		};

		// ── tile yield methods ──────────────────────────────────────────────────

		internal int FoodValue(ITile tile)
		{
			// Grey goo is dead ground: nothing grows, nothing is mined, nothing moves.
			if (Game.Instance.GooTiles.ContainsKey((tile.X, tile.Y))) return 0;
			int output = tile.Food;
			switch (tile.Type)
			{
				case Terrain.Desert:
				case Terrain.Forest:
				case Terrain.Grassland1:
				case Terrain.Grassland2:
				case Terrain.River:
					if (!Player.AnarchyDespotism && tile.Irrigation) output += 1;
					break;
				case Terrain.Ocean:
				case Terrain.Tundra:
					if (!Player.AnarchyDespotism && tile.Special) output += 1;
					break;
			}
			if (tile.RailRoad) output = (int)Math.Floor((double)output * 1.5);
			if (tile.IsOcean && HasBuilding<SeaPlatform>()) output += 1;
			if (Game.OlvirImprovements.TryGetValue((tile.X, tile.Y), out var olvirF))
				output += OlvirFoodBonus(olvirF);
			return output;
		}

		internal int ShieldValue(ITile tile)
		{
			if (Game.Instance.GooTiles.ContainsKey((tile.X, tile.Y))) return 0;
			int output = tile.Shield;
			bool isCenter = (tile.X == X && tile.Y == Y);

			// City-center floor (Civ I baseline): the tile under the city always produces
			// at least 1 shield, regardless of terrain. Rescues floating cities on Ocean and
			// land cities founded on Grassland/Hills.
			if (isCenter && output < 1) output = 1;

			switch (tile.Type)
			{
				case Terrain.Hills:
					if (!Player.AnarchyDespotism && tile.Mine) output += 1;
					break;
			}
			if (tile.RailRoad) output = (int)Math.Floor((double)output * 1.5);
			// Sea Platform extends floating industry to the worked ocean ring (the center
			// is already covered by the city-center floor above).
			if (tile.IsOcean && !isCenter && HasBuilding<SeaPlatform>()) output += 1;
			if (Game.OlvirImprovements.TryGetValue((tile.X, tile.Y), out var olvirS))
				output += OlvirShieldBonus(olvirS);
			return output;
		}

		private int ShieldRaw => (int)(_cachedShieldRaw ??= ResourceTiles.Sum(t => ShieldValue(t)));

		internal int ShieldTotal
		{
			get
			{
				int shields = ShieldRaw;
				if (HasBuilding<Buildings.MassTransit>()) shields = (int)(shields * 1.2);
				if (_buildings.Any(b => (b is Factory))) shields += (short)Math.Floor((double)shields * (_buildings.Any(b => (b is NuclearPlant || b is PowerPlant || b is HydroPlant)) || HooverDamActive ? 1.0 : 0.5));
				if (_buildings.Any(b => (b is MfgPlant))) shields += (short)Math.Floor((double)shields * 1.0);
				return shields;
			}
		}

		internal int TradeValue(ITile tile)
		{
			if (Game.Instance.GooTiles.ContainsKey((tile.X, tile.Y))) return 0;
			int output = tile.Trade;

			// City-center floor: the tile under the city always produces at least 1 trade
			// (matches the shield floor in ShieldValue and the original Civ I rule).
			if (tile.X == X && tile.Y == Y && output < 1) output = 1;

			if (tile.RailRoad) output = (int)Math.Floor((double)output * 1.5);
			switch (tile.Type)
			{
				case Terrain.Desert:
				case Terrain.Grassland1:
				case Terrain.Grassland2:
				case Terrain.Plains:
					if (!tile.Road) break;
					output += Player.Government.TradeBonus;
					break;
				case Terrain.Ocean:
					if (Player.HasAdvance<Trade>()) output += 1;
					output += Player.Government.TradeBonus;
					break;
				case Terrain.River:
					output += 1; // rivers are natural trade corridors regardless of government
					output += Player.Government.TradeBonus;
					break;
				case Terrain.Jungle:
				case Terrain.Mountains:
					if (!tile.Special) break;
					output += Player.Government.SpecialResourceTradeBonus;
					break;
			}
			if (output > 0 && HasWonder<Colossus>() && !Game.WonderObsolete<Colossus>()) output += 1;
			if (Game.OlvirImprovements.TryGetValue((tile.X, tile.Y), out var olvirT))
				output += OlvirTradeBonus(olvirT);
			return output;
		}

		private int RawTrade => (int)(_cachedRawTrade ??= ResourceTiles.Sum(t => TradeValue(t)));

		// Pre-corruption trade, for the AI's government comparison — it needs the
		// denominator to judge what graft is actually costing the empire.
		internal int RawTradeForAi => RawTrade;

		private int BaseTrade => (int)(_cachedBaseTrade ??= Math.Max(0, RawTrade - Corruption));

		private int RouteBonus(City partner)
		{
			if (partner.X == 255) return 0;
			if (Owner != partner.Owner && Game.GetPlayer(Owner).IsAtWar(Game.GetPlayer(partner.Owner))) return 0;
			int distance = Common.DistanceToTile(X, Y, partner.X, partner.Y);
			float multiplier = 1.0f;
			if (X != 255 && Tile.ContinentId == partner.Tile.ContinentId) multiplier *= 0.5f;
			if (Owner == partner.Owner) multiplier *= 0.5f;
			return (int)(multiplier * (float)(distance + 10) * partner.BaseTrade / 24);
		}

		private int TradeRouteBonus => (int)(_cachedTradeRouteBonus ??= _tradeRoutes.Sum(r => RouteBonus(r.Partner)));

		internal int TradeTotal => (int)(_cachedTradeTotal ??= BaseTrade + TradeRouteBonus);

		// Cultural weight per turn: faith, arts, and learning. Wonders radiate 3;
		// obsolete wonders still count 1 — old glory endures. Accumulated into
		// Player.Culture each turn; read by diplomacy, the visitor-archetype draw,
		// and the cultural-defection check.
		internal int CultureRate =>
			(HasBuilding<Buildings.Temple>() ? 1 : 0)
			+ (HasBuilding<Buildings.Colosseum>() ? 1 : 0)
			+ (HasBuilding<LibraryBuilding>() ? 1 : 0)
			+ (HasBuilding<Buildings.Cathedral>() ? 2 : 0)
			+ (HasBuilding<UniversityBuilding>() ? 2 : 0)
			+ (HasBuilding<Buildings.CivicMonument>() ? 3 : 0)
			+ (Player.HasWonder<Wonders.TheInternet>() ? 1 : 0)
			+ Wonders.Sum(w => Game.WonderObsolete(w) ? 1 : 3);
		internal short TradeTaxes => (short)(_cachedTradeTaxes ??= (short)Math.Round(((double)TradeTotal / 10) * Player.TaxesRate, MidpointRounding.AwayFromZero));
		internal short TradeLuxuries => (short)(_cachedTradeLuxuries ??= (short)Math.Round(((double)(TradeTotal - TradeTaxes) / (10 - Player.TaxesRate)) * Player.LuxuriesRate, MidpointRounding.AwayFromZero));
		internal short TradeScience => (short)(_cachedTradeScience ??= (short)(TradeTotal - TradeLuxuries - TradeTaxes));

		internal int Corruption
		{
			get
			{
				if (_cachedCorruption.HasValue) return _cachedCorruption.Value;
				int corruption = CorruptionBase;
				// The Greys skim a fifth of the city's trade: they don't work here,
				// but they do eat here (The Portal's cursed outcome, Game.GreyCities).
				if (Game.Instance.GreyCities.Contains((X, Y)))
					corruption += RawTrade / 5;
				return (_cachedCorruption = corruption).Value;
			}
		}

		private int CorruptionBase
		{
			get
			{
				IGovernment government = Game.GetPlayer(_owner).Government;
				// "Democracy still leaks": utopian governments (CorruptionMultiplier == 0,
				// i.e. Democracy) used to be perfectly clean. Now graft creeps in as a
				// metropolis outgrows its oversight — leak rises with city size. Palace
				// and Audit Authority hosts stay clean; Courthouse halves the leak.
				// Future hook: a Police Station building should zero the leak entirely.
				if (government.CorruptionMultiplier == 0)
				{
					if (HasBuilding<Palace>()) return 0;
					if (HasWonder<Wonders.AuditAuthority>()) return 0;
					if (HasBuilding<PoliceStation>()) return 0;
					int leakPct = Math.Max(0, Size - 4);
					int leak = RawTrade * leakPct / 100;
					if (HasBuilding<Courthouse>()) leak /= 2;
					return leak;
				}

				int distance;
				switch (government)
				{
					case IGovernment g when g.FixedCorruptionDistance is int fixedDistance:
						distance = fixedDistance;
						break;
					default:
						if (HasBuilding<Palace>()) return 0;
						// Audit Authority host city is a second capital: corruption-free.
						if (HasWonder<Wonders.AuditAuthority>()) return 0;
						var owner = Game.GetPlayer(Owner);
						City capital2 = owner.Cities.FirstOrDefault(x => x.HasBuilding<Palace>());
						City audit = owner.Cities.FirstOrDefault(x => x.HasWonder<Wonders.AuditAuthority>());
						int dCapital = capital2 is not null ? Common.DistanceToTile(X, Y, capital2.X, capital2.Y) : 32;
						int dAudit   = audit   is not null ? Common.DistanceToTile(X, Y, audit.X,   audit.Y)   : int.MaxValue;
						distance = System.Math.Min(dCapital, dAudit);
						break;
				}

				int totalTrade = RawTrade;
				int corruption = (int)Math.Round((float)(totalTrade * distance * 3) / (10 * government.CorruptionMultiplier));

				if (HasBuilding<Courthouse>() || (HasBuilding<Palace>() && government.PalaceHalvesCorruption)) corruption /= 2;

				return corruption;
			}
		}

		internal short Luxuries
		{
			get
			{
				if (_cachedLuxuries.HasValue) return _cachedLuxuries.Value;
				short luxuries = TradeLuxuries;
				if (HasBuilding<MarketPlace>()) luxuries += (short)Math.Floor((double)luxuries * 0.5);
				if (HasBuilding<Bank>()) luxuries += (short)Math.Floor((double)luxuries * 0.5);
				luxuries += (short)(_specialists.Count(c => c == Citizen.Entertainer) * 2);
				return (_cachedLuxuries = luxuries).Value;
			}
		}

		internal short Taxes
		{
			get
			{
				if (_cachedTaxes.HasValue) return _cachedTaxes.Value;
				short taxes = TradeTaxes;
				if (HasBuilding<MarketPlace>()) taxes += (short)Math.Floor((double)taxes * 0.5);
				if (HasBuilding<Bank>()) taxes += (short)Math.Floor((double)taxes * 0.5);
				taxes += (short)(_specialists.Count(c => c == Citizen.Taxman) * 2);
				return (_cachedTaxes = taxes).Value;
			}
		}

		internal short Science
		{
			get
			{
				if (_cachedScience.HasValue) return _cachedScience.Value;
				short science = TradeScience;
				bool newtonActive = !Game.WonderObsolete<IsaacNewtonsCollege>() && Player.HasWonder<IsaacNewtonsCollege>() && !Player.HasWonder<HumanGenomeProject>();
				double libUniBonus = newtonActive ? (2.0 / 3.0) : 0.5;
				if (HasBuilding<Library>()) science += (short)Math.Floor(science * libUniBonus);
				if (HasBuilding<UniversityBuilding>()) science += (short)Math.Floor(science * libUniBonus);
				if (HasBuilding<ObservatoryBuilding>()) science += (short)Math.Floor(science * libUniBonus);
				if (HasBuilding<Xenolab>()) science += (short)Math.Floor(science * 0.5);
				if (!Game.WonderObsolete<CopernicusObservatory>() && HasWonder<CopernicusObservatory>()) science += science;
				if (Player.HasWonder<HumanGenomeProject>()) science += (short)Math.Floor((double)science * 0.5);
				// The Internet: every mind in the empire, one conversation.
				if (Player.HasWonder<TheInternet>()) science += (short)Math.Floor((double)science * 0.25);
				science += (short)(_specialists.Count(c => c == Citizen.Scientist) * 2);
				// Government research bias, applied last so it lifts the whole city's
				// output — buildings, wonders and scientists alike.
				int govBonus = Player.Government.ScienceBonus;
				if (govBonus != 0) science += (short)Math.Floor(science * (govBonus / 100.0));
				return (_cachedScience = science).Value;
			}
		}

		internal short TotalMaintenance
		{
			get
			{
				bool adamSmith = Player.HasWonder<AdamSmithsTradingHouse>();
				return (short)_buildings.Sum(b => (adamSmith && b.Maintenance == 1) ? 0 : b.Maintenance);
			}
		}

		internal byte Status
		{
			get
			{
				if (Size == 0) return 0;
				
				byte output = 0;
				if (Map[X, Y].GetBorderTiles().Any(t => t.IsOcean)) output |= (0x01 << 1); // Coastal city
				if (BuildingSold) output |= (0x01 << 7); // Building sold this turn
				return output;
			}
		}

		internal IEnumerable<ITile> ResourceTiles => CityTiles.Where(t => (t.X == X && t.Y == Y) || _resourceTiles.Contains(t));

		internal bool OccupiedTile(ITile tile)
		{
			if (ResourceTiles.Any(t => t.X == tile.X && t.Y == tile.Y))
				return false;
			return InvalidTile(tile);
		}

		internal bool InvalidTile(ITile tile)
		{
			// A foreign unit standing on the tile blocks it — cheap, check first.
			if (tile.Units.Any(u => u.Owner != Owner)) return true;

			// Otherwise the tile is invalid only if ANOTHER city already works it. The old
			// form scanned every city and materialised each one's ResourceTiles (a fresh 5x5
			// CityRadius array + LINQ) for every tile checked — O(cities) allocation-heavy
			// work per tile, the dominant late-game per-turn cost (UpdateResources and
			// SetResourceTiles both hammer this). But a tile can only be worked by a city
			// whose CENTRE lies within the 5x5 radius (Chebyshev <= 2, X wrapping), so
			// pre-filter on cheap integer distance and then test the city's fields directly
			// (centre or _resourceTiles) instead of building ResourceTiles. Only 0-4 cities
			// are ever in range, so this is effectively O(cities) of int math, no allocation.
			IReadOnlyList<City> cities = Game.Instance.CitiesList;
			for (int i = 0; i < cities.Count; i++)
			{
				City c = cities[i];
				if (c == this) continue;
				int dx = Math.Abs(c.X - tile.X);
				if (dx > Map.WIDTH - dx) dx = Map.WIDTH - dx;   // horizontal map wrap
				if (dx > 2 || Math.Abs(c.Y - tile.Y) > 2) continue;
				if ((c.X == tile.X && c.Y == tile.Y) ||
				    c._resourceTiles.Any(t => t.X == tile.X && t.Y == tile.Y))
					return true;
			}
			return false;
		}

		private void UpdateSpecialists()
		{
			// Specialists fill the citizen slots not working a tile. ResourceTiles includes
			// the city center, which doesn't consume a citizen — same formula as ComputeCitizens.
			int specialists = Math.Max(0, _size - (ResourceTiles.Count() - 1));
			while (_specialists.Count < specialists) _specialists.Add(Citizen.Entertainer);
			while (_specialists.Count > specialists) _specialists.RemoveAt(_specialists.Count - 1);
			InvalidateCache();
		}

		private void SetResourceTiles()
		{
			if (!Game.Started) return;
			while (_resourceTiles.Count > Size)
				_resourceTiles.RemoveAt(_resourceTiles.Count - 1);
			// Fill the full deficit, not just one slot — a Size jump >1 (advanced tribe hut,
			// settler joining a freshly-loaded city, etc.) would otherwise leave gaps that
			// UpdateSpecialists rounds out into Entertainers.
			while (_resourceTiles.Count < Size)
			{
				ITile pick = CityTiles
					.Where(t => !OccupiedTile(t) && !ResourceTiles.Contains(t))
					.OrderByDescending(t => FoodValue(t))
					.ThenByDescending(t => ShieldValue(t))
					.ThenByDescending(t => TradeValue(t))
					.FirstOrDefault();
				if (pick is null) break;  // no workable tiles left — let UpdateSpecialists fill the rest
				_resourceTiles.Add(pick);
			}

			UpdateSpecialists();
		}

		internal byte[] GetResourceTiles()
		{
			byte[] output = new byte[6]; // bytes 0-2: tile bitmap; bytes 3-5: specialist types
			foreach (ITile tile in _resourceTiles)
			{
				int x = tile.X - X;
				int y = tile.Y - Y;
				switch(x)
				{
					case -2:
						if (y == -1) output[2] |= (byte)(0x01 << 3);
						if (y == 0) output[1] |= (byte)(0x01 << 3);
						if (y == 1) output[2] |= (byte)(0x01 << 2);
						continue;
					case -1:
						if (y == -2) output[1] |= (byte)(0x01 << 4);
						if (y == -1) output[0] |= (byte)(0x01 << 7);
						if (y == 0) output[0] |= (byte)(0x01 << 3);
						if (y == 1) output[0] |= (byte)(0x01 << 6);
						if (y == 2) output[2] |= (byte)(0x01 << 1);
						continue;
					case 0:
						if (y == -2) output[1] |= (byte)(0x01 << 0);
						if (y == -1) output[0] |= (byte)(0x01 << 0);
						if (y == 1) output[0] |= (byte)(0x01 << 2);
						if (y == 2) output[1] |= (byte)(0x01 << 2);
						continue;
					case 1:
						if (y == -2) output[1] |= (byte)(0x01 << 5);
						if (y == -1) output[0] |= (byte)(0x01 << 4);  // (1,-1) NE-inner; matches decoder at byte 0 bit 4
						if (y == 0) output[0] |= (byte)(0x01 << 1);
						if (y == 1) output[0] |= (byte)(0x01 << 5);
						if (y == 2) output[2] |= (byte)(0x01 << 0);
						continue;
					case 2:
						if (y == -1) output[1] |= (byte)(0x01 << 6);
						if (y == 0) output[1] |= (byte)(0x01 << 1);
						if (y == 1) output[1] |= (byte)(0x01 << 7);
						continue;
				}
			}
			// Encode specialist types: 0=Entertainer, 1=Taxman, 2=Scientist (2 bits each, up to 12)
			for (int i = 0; i < _specialists.Count && i < 12; i++)
			{
				int type = _specialists[i] == Citizen.Taxman ? 1 : _specialists[i] == Citizen.Scientist ? 2 : 0;
				output[3 + i / 4] |= (byte)(type << (i % 4 * 2));
			}
			return output;
		}

		internal void SetResourceTiles(byte[] gameData)
		{
			if (gameData.Length != 6)
			{
				Log($"Invalid Resource game data for {Name}");
				return;
			}

			_resourceTiles.Clear();
			if (((gameData[0] >> 0) & 1) > 0) _resourceTiles.Add(Tile[0, -1]);
			if (((gameData[0] >> 1) & 1) > 0) _resourceTiles.Add(Tile[1, 0]);
			if (((gameData[0] >> 2) & 1) > 0) _resourceTiles.Add(Tile[0, 1]);
			if (((gameData[0] >> 3) & 1) > 0) _resourceTiles.Add(Tile[-1, 0]);
			if (((gameData[0] >> 4) & 1) > 0) _resourceTiles.Add(Tile[1, -1]);
			if (((gameData[0] >> 5) & 1) > 0) _resourceTiles.Add(Tile[1, 1]);
			if (((gameData[0] >> 6) & 1) > 0) _resourceTiles.Add(Tile[-1, 1]);
			if (((gameData[0] >> 7) & 1) > 0) _resourceTiles.Add(Tile[-1, -1]);
			
			if (((gameData[1] >> 0) & 1) > 0) _resourceTiles.Add(Tile[0, -2]);
			if (((gameData[1] >> 1) & 1) > 0) _resourceTiles.Add(Tile[2, 0]);
			if (((gameData[1] >> 2) & 1) > 0) _resourceTiles.Add(Tile[0, 2]);
			if (((gameData[1] >> 3) & 1) > 0) _resourceTiles.Add(Tile[-2, 0]);
			if (((gameData[1] >> 4) & 1) > 0) _resourceTiles.Add(Tile[-1, -2]);
			if (((gameData[1] >> 5) & 1) > 0) _resourceTiles.Add(Tile[1, -2]);
			if (((gameData[1] >> 6) & 1) > 0) _resourceTiles.Add(Tile[2, -1]);
			if (((gameData[1] >> 7) & 1) > 0) _resourceTiles.Add(Tile[2, 1]);
			
			if (((gameData[2] >> 0) & 1) > 0) _resourceTiles.Add(Tile[1, 2]);
			if (((gameData[2] >> 1) & 1) > 0) _resourceTiles.Add(Tile[-1, 2]);
			if (((gameData[2] >> 2) & 1) > 0) _resourceTiles.Add(Tile[-2, 1]);
			if (((gameData[2] >> 3) & 1) > 0) _resourceTiles.Add(Tile[-2, -1]);

			// Tiles past the top/bottom map edge resolve to null for cities within
			// two rows of a pole; drop them so the yield sums don't dereference null.
			_resourceTiles.RemoveAll(t => t is null);

			// Decode specialist types: 0=Entertainer, 1=Taxman, 2=Scientist (2 bits each, up to 12)
			_specialists.Clear();
			int specialistCount = Math.Max(0, Size - _resourceTiles.Count);
			for (int i = 0; i < specialistCount && i < 12; i++)
			{
				int type = (gameData[3 + i / 4] >> (i % 4 * 2)) & 0x3;
				_specialists.Add(type == 1 ? Citizen.Taxman : type == 2 ? Citizen.Scientist : Citizen.Entertainer);
			}
		}

		internal void ResetResourceTiles()
		{
			_resourceTiles.Clear();
			for (int i = 0; i < Size; i++)
				SetResourceTiles();
			InvalidateCache();
		}

		public void RelocateResourceTile(ITile tile)
		{
			if (tile.X == X && tile.Y == Y) return;
			// Swap the now-invalid worked tile for the best available valid one, preserving
			// the worked-tile COUNT (and thus any citizens the player has assigned as
			// specialists). A full ResetResourceTiles here would re-pick up to Size on every
			// relocation, wiping the player's manual musician/specialist allocations.
			_resourceTiles.Remove(tile);
			ITile replacement = CityTiles
				.Where(t => !(t.X == X && t.Y == Y) && !OccupiedTile(t) && !_resourceTiles.Contains(t))
				.OrderByDescending(t => FoodValue(t))
				.ThenByDescending(t => ShieldValue(t))
				.ThenByDescending(t => TradeValue(t))
				.FirstOrDefault();
			if (replacement is not null) _resourceTiles.Add(replacement);
			UpdateSpecialists();
			InvalidateCache();
		}

		public void SetResourceTile(ITile tile)
		{
			if (tile is null || OccupiedTile(tile) || !CityTiles.Contains(tile) || (tile.X == X && tile.Y == Y) || (_resourceTiles.Count >= Size && !_resourceTiles.Contains(tile)))
			{
				ResetResourceTiles();
				return;
			}
			if (_resourceTiles.Contains(tile))
			{
				_resourceTiles.Remove(tile);
				InvalidateCache();
				return;
			}
			_resourceTiles.Add(tile);
			UpdateSpecialists();
			InvalidateCache();
		}

		public Player Player => Game.Instance.GetPlayer(Owner);

		public IEnumerable<IProduction> AvailableProduction
		{
			get
			{
				foreach (IUnit unit in Reflect.GetUnits().Where(u => Player.ProductionAvailable(u)))
				{
					if (unit.Class == UnitClass.Water && !Map[X, Y].GetBorderTiles().Any(t => t.IsOcean)) continue;
					if (unit is Nuclear && !Game.WonderBuilt<ManhattanProject>()) continue;
					if ((unit is Transport || unit is Submarine || unit is Carrier || unit is Battleship || unit is Cruiser) && !HasBuilding<Shipyard>()) continue;
					yield return unit;
				}
				bool coastal = Map[X, Y].GetBorderTiles().Any(t => t.IsOcean && !Map.Instance.IsFreshwaterAt(t.X, t.Y));
				foreach (IBuilding building in Reflect.GetBuildings().Where(b => Player.ProductionAvailable(b) && !_buildings.Any(x => x.Id == b.Id)))
				{
					if (HasBuilding<Palace>() && building is Courthouse) continue;
					if (building is Shipyard && !coastal) continue;
					if (building is SeaPlatform && !coastal) continue;
					if (building is HydroPlant && !CityTiles.Any(t => t.Type == Terrain.River)) continue;
					yield return building;
				}
				foreach (IWonder wonder in Reflect.GetWonders().Where(b => Player.ProductionAvailable(b)))
				{
					if (!coastal && (wonder is Lighthouse || wonder is MagellansExpedition || wonder is DarwinsVoyage || wonder is Colossus || wonder is ZhengHeVoyage)) continue;
					if (wonder is ZhengHeVoyage && !Map.AllTiles().Any(t => t.ContinentId != Tile.ContinentId && t.ContinentId > 0 && t.City is not null && t.City.Owner != 0)) continue;
					if (wonder is Wonders.GreatLibrary && !HasBuilding<LibraryBuilding>()) continue;
					if (wonder is Wonders.GreatLibrary && Game.GetPlayer(Owner).Cities.Count(c => c.HasBuilding<LibraryBuilding>()) < 5) continue;
					yield return wonder;
				}
			}
		}

		public void SetProduction(IProduction production)
		{
			bool switchingWonders = CurrentProduction is IWonder && production is IWonder;
			if (!switchingWonders && CurrentProduction is not null && CurrentProduction.GetType() != production.GetType())
				Shields = 0;
			CurrentProduction = production;
		}

		// ── production queue ──────────────────────────────────────────────────

		internal IReadOnlyList<IProduction> ProductionQueue => _productionQueue;

		internal void EnqueueProduction(IProduction item) => _productionQueue.Add(item);

		internal void ClearProductionQueue() => _productionQueue.Clear();

		// Advance to the next queued item without a shield penalty. Returns true if
		// something was dequeued; CurrentProduction is updated but Shields untouched.
		private bool DequeueProduction()
		{
			if (_productionQueue.Count == 0) return false;
			CurrentProduction = _productionQueue[0];
			_productionQueue.RemoveAt(0);
			// Log queue-driven builds too — previously only re-plans (CityProduction calls)
			// were recorded, which biased the histogram toward the head-of-queue items.
			DecisionLogger.LogCityProduction(this, CurrentProduction, "queued", isHuman: (Player == Human));
			return true;
		}

		// Find the next wonder that is not yet globally built and is researchable
		// by this city's player, skipping the optionally supplied beaten wonder.
		private IWonder NextAvailableWonder(IWonder? beaten = null)
		{
			// Prefer a wonder already planned in the queue
			IWonder queued = _productionQueue.OfType<IWonder>()
			    .FirstOrDefault(w => !Game.WonderBuilt(w));
			if (queued is not null) return queued;

			return Reflect.GetWonders()
			    .Where(w => (beaten is null || w.Id != beaten.Id)
			             && !Game.WonderBuilt(w)
			             && Player.ProductionAvailable(w))
			    .FirstOrDefault();
		}

		internal void SetProduction(byte productionId)
		{
			IProduction production = Reflect.GetProduction().FirstOrDefault(p => p.ProductionId == productionId);
			if (production is null)
			{
				Log($"Invalid production ID for {Name}: {productionId}");
				return;
			}
			CurrentProduction = production;
		}

		// Soft strategic-resource gate: without the required material (Iron/Coal/
		// Oil — Game.RequiredResource), the works pay ruinous import prices:
		// +50% shields. Never a wall, always a cost. Used by the completion
		// check, rush-buy, and the city screens, so the higher target is
		// visible wherever progress is shown.
		internal int ProductionCost(IProduction production)
		{
			int cost = (int)production.Price * 10;
			StrategicResource need = Game.RequiredResource(production);
			if (need != StrategicResource.None && !Game.Instance.HasResource(Player, need))
				cost += cost / 2;
			return cost;
		}

		internal short BuyPrice
		{
			get
			{
				// A rush-bought item isn't delivered until next turn's NewTurn, by which
				// point the city banks one more turn of production. Credit that forthcoming
				// output toward the price so the player only pays for what the city can't
				// finish on its own by delivery. ShieldIncome can be negative (units being
				// disbanded for upkeep), so floor the credit at zero.
				int target    = ProductionCost(CurrentProduction);
				int effective  = (Shields > 0)
					? Math.Min(target, Shields + Math.Max(0, ShieldIncome))
					: 0;

				if (effective > 0)
				{
					if (effective >= target) return 0; // finishes next turn unaided — nothing to buy
					int remaining = target - effective;
					// Thanks to Tristan_C (http://forums.civfanatics.com/threads/buy-unit-building-wonder-price.576026/#post-14490920)
					if (CurrentProduction is IUnit)
					{
						double x = (double)remaining / 10;
						double price = 5 * (x * x) + (20 * x);
						return (short)(Math.Floor(price));
					}
					return (short)(remaining * (CurrentProduction is IWonder ? 4 : 2));
				}
				return CurrentProduction.BuyPrice;
			}
		}

		public bool Buy()
		{
			int buyPrice = BuyPrice;
			if (buyPrice <= 0) return false;
			if (IsInDisorder && CurrentProduction is IBuilding) return false;
			// Charge the city's owner, not Game.CurrentPlayer — the city manager can be
			// opened by tasks inserted during another player's turn processing.
			if (Player.Gold < buyPrice) return false;

			Player.Gold -= (short)buyPrice;
			Shields = ProductionCost(CurrentProduction);
			return true;
		}

		public int Population
		{
			get
			{
				int output = 0;
				for (int i = 1; i <= Size; i++)
				{
					output += 10000 * i;
				}
				return output;
			}
		}

		private readonly List<Citizen> _specialists = new();

		internal IEnumerable<Citizen> Citizens => _cachedCitizens ??= ComputeCitizens().ToList();

		private IEnumerable<Citizen> ComputeCitizens()
		{
			// Happiness pipeline — runs in two passes:
			//
			// Pass 1 (budget): derive the net happy and unhappy integers.
			//   happyCount  starts from luxury income and wonder bonuses.
			//   unhappyCount starts from (size - difficulty_floor), then grows for
			//   Republic/Democracy military-away penalties, then shrinks for each
			//   happiness building/wonder that applies.
			//
			// Pass 2 (assignment): walk the Size citizen slots and emit Citizen
			//   values. Working slots (index < working) pull from the happy/unhappy
			//   budgets; any remaining slots past the worked tiles become specialists.
			//   Specialists are never happy or unhappy — their mood is irrelevant to
			//   disorder.

			// Sync specialist list length with current city size and worked tiles.
			int resourceCount = ResourceTiles.Count();
			while (_specialists.Count < Size - (resourceCount - 1)) _specialists.Add(Citizen.Entertainer);
			while (_specialists.Count > Size - (resourceCount - 1)) _specialists.Remove(_specialists.Last());

			int happyCount = (int)Math.Floor((double)Luxuries / 2);
			if (Player.HasWonder<HangingGardens>() && !Game.WonderObsolete<HangingGardens>()) happyCount++;
			if (Player.HasWonder<CureForCancer>()) happyCount++;
			if (Player.HasWonder<TajMahal>()) happyCount++;

			// Empire-size unhappiness (Civ 1's "number of cities" penalty): the larger
			// the empire, the fewer citizens are naturally content, and past a second
			// threshold extra malcontents ("red shirts") appear in every city. This is
			// the balance counterweight to unlimited expansion — without it a runaway
			// empire keeps its 70th city as easy to please as its 3rd. Difficulty brings
			// it on sooner. Tunable: raise EmpireFree/RedShirtFree or the steps to soften.
			// Difficulty is the HUMAN's handicap, not a world-wide malaise. This read
			// Game.Difficulty for every player, so raising the level took two content
			// citizens from every AI city as well — and the AI is far less able to absorb
			// that: it reaches Temples late, manages the luxury slider badly, and its cities
			// are small to begin with. The effect was that a harder setting produced a
			// WEAKER world to play against rather than a stronger one.
			//
			// The asymmetry matches how research already works (Player.ScienceCost: the
			// human pays Difficulty + 3, the AI a flat 3), including the Autopilot clause —
			// when the AI is steering the human's civ it gets the AI's terms, which also
			// keeps autoplay runs representative of AI behaviour.
			//
			// The empire-size penalty below is deliberately NOT exempted: that is the
			// counterweight to unlimited expansion and it should bite everyone.
			bool aiRun = Player != Human || Settings.Instance.Autopilot;
			int handicap = aiRun ? 0 : Game.Difficulty;

			int empireCities = Player.Cities.Length;
			int contentFloor = 6 - handicap;
			const int EmpireStep = 8;                    // -1 content per this many cities…
			int empireFree = Math.Max(6, 12 - handicap); // …beyond this many
			if (empireCities > empireFree)
				contentFloor -= (empireCities - empireFree) / EmpireStep;
			if (contentFloor < 0) contentFloor = 0;

			int unhappyCount = Size - contentFloor - happyCount;

			// Red shirts: a truly sprawling empire piles extra unhappy onto every city.
			const int RedShirtFree = 38;                 // no extra unhappy up to here
			const int RedShirtStep = 12;                 // +1 unhappy per this many beyond
			if (empireCities > RedShirtFree)
				unhappyCount += (empireCities - RedShirtFree) / RedShirtStep;
			if (Player.Government.WarWeariness > 0)
			{
				int penalty = Player.Government.WarWeariness;
				if (Player.HasWonder<WomensSuffrage>()) penalty = Math.Max(0, penalty - 1);
				int militaryAway = Units.Count(u => !(u is Diplomat) && !(u is ICaravan) && !(u is Settlers) && (u.X != X || u.Y != Y));
				unhappyCount += militaryAway * penalty;
			}
			else if (Player.Government.MartialLaw)
			{
				// Martial law (Civ 1): under authoritarian rule (Anarchy/Despotism/
				// Monarchy/Communism) each garrisoned military unit keeps one citizen
				// content, up to three — the classic tool for holding a large despotic
				// city together with a garrison. Its absence let big low-trade cities
				// riot indefinitely even at maximum luxury, with no reachable escape.
				int garrison = Tile.Units.Count(u => !(u is Settlers) && !(u is Diplomat) && !(u is ICaravan));
				unhappyCount -= Math.Min(3, garrison);
			}
			// Pollution unhappiness: each city pays for its own smog. SmokeStacks is
			// already post-tolerance (City.cs:1009 subtracts 20 free units), so a city
			// only feels social cost after it's industrialized past the absorbable level.
			// Mitigation is the existing chain — Recycling Center (industrial /3),
			// Hydro/Nuclear/Hoover (industrial /2), Mass Transit (pop pollution = 0).
			// Shakespeare's Theatre below still zeroes everything, so a single global
			// "happiness wonder" remains the full counter for an industrial powerhouse.
			unhappyCount += SmokeStacks / 10;
			// The Greys: one permanently unhappy citizen — nobody likes the houseguests.
			if (Game.Instance.GreyCities.Contains((X, Y))) unhappyCount++;
			// The Other Voice: the dread of true prophecy sits on the Oracle
			// keeper's whole empire while the voice speaks.
			if (Game.Instance.OracleVoiceActive && Player.HasWonder<Oracle>() && !Game.WonderObsolete<Oracle>())
				unhappyCount++;
			// The King in Yellow: an afflicted stage loses the Theatre's charm entirely.
			bool maskUponUs = Game.Instance.YellowCities.Contains((X, Y));
			if (HasWonder<ShakespearesTheatre>() && !Game.WonderObsolete<ShakespearesTheatre>() && !maskUponUs)
			{
				unhappyCount = 0;
			}
			else
			{
				// Stonehenge: every city shares a Temple's peace, present and future
				// (Michelangelo-style computed effect; expires with Religion).
				if (HasBuilding<Temple>()
				    || (Player.HasWonder<Wonders.Stonehenge>() && !Game.WonderObsolete<Wonders.Stonehenge>()))
				{
					int templeEffect = 1;
					if (Player.HasAdvance<Mysticism>()) templeEffect <<= 1;
					if (Player.HasWonder<Oracle>() && !Game.WonderObsolete<Oracle>()) templeEffect <<= 1;
					unhappyCount -= templeEffect;
				}
				if (Tile is not null && Map.ContentCities(Tile.ContinentId).Any(x => x.Size > 0 && x.Owner == Owner && x.HasWonder<JSBachsCathedral>()))
				{
					unhappyCount -= 2;
				}
				if (HasBuilding<Colosseum>()) unhappyCount -= 3;
				if (HasBuilding<Hospital>()) unhappyCount -= 2;
				if (HasBuilding<ExchangeCenter>()) unhappyCount -= 1;
				if (HasBuilding<NeuralLab>()) unhappyCount -= 1;
				if (HasBuilding<CivicMonument>()) unhappyCount -= 1;
				bool chapelOnContinent = !Game.WonderObsolete<MichelangelosChapel>() &&
					Tile is not null &&
					Map.ContentCities(Tile.ContinentId).Any(x => x.Size > 0 && x.Owner == Owner && x.HasWonder<MichelangelosChapel>());
				bool hagiaSofiaActive = Player.HasWonder<HagiaSofia>() && !Game.WonderObsolete<HagiaSofia>();
				if (HasBuilding<Cathedral>())
					unhappyCount -= chapelOnContinent ? (hagiaSofiaActive ? 8 : 6) : (hagiaSofiaActive ? 6 : 4);
				else if (chapelOnContinent)
					unhappyCount -= 4;
			}

			// The King in Yellow: two citizens have seen the play and cannot
			// unsee it. Applied last — the mask outplays the bard; only a
			// Cathedral cures it (Game.ProcessKingInYellow).
			if (maskUponUs) unhappyCount += 2;

			int content = 0;
			int unhappy = 0;
			int working = resourceCount - 1;
			int specialist = 0;

			for (int i = 0; i < Size; i++)
			{
				if (i < working)
				{
					if (happyCount-- > 0)
					{
						yield return (i % 2 == 0) ? Citizen.HappyMale : Citizen.HappyFemale;
						continue;
					}
					if ((unhappyCount - (working - i)) >= 0)
					{
						unhappyCount--;
						yield return ((unhappy++) % 2 == 0) ? Citizen.UnhappyMale : Citizen.UnhappyFemale;
						continue;
					}
					yield return ((content++) % 2 == 0) ? Citizen.ContentMale : Citizen.ContentFemale;
					continue;
				}
				yield return _specialists[specialist++];
			}
		}
		internal void ChangeSpecialist(int index)
		{
			while (_specialists.Count < (index + 1)) _specialists.Add(Citizen.Entertainer);
			_specialists[index] = (Citizen)((((int)_specialists[index] - 5) % 3) + 6);
			InvalidateCache();
		}

		private IEnumerable<ITile> CityTiles
		{
			get
			{
				ITile[,] tiles = CityRadius;
				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					if (tiles[xx, yy] is null) continue;
					yield return tiles[xx, yy];
				}
			}
		}

		public ITile[,] CityRadius
		{
			get
			{
				Player player = Game.Instance.GetPlayer(Owner);
				ITile[,] tiles = Map[X - 2, Y - 2, 5, 5];
				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					ITile tile = tiles[xx, yy];
					if (tile is null) continue;
					if ((xx == 0 || xx == 4) && (yy == 0 || yy == 4)) tiles[xx, yy] = null!;
					if (!player.Visible(tile)) tiles[xx, yy] = null!;
				}
				return tiles;
			}
		}

		private readonly List<IUnit> _homeUnits = [];
		internal void AddHomeUnit(IUnit unit)    { if (!_homeUnits.Contains(unit)) _homeUnits.Add(unit); }
		internal void RemoveHomeUnit(IUnit unit) => _homeUnits.Remove(unit);
		public IUnit[] Units => _homeUnits.ToArray();

		public ITile Tile => Map[X, Y];

		public bool BuildingSold { get; private set; }

		public void AddBuilding(IBuilding building)
		{
			_buildings.Add(building);
			InvalidateCache();
		}

		public void SellBuilding(IBuilding building)
		{
			RemoveBuilding(building);
			// Credit the city's owner, not Game.CurrentPlayer (see Buy).
			Player.Gold += building.SellPrice;
			BuildingSold = true;
		}

		public void RemoveBuilding(IBuilding building)
		{
			_buildings.RemoveAll(b => b.Id == building.Id);
			InvalidateCache();
		}

		public void RemoveBuilding<T>() where T : IBuilding
		{
			_buildings.RemoveAll(b => b is T);
			InvalidateCache();
		}

		public void AddWonder(IWonder wonder)
		{
			_wonders.Add(wonder);
			InvalidateCache();
			Game.InvalidateBuiltWondersSafe();
			if (Game.Started)
			{
				if (wonder is Colossus && !Game.WonderObsolete<Colossus>())
				{
					ResetResourceTiles();
				}
				if ((wonder is Lighthouse && !Game.WonderObsolete<Lighthouse>()) ||
					(wonder is MagellansExpedition && !Game.WonderObsolete<MagellansExpedition>()))
				{
					// Apply Lighthouse/Magellan's Expedition wonder effects in the first turn
					foreach (IUnit unit in Game.GetUnits().Where(x => x.Owner == Owner && x.Class ==  UnitClass.Water && x.MovesLeft == x.Move))
					{
						unit.MovesLeft++;
					}
				}
			}
		}

		public void UpdateResources()
		{
			// Relocate any worked tile that has gone invalid (a foreign unit stepped on
			// it, or a neighbour claimed it). Materialise the list first — RelocateResourceTile
			// mutates _resourceTiles, which would corrupt a lazy enumeration mid-loop.
			// RelocateResourceTile swaps each invalid tile for the best available valid one,
			// preserving the worked-tile COUNT. That alone cures the original zero-tile
			// starvation bug: a city no longer bleeds worked tiles down to nothing when
			// several are blocked at once — each is individually swapped while free land
			// exists. Do NOT force a refill-to-Size here: a city legitimately sitting at
			// zero worked tiles has had its citizens made specialists BY THE PLAYER (all
			// musicians for happiness under Republic/Democracy); those tiles were never in
			// _resourceTiles for the loop to relocate, so refilling would just overwrite the
			// player's choice every turn. Involuntary gaps self-heal on the next size change
			// (growth/starvation both call SetResourceTiles) or when the player visits.
			foreach (ITile tile in ResourceTiles.Where(t => InvalidTile(t)).ToList())
			{
				RelocateResourceTile(tile);
			}

			// ...but an AI city stripped to zero worked tiles IS always involuntary: the
			// AI never assigns specialists anywhere (grep AI*.cs for Entertainer — no
			// hits), so "all citizens are musicians" is never something it chose. Left
			// alone, such a city works only its centre, turns every citizen into an
			// entertainer, and starves — a capital at -7 food with its whole population
			// making music. Refill it. The player's own cities keep the hands-off
			// treatment described above, since there the allocation may be deliberate.
			if (_resourceTiles.Count == 0 && Size > 0
			    && (Player != Human || Settings.Instance.Autopilot))
				SetResourceTiles();
		}

		// Industrial + population pollution, reduced by clean-power buildings.
		// Pollution BEFORE the tolerance is subtracted. SmokeStacks only becomes non-zero
		// once a city is already over the line and rolling for a polluted tile every turn —
		// so an AI that waits for it is always cleaning up after damage it has already done,
		// and a Mass Transit takes many turns to build. This exposes the pressure so the
		// mitigation can be started while there is still time for it to matter.
		internal int PollutionPressure => RawPollution;

		public int SmokeStacks => Math.Max(0, RawPollution - 20); // first 20 units are tolerated

		private int RawPollution
		{
			get
			{
				int industrial = ShieldTotal;
				if (HasBuilding<Buildings.RecyclingCenter>()) industrial /= 3;
				else if (HasBuilding<Buildings.HydroPlant>() || HasBuilding<Buildings.NuclearPlant>() || HooverDamActive) industrial /= 2;

				int popMult = 100;
				if (HasBuilding<Buildings.MassTransit>())       popMult = 0;
				else if (Player.HasAdvance<Advances.Plastics>())        popMult = 100;
				else if (Player.HasAdvance<Advances.MassProduction>())  popMult = 75;
				else if (Player.HasAdvance<Advances.Automobile>())      popMult = 50;
				else if (Player.HasAdvance<Advances.Industrialization>()) popMult = 25;
				else                                                     popMult = 0;

				return industrial + (Size * popMult / 100);
			}
		}

		private bool GeneratePollution()
		{
			if (SmokeStacks == 0) return false;
			int cap = Math.Max(2, 256 - (Player.Advances.Length * (1 + Game.Difficulty) / 2));
			return (2 * SmokeStacks) > Common.Random.Next(cap);
		}

		private void ExecutePollution()
		{
			if (!GeneratePollution()) return;

			var candidates = CityTiles.Where(t => !t.Pollution && t.City is null && !t.IsOcean).ToList();
			if (candidates.Count == 0) return;

			candidates[Common.Random.Next(candidates.Count)].Pollution = true;

			if (Human == Owner)
				GameTask.Enqueue(Show.EventArt("pollution", $"Pollution in {Name}!"));
		}

		private void ExecuteMeltdown()
		{
			// Destroy the Nuclear Plant
			RemoveBuilding<NuclearPlant>();

			// Reduce population
			if (Size > 1) Size = (byte)Math.Max(1, Size - 2);

			// Spread fallout across entire city radius
			foreach (ITile tile in CityTiles.Where(t => !t.Pollution && !t.IsOcean && t.City is null))
				tile.Pollution = true;

			// Disband all units in the 3×3 blast zone
			foreach (ITile tile in Map.QueryMapPart(X - 1, Y - 1, 3, 3))
			{
				if (tile is null) continue; // map-edge cities get null tiles past the pole
				IUnit[] victims = tile.Units.ToArray();
				foreach (IUnit u in victims)
					Game.DisbandUnit(u);
			}

			if (Player == Human)
			{
				Common.GamePlay?.CenterOnPoint(X, Y);
				int px = (X - (Common.GamePlay?.X ?? 0)) * 16;
				int py = (Y - (Common.GamePlay?.Y ?? 0)) * 16;
				GameTask.Insert(Show.Nuke(px, py));
				GameTask.Insert(Show.EventArt("nuclearmeltdown", $"Nuclear meltdown in {Name}!"));
			}

			Log($"Nuclear meltdown in {Name} (owned by {Player.TribeName})");
		}

		public void NewTurn()
		{
			// Turn processing order — each stage feeds the next:
			//   1. UpdateResources + ExecutePollution  — tile state settled first.
			//   2. Cache snapshot — ShieldIncome/FoodIncome/Citizens computed once;
			//      later mutations don't invalidate mid-turn reads.
			//   3. Disorder — riots escalate (burning buildings, revolt); or order restored.
			//   4. We Love the King — triggers on happiness surplus; grants growth or Caravan.
			//   5. Food — grows or starves the city; Granary half-refill applied here.
			//   6. Shields — accumulate toward production; negative shield income disbands
			//      the most distant home unit.
			//   7. Production completion — unit/building/wonder/SS part built when shields full.
			// Reset cached tile yields so changes from the previous turn (irrigation,
			// railroad, pollution) are reflected before any income is read this turn.
			InvalidateCache();
			UpdateResources();
			ExecutePollution();

			// Cache expensive per-city computations once for the whole turn.
			int shieldIncome = ShieldIncome;
			int foodIncome   = FoodIncome;
			Citizen[] citizensSnapshot = Citizens.ToArray();
			int happyCit   = citizensSnapshot.Count(c => c == Citizen.HappyMale   || c == Citizen.HappyFemale);
			int unhappyCit = citizensSnapshot.Count(c => c == Citizen.UnhappyMale || c == Citizen.UnhappyFemale);
			int contentCit = citizensSnapshot.Count(c => c == Citizen.ContentMale || c == Citizen.ContentFemale);
			bool inDisorder = _size > 0 && unhappyCit > happyCit;

			if (inDisorder)
			{
				if (Common.Random.Next(20) == 1 && HasBuilding<Buildings.NuclearPlant>() && !Player.HasAdvance<Advances.FusionPower>())
					ExecuteMeltdown();

				if (DisorderTurns == 0)
				{
					if (Player == Human)
					{
						GameTask.Insert(Show.EventArt("civilunrest0", $"Civil disorder in {Name}!"));
					}
					Log($"City {Name} belonging to {Player.TribeName} has gone into disorder");
				}
				else
				{
					if (Player == Human)
						GameTask.Insert(Message.Advisor(Advisor.Domestic, true, "Civil Disorder in", $"{Name}! Mayor", "flees in panic."));

					switch (DisorderTurns)
					{
						case 1:
							if (HasBuilding<Buildings.MarketPlace>())
							{
								RemoveBuilding<Buildings.MarketPlace>();
								if (Player == Human)
									GameTask.Insert(Show.EventArt("civilunrest1", $"Marketplace burned in {Name}!"));
							}
							break;
						case 2:
							if (HasBuilding<Bank>())
							{
								RemoveBuilding<Bank>();
								if (Player == Human)
									GameTask.Insert(Show.EventArt("civilunrest2", $"Bank looted in {Name}!"));
							}
							else if (HasBuilding<Cathedral>())
							{
								RemoveBuilding<Cathedral>();
								if (Player == Human)
									GameTask.Insert(Show.EventArt("civilunrest2", $"Cathedral burned in {Name}!"));
							}
							break;
						case 3:
							if (Player.Government.CollapsesInDisorder)
							{
								Player.Revolt();
								if (Player == Human)
									GameTask.Insert(Show.EventArt("governmentcollapses", "The government has COLLAPSED!"));
							}
							break;
					}
				}
				DisorderTurns++;
			}
			else
			{
				if (DisorderTurns > 0)
				{
					if (Player == Human)
						GameTask.Insert(Message.Advisor(Advisor.Domestic, true, "Order restored", $" in {Name}."));
					Log($"City {Name} belonging to {Player.TribeName} is no longer in disorder");
				}
				DisorderTurns = 0;
			}
 			if (unhappyCit == 0 && happyCit >= contentCit && Size >= 3)
			{
				if (!WasWeLoveKing)
				{
					WasWeLoveKing = true;
					// First-time benefit: growth or caravan (only with positive food income)
					if (Player.Government.CelebrationGrowsCity)
					{
						if (foodIncome > 0)
						{
							bool blockedByAqueduct = (Size >= 7  && !HasBuilding<Aqueduct>());
							bool blockedBySewer    = (Size >= 12 && !HasBuilding<SewerSystem>());
							bool blocked = blockedByAqueduct || blockedBySewer;
							if (!blocked)
							{
								Size++;
							}
							else
							{
								var caravan = Game.Instance.CreateUnit(UnitType.Caravan, X, Y, Owner)!;
								caravan.SetHome(this);
								if (Human == Owner)
								{
									string reason = blockedBySewer
										? "No room to grow without a Sewer System."
										: "No room to grow without an Aqueduct.";
									GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false,
										$"{Name} celebration: free Caravan!", reason));
								}
							}
						}
					}
					if (Human == Owner)
						GameTask.Enqueue(Show.EventArt("welovethekingday", $"We Love the King Day in {Name}!"));
					WLTKNotifications.Add(Name);
				}
				else
				{
					WLTKNotifications.Remove(Name); // expire after one turn
				}
			}
			else
			{
				if (WasWeLoveKing)
					WLTKNotifications.Remove(Name);
				WasWeLoveKing = false;
			}
 			Food += inDisorder ? 0 : foodIncome;
			if (!inDisorder && foodIncome > 0 && HasBuilding<SurplusDepot>())
				Player.Gold += (short)(foodIncome / 2);

			if (Food < 0)
			{
				Food = 0;
				Size--;
				if (Human == Owner)
				{
					GameTask.Enqueue(Show.EventArt("famine", $"Famine in {Name}!"));
				}
				if (Size == 0) return;
			}
			else if (Food > FoodRequired)
			{
				Food -= FoodRequired;

				// Growth caps: no advisor message — it re-fired every time the food store
				// refilled, spamming the turn. The City Manager's food storage view and the
				// Aqueduct/Sewer entries in the build list carry the same information.
				if (Size == 7 && !_buildings.Any(b => b.Id == (int)Building.Aqueduct))
				{
					// blocked: needs Aqueduct
				}
				else if (Size == 12 && !_buildings.Any(b => b.Id == (int)Building.SewerSystem))
				{
					// blocked: needs Sewer System
				}
				else
				{
					Size++;
				}

				if (_buildings.Any(b => (b is Granary)))
				{
					if (Food < (FoodRequired / 2))
					{
						Food = (FoodRequired / 2);
					}
				}
			}

			if (shieldIncome < 0)
			{
				IUnit[] homeUnits = Units;
				int maxDistance = homeUnits.Max(u => Common.DistanceToTile(X, Y, u.X, u.Y));
				IUnit unit = homeUnits.Last(u => Common.DistanceToTile(X, Y, u.X, u.Y) == maxDistance);
				if (Human == Owner)
				{
					Message message = Message.DisbandUnit(this, unit);
					message.Done += (s, a) => {
						Game.DisbandUnit(unit);
					};
					GameTask.Enqueue(message);
				}
				else
				{
					Game.DisbandUnit(unit);
				}
			}
			else if (shieldIncome > 0)
			{
				// AI cities ignore the disorder-blocks-production rule (asymmetric cheat:
				// the AI lacks the rate-sliders and Entertainer-specialist tools a human
				// uses to escape disorder, so without this relaxation a single unhappy
				// citizen freezes its production indefinitely). Food is still blocked by
				// disorder above — the city stops growing, it just doesn't stop building.
				//
				// Autopilot counts as AI here, matching City.cs:1187 and :2011. The human
				// slot under autopilot is driven by the same AI that gets this relaxation
				// everywhere else, so without it that civ alone kept the human penalty
				// while keeping none of the human's tools: an autoplayed Japan sat on one
				// rioting city for 250 turns, production frozen, locked on a wonder it
				// could never finish and therefore never re-planning.
				if (!inDisorder || Player != Human || Settings.Instance.Autopilot)
				{
					int income = shieldIncome;
					// Higher difficulties give AI cities a production bonus (classic Civ 1 "cheat").
					// +25 % per difficulty step — double speed at Emperor.
					if (Player != Human && Game.Difficulty > 0)
						income += income * Game.Difficulty / 4;

					// Bond-donor diversion: cities producing Infrastructure Bond under Adam Smith's
					// Trading House export this turn's shields to the player pool instead of
					// accumulating them locally. Player.NewTurn distributes the pool.
					if (CurrentProduction is InfrastructureBond && Player.HasWonder<AdamSmithsTradingHouse>())
						Player.BondPool += income;
					// Research Grant: the city's whole output becomes research. Applied here
					// so it lands before the ProcessScience below picks the total up on the
					// same turn, exactly as a city's own science does.
					else if (CurrentProduction is ResearchGrant)
						Player.Science = (short)Math.Min(short.MaxValue, Player.Science + income);
					else
						Shields += income;
				}
			}

			// A Research Grant is never finished — it is a standing commitment, not a
			// building. Guarded here rather than by keeping Shields at zero, because
			// shields can also arrive from outside (the bond pool), and one stray
			// donation would otherwise "complete" it and hand the city a phantom
			// improvement it can never use.
			if (CurrentProduction is not ResearchGrant
			    && CurrentProduction is not null && Shields >= ProductionCost(CurrentProduction))
			{
				if (CurrentProduction is Settlers && Size == 1 && Game.Difficulty == 0 && !Settings.Instance.Autopilot)
				{
					// On Chieftain level, it's not possible to create a Settlers in a city of size 1
					// (protects the player's only city from accidentally destroying itself). In
					// Autopilot the AI is steering, so let the auto-grow trick on line 1293 fire
					// — otherwise the city stalls forever with completed-but-uncreated Settlers.
				}
				else if ((CurrentProduction is Settlers || CurrentProduction is HydroEngineer || CurrentProduction is Longboat)
				         && Size == 1 && Player.Cities.Length > 1 && !Player.IsHuman)
				{
					// A Settlers/HydroEngineer costs 1 population. The only-city case is rescued by
					// the Size++ bump below, but a size-1 town in a MULTI-city civ would drop to 0
					// and be destroyed. That's how famished AI towns vanish: they queue a settler at
					// a healthy size, famine down to 1, then the completed settler wipes the town —
					// so for the AI we hold instead, keeping the shields until the city regrows.
					// The HUMAN is exempt (!Player.IsHuman): abandoning a size-1 city by completing
					// a Settler — relocating its last population — is a deliberate, legitimate move.
				}
				else if (CurrentProduction is IUnit currentUnit)
				{
					// Create BEFORE spending. Game.CreateUnit is a hand-written switch,
					// so a unit type added to the enum without a matching case returns
					// null — and this used to zero the shields first, so the city paid
					// in full, got nothing, and started over. That reads to the player
					// as production mysteriously resetting, over and over.
					IUnit? built = Game.Instance.CreateUnit(currentUnit.Type, X, Y, Owner);
					if (built is null)
					{
						Log($"{Name}: cannot create {currentUnit.Type} — no factory case; keeping shields");
						return;
					}
					Shields = 0;
					IUnit unit = built;
					bool sunTzu = Player.HasWonder<SunTzusWarAcademy>() && !Game.WonderObsolete<SunTzusWarAcademy>();
					unit.Veteran = (_buildings.Any(b => (b is Barracks)))
						|| (sunTzu && unit.Class == UnitClass.Land && unit.Attack > 0);
					if (CurrentProduction is Settlers || CurrentProduction is HydroEngineer || CurrentProduction is Longboat)
					{
						if (Size == 1 && Player.Cities.Length == 1) Size++;
						if (Size > 1) unit.SetHome();
						Size--;
					}
					else
					{
						unit.SetHome();
					}
					if (Human == Owner && (unit is Settlers || unit is HydroEngineer || unit is Diplomat || unit is ICaravan))
					{
						GameTask.Enqueue(new ImprovementBuilt(this, unit));
					}
					if (!(CurrentProduction is Settlers || CurrentProduction is HydroEngineer || CurrentProduction is Longboat || CurrentProduction is Diplomat || CurrentProduction is ICaravan))
					{
						string? uname = (CurrentProduction as ICivilopedia)?.Name;
						if (uname is not null && !Game.Instance.GetReplayData<ReplayData.UnitBuilt>().Any(u => u.UnitName == uname))
							Game.Instance.AddReplayEvent(new ReplayData.UnitBuilt(Game.GameTurn, Owner, uname));
					}
				}
				if (CurrentProduction is ISpaceShip)
				{
					// SS parts are tracked as player-level counters, not city buildings,
					// so the city can repeat-build the same part.
					Shields = 0;
					int playerIndex = Owner;
					// Clamped as well as gated in BuildingAvailable: a part already sitting in
					// the production QUEUE was validated when it was queued, so the cap has to
					// hold here too or the queue becomes a way around it.
					if (CurrentProduction is Buildings.SSStructural)
						Game.SpaceshipStructural[playerIndex] = Math.Min(Game.MaxSpaceshipStructural, Game.SpaceshipStructural[playerIndex] + 1);
					else if (CurrentProduction is Buildings.SSComponent)
						Game.SpaceshipComponent[playerIndex]  = Math.Min(Game.MAX_SS_COMPONENT, Game.SpaceshipComponent[playerIndex] + 1);
					else if (CurrentProduction is Buildings.SSModule)
						Game.SpaceshipModule[playerIndex]     = Math.Min(Game.MAX_SS_MODULE, Game.SpaceshipModule[playerIndex] + 1);
					// Deliberately silent. A spaceship is dozens of identical parts, and this
					// announced every one of them — for EVERY civ, since nothing here gated on
					// the human — then opened the building city. During a space race that is a
					// popup every turn or two about someone else's factory. The parts are
					// visible on the SpaceShips screen, which is where a player tracking the
					// race is already looking. (Launches still announce; see Game.cs:1074.)
				}
				else if (CurrentProduction is IBuilding currentBuilding && !_buildings.Any(b => b.Id == currentBuilding.Id))
				{
					Shields = 0;
					if (CurrentProduction is Palace)
					{
						foreach (City city in Game.Instance.GetCities().Where(c => c.Owner == Owner))
						{
							// Remove palace from all cites.
							city.RemoveBuilding<Palace>();
						}
						if (HasBuilding<Courthouse>())
						{
							_buildings.RemoveAll(x => x is Courthouse);
						}
						_buildings.Add(currentBuilding);

						// Only the player whose capital actually moved. This fired for every
						// civ's Palace, so a rival relocating its seat of government opened a
						// newspaper, an advisor and then that civ's City Manager.
						if (Player == Human)
						{
							Message message = Message.Newspaper(this, $"{this.Name} builds", $"{(CurrentProduction as ICivilopedia)?.Name}.");
							message.Done += (s, a) => {
								GameTask advisorMessage = Message.Advisor(Advisor.Foreign, true, $"{Player.TribeName} capital", $"moved to {Name}.");
								advisorMessage.Done += (s1, a1) => GameTask.Insert(Show.CityManager(this));
								GameTask.Enqueue(advisorMessage);
							};
							GameTask.Enqueue(message);
						}
					}
					else
					{
						_buildings.Add(currentBuilding);
						GameTask.Enqueue(new ImprovementBuilt(this, currentBuilding));
					}
					string? bname = (CurrentProduction as ICivilopedia)?.Name;
					if (bname is not null && !Game.Instance.GetReplayData<ReplayData.BuildingBuilt>().Any(b => b.BuildingName == bname))
						Game.Instance.AddReplayEvent(new ReplayData.BuildingBuilt(Game.GameTurn, Owner, bname));
				}
				if (CurrentProduction is IWonder wonder)
				{
					if (!Game.WonderBuilt(wonder))
					{
						Shields = 0;
						AddWonder(wonder);
						Game.Instance.AddReplayEvent(new ReplayData.WonderBuilt(Game.GameTurn, Owner, (wonder as ICivilopedia).Name, X, Y));
						var impTask = new ImprovementBuilt(this, wonder);
						if (wonder is Wonders.SouthPoleExpedition)
						{
							// Whoever builds the wonder brings back the curse: the anomaly can
							// be the propulsion cache — or something that was waiting in the ice.
							City? infected = Game.Instance.TrySouthPoleCurse(Player, this);
							string gameYear = Game.GameYear;
							if (Player == Human)
							{
								if (infected is null)
									Game.SpaceshipComponent[Game.PlayerNumber(Player)] += 2;
								Game.Instance.RecordTransmission("SouthPoleExpedition", gameYear);
								impTask.Done += (s, a) => GameTask.Enqueue(Show.Screen(new SouthPoleExpeditionLog(gameYear)));
							}
							if (infected is not null)
							{
								// The reveal plays for the human either way — it is world news.
								// For the human builder it lands right after the expedition log.
								string infectedName = infected.Name;
								impTask.Done += (s, a) =>
								{
									string? thingArt = EventArtScreen.FindPath("TheThing");
									if (thingArt is not null)
										GameTask.Enqueue(Show.Screen(new EventArtScreen(thingArt, $"QUARANTINE — {infectedName.ToUpper()} HAS GONE DARK")));
									GameTask.Enqueue(Show.Screen(new Screens.ThingOutbreakTransmission(gameYear, infectedName)));
								};
							}
						}
						if (wonder is Wonders.ThePortal)
						{
							// Contact resolves immediately for any builder; the reveal plays
							// for the human either way — global peace or new houseguests are
							// both world news.
							bool greys = Game.Instance.OpenPortal(Player, this);
							string portalCity = Name;
							impTask.Done += (s, a) =>
							{
								if (greys)
								{
									string? greysArt = EventArtScreen.FindPath("TheGreys");
									if (greysArt is not null)
										GameTask.Enqueue(Show.Screen(new EventArtScreen(greysArt,
											$"CONTACT — THEY LIKE IT IN {portalCity.ToUpper()}")));
									GameTask.Enqueue(Message.Newspaper(null!, "Contact made!",
										"Visitors arrive in numbers.", "They do not appear to work."));
								}
								else
								{
									GameTask.Enqueue(Message.Newspaper(null!, "Contact made!",
										"Luminous beings counsel peace.", "Every war on Earth has ended."));
								}
							};
						}
						if (wonder is Wonders.NanobotFactory)
						{
							// 1/4: the replication bound does not hold. The goo seeds under
							// the factory; the doubling clock starts (Game.ProcessGreyGoo).
							bool goo = Settings.Instance.CursedWonders && Common.Random.Next(4) == 0;
							if (goo)
							{
								Game.Instance.SeedGreyGoo(this);
								string gooCity = Name;
								impTask.Done += (s, a) =>
								{
									string? gooArt = EventArtScreen.FindPath("GreyGoo");
									if (gooArt is not null)
										GameTask.Enqueue(Show.Screen(new EventArtScreen(gooArt,
											$"CONTAINMENT FAILURE — {gooCity.ToUpper()}")));
									GameTask.Enqueue(Message.Newspaper(null!, "Containment failure!",
										$"A grey tide spreads from {gooCity}.", "It is eating the ground."));
								};
							}
							else if (Player == Human)
							{
								impTask.Done += (s, a) => GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
									"The assemblers are online.",
									"Field refits will proceed",
									"automatically, free of charge."));
							}
						}
						if (wonder is Wonders.Oracle && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the Oracle answers, and it is not Apollo (docs/cursed_wonders.md
							// #11). True prophecies for the keeper, dread for the empire, until
							// Religion silences it (Game.ProcessOracleVoice).
							Game.Instance.OracleVoiceActive = true;
							impTask.Done += (s, a) =>
							{
								string? voiceArt = EventArtScreen.FindPath("OtherVoice");
								if (voiceArt is not null)
									GameTask.Enqueue(Show.Screen(new EventArtScreen(voiceArt,
										"THE ORACLE ANSWERS — IT IS NOT APOLLO")));
								GameTask.Enqueue(Message.Newspaper(null!, "The Oracle speaks!",
									"The priests will not repeat",
									"what it said first."));
							};
						}
						if (wonder is Wonders.Stonehenge && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the circle is a door (docs/cursed_wonders.md #5). The city
							// is halved, a Guardian stands in the stones, and the tithe runs
							// until it falls (Game.ProcessStoneDoor). The free temples still
							// arrive — the druids got that part right.
							Game.Instance.OpenStoneDoor(this);
							if (Game.Instance.DoorState == 1)
							{
								string circle = Name;
								impTask.Done += (s, a) =>
								{
									string? doorArt = EventArtScreen.FindPath("TheDoor");
									if (doorArt is not null)
										GameTask.Enqueue(Show.Screen(new EventArtScreen(doorArt,
											"THE CIRCLE IS A DOOR")));
									GameTask.Enqueue(Message.Newspaper(null!, "The stones scream!",
										$"Half of {circle} is gone.",
										"Something stands in the circle."));
								};
							}
						}
						if (wonder is Wonders.Pyramids && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the alignment is a beacon (docs/cursed_wonders.md #4).
							// The wonder city is visited for the next four thousand years
							// (Game.ProcessVisitations) — no counterplay, by design.
							Game.Instance.VisitationsActive = true;
							Game.Instance.VisitationsX = X;
							Game.Instance.VisitationsY = Y;
							string monument = Name;
							impTask.Done += (s, a) =>
							{
								string? tapestry = EventArtScreen.FindPath("Visitations");
								if (tapestry is not null)
									GameTask.Enqueue(Show.Screen(new EventArtScreen(tapestry,
										"AS RECORDED IN LATER CENTURIES")));
								GameTask.Enqueue(Message.Newspaper(null!, "The capstone is set!",
									$"Lights stand over {monument}",
									"by night. They do not move."));
							};
						}
						if (wonder is Wonders.Lighthouse && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the light carries farther than intended (docs/cursed_wonders.md
							// #8). Something in the deep answers; it hunts until slain.
							Game.Instance.UnleashLeviathan(this);
							if (Game.Instance.LeviathanState == 1)
							{
								string beacon = Name;
								impTask.Done += (s, a) =>
								{
									string? levArt = EventArtScreen.FindPath("Leviathan");
									if (levArt is not null)
										GameTask.Enqueue(Show.Screen(new EventArtScreen(levArt,
											$"THE LIGHT CARRIES — SOMETHING ANSWERS")));
									GameTask.Enqueue(Message.Newspaper(null!, "Ships vanish!",
										$"Sailors off {beacon} speak",
										"of a shape below the light."));
								};
							}
						}
						if (wonder is Wonders.GreatWall && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the wall was not built to keep them out (docs/cursed_wonders.md
							// #9) — raids double on the builder's continent for sixty turns.
							Game.Instance.WallCurseEndTurn = (uint)(Game.GameTurn + 60);
							Game.Instance.WallCurseContinent = Tile.ContinentId;
							impTask.Done += (s, a) => GameTask.Enqueue(Message.Newspaper(null!,
								"The wall is finished!",
								"Beyond it, the herdsmen report",
								"fires moving closer."));
						}
						if (wonder is Wonders.CureForCancer && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: it cures slightly more than cancer (docs #10) — every city
							// +2 population at once, the granaries emptied by celebration.
							// A windfall for a fed empire, a famine for a hollow one.
							foreach (City boom in Player.Cities.Where(c => c.Size > 0).ToArray())
							{
								boom.Size += 2;
								boom.Food = 0;
							}
							impTask.Done += (s, a) => GameTask.Enqueue(Message.Newspaper(null!,
								"It cures more than cancer!",
								"Population soars overnight.",
								"The granaries stand empty."));
						}
						if (wonder is Wonders.IsaacNewtonsCollege && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the *other* research succeeds (docs #7) — a temporal anomaly
							// settles on the College city for fifty turns (Game.ProcessAnomaly).
							Game.Instance.AnomalyX = X;
							Game.Instance.AnomalyY = Y;
							Game.Instance.AnomalyEndTurn = (uint)(Game.GameTurn + 50);
							string anomalyCity = Name;
							impTask.Done += (s, a) =>
							{
								string? anomalyArt = EventArtScreen.FindPath("Anomaly");
								if (anomalyArt is not null)
									GameTask.Enqueue(Show.Screen(new EventArtScreen(anomalyArt,
										$"THE OTHER WORK — {anomalyCity.ToUpper()}")));
								GameTask.Enqueue(Message.Newspaper(null!, "Alchemy!",
									"Time runs strangely",
									$"in {anomalyCity}."));
							};
						}
						if (wonder is Wonders.ShakespearesTheatre && Settings.Instance.CursedWonders && Common.Random.Next(4) == 0)
						{
							// 1/4: the debut play is the wrong play (docs/cursed_wonders.md #6).
							// The madness starts here and travels the trade routes; a Cathedral
							// cures it (Game.ProcessKingInYellow).
							Game.Instance.YellowCities.Add((X, Y));
							Game.InvalidateCitiesAt(X, Y);
							string stage = Name;
							impTask.Done += (s, a) =>
							{
								string? maskArt = EventArtScreen.FindPath("KingInYellow");
								if (maskArt is not null)
									GameTask.Enqueue(Show.Screen(new EventArtScreen(maskArt,
										$"OPENING NIGHT — {stage.ToUpper()}")));
								GameTask.Enqueue(Message.Newspaper(null!, "The debut is a triumph!",
									"The audience cannot stop", "talking about the play."));
							};
						}
						if (wonder is Wonders.TheInternet)
						{
							// 1/4: the outbreak of Social Media (docs/cursed_wonders.md #2).
							// The split resolves immediately for any builder; too-small
							// empires can't schism and quietly get the blessing.
							Player? splinter = Settings.Instance.CursedWonders && Common.Random.Next(4) == 0
								? Game.Instance.ExecuteSocialMediaSchism(Player, this)
								: null;
							if (splinter is not null)
							{
								string tribe = Player.TribeNamePlural;
								string splinterTribe = splinter.TribeNamePlural;
								impTask.Done += (s, a) =>
								{
									string? schismArt = EventArtScreen.FindPath("SplinterRepublic");
									if (schismArt is not null)
										GameTask.Enqueue(Show.Screen(new EventArtScreen(schismArt,
											$"SCHISM — THE {splinterTribe.ToUpper()} LOG OFF")));
									GameTask.Enqueue(Message.Newspaper(null!, "The feed is poison!",
										$"Half the {tribe} provinces",
										$"secede as the {splinterTribe}."));
								};
							}
							else if (Player == Human)
							{
								impTask.Done += (s, a) => GameTask.Enqueue(Message.Advisor(Advisor.Science, false,
									"The network is online.",
									"Science and culture flow",
									"between every city."));
							}
						}
						if (wonder is Wonders.DarwinsVoyage)
						{
							if (Player == Human)
							{
								// Human gets to choose 2 free advances immediately
								impTask.Done += (s, a) =>
								{
									IScreen ct1 = new ChooseTech();
									ct1.Closed += (s2, a2) =>
									{
										if (Human.CurrentResearch is not null)
										{
											Human.AddAdvance(Human.CurrentResearch);
											Human.CurrentResearch = null;
										}
										IScreen ct2 = new ChooseTech();
										ct2.Closed += (s3, a3) =>
										{
											if (Human.CurrentResearch is not null)
											{
												Human.AddAdvance(Human.CurrentResearch);
												Human.CurrentResearch = null;
											}
											GameTask.Enqueue(new TechSelect(Human));
										};
										Common.AddScreen(ct2);
									};
									Common.AddScreen(ct1);
								};
							}
							else
							{
								// AI gets 2 random available advances
								for (int ii = 0; ii < 2; ii++)
								{
									IAdvance adv = Player.AvailableResearch.FirstOrDefault(a => !(a is FutureTech));
									if (adv is not null) Player.AddAdvance(adv);
								}
							}
						}
						if (wonder is Wonders.InterstellarProbe && Player == Human)
						{
							Game.Instance.ProbeDispatched = true;
							Game.Instance.ProbeDispatchTurn = Game.Instance.GameTurn;
							Game.Instance.ProbeInterimPhase = 0;

							int quality = Game.CalcProbeQuality(Player);
							int tier    = Game.CalcProbeOutcomeTier(quality, Game.Instance.VisitorType);
							Game.Instance.ProbeOutcomeTier = tier;

							// Tech grants are held and applied when the result transmission fires.
							int techCount = tier >= 4 ? 2 : tier >= 3 ? 1 : 0;
							if (techCount > 0)
							{
								Game.Instance.ProbeGrantedAdvanceIds = Human.AvailableResearch
									.Where(a => !(a is FutureTech))
									.Take(techCount)
									.Select(a => (int)a.Id)
									.ToArray();
							}
						}
						if (wonder is MarcoPoloVoyage)
						{
							int continentId = Tile.ContinentId;
							Player.RevealTiles(Map.ContinentTiles(continentId).Where(t => !t.IsOcean));
							Player contact = Map.ContentCities(continentId)
								.Where(c => c.Owner != Owner && c.Owner != 0)
								.GroupBy(c => c.Owner)
								.OrderByDescending(g => g.Count())
								.Select(g => Game.GetPlayer(g.Key))
								.FirstOrDefault();
							if (contact is not null && !Player.HasEmbassy(contact))
								Player.EstablishEmbassy(contact);
							if (Player == Human)
							{
								string line3 = contact is not null ? $"contacts the {contact.TribeName}!" : "maps the continent.";
								impTask.Done += (s, a) => GameTask.Enqueue(
									Message.Advisor(Advisor.Foreign, false, "Marco Polo's Voyage:", "Continent revealed,", line3));
							}
						}
						if (wonder is ZhengHeVoyage)
						{
							int myContinentId = Tile.ContinentId;
							var foreignGroup = Map.AllTiles()
								.Where(t => t.ContinentId != myContinentId && t.ContinentId > 0 && t.City is not null && t.City.Owner != 0)
								.GroupBy(t => t.ContinentId)
								.OrderByDescending(g => g.Count())
								.FirstOrDefault();
							if (foreignGroup is not null)
							{
								Player.RevealTiles(Map.ContinentTiles(foreignGroup.Key));
								Player contact = foreignGroup
									.Select(t => t.City)
									.GroupBy(c => c.Owner)
									.OrderByDescending(g => g.Count())
									.Select(g => Game.GetPlayer(g.Key))
									.FirstOrDefault();
								if (contact is not null && !Player.HasEmbassy(contact))
									Player.EstablishEmbassy(contact);
								if (Player == Human && contact is not null)
								{
									string tribeName = contact.TribeName;
									impTask.Done += (s, a) => GameTask.Enqueue(
										Message.Advisor(Advisor.Foreign, false, "Zheng He's Voyage:", "New continent found,", $"contacts the {tribeName}!"));
								}
							}
						}
						GameTask.Enqueue(impTask);
					}
					else
					{
						// Another civ got there first — roll over to the next available
						// wonder, keeping accumulated shields as a head-start.
						string lostName = (wonder as ICivilopedia).Name;
						IWonder next = NextAvailableWonder(wonder);
						if (next is not null)
						{
							// Remove from queue if it was planned there
							_productionQueue.Remove(next);
							CurrentProduction = next;
							if (Player == Human)
								GameTask.Enqueue(Message.Newspaper(this,
								    $"{lostName} was", "built by another civ.",
								    $"Now building {(next as ICivilopedia).Name}."));
						}
						else
						{
							Shields = 0; // no more wonders — let production re-evaluate
						}
					}
				}
			}

			Player.Gold += IsInDisorder ? (short)0 : Taxes;

			// INSOLVENCY. Player.Gold clamps at zero (Player.cs:205, where this was a TODO),
			// so a treasury that cannot meet the bill simply didn't pay it — silently, and
			// forever. AI.ConsiderDivestment only ever sheds buildings that were provably
			// doing nothing, so an empire full of USEFUL buildings it could not afford ran a
			// free permanent deficit: measured at the end of a 750-turn game, Japan took 62
			// gold a turn against 175 owed and never lost a thing.
			//
			// Civ 1 sells a building instead of forgiving the debt, and so do we. Highest
			// upkeep first, so the fewest buildings go; TotalMaintenance falls as they do, so
			// this converges. A city with nothing left to sell still gets its debt written
			// off — disbanding units for arrears is the other half of the Civ 1 rule and is
			// not implemented here.
			while (Player.Gold < TotalMaintenance && _buildings.Count > 0)
			{
				IBuilding sold = _buildings.OrderByDescending(b => b.Maintenance).First();
				SellBuilding(sold);
				if (Player == Human)
					GameTask.Enqueue(Message.Newspaper(this,
					    $"{Name} cannot pay its bills.", $"{sold.Name} sold to cover the debt."));
			}

			Player.Gold -= TotalMaintenance;
			Player.Science += Science;
			BuildingSold = false;
			GameTask.Enqueue(new ProcessScience(Player));

			if (Shields == 0 && !DequeueProduction() && (Player != Human || Settings.Instance.Autopilot))
				Player.AI?.CityProduction(this);
		}

		public void Disaster()
		{
			List<string> message = new();
			bool humanGetsCity = false;

			if (Player.Cities.Length == 1)
				return;

			if (Size < 5)
				return;

			switch (Common.Random.Next(0, 11))
			{
				case 0: 
				{
					// Earthquake
					bool hillsNearby = CityTiles.Any(t => t.Type == Terrain.Hills);
					IList<IBuilding> buildingsOtherThanPalace = Buildings.Where(b => !(b is Palace)).ToList();
					if (!hillsNearby || !buildingsOtherThanPalace.Any())
						return;
					
					IBuilding buildingToDestroy = buildingsOtherThanPalace[Common.Random.Next(0, buildingsOtherThanPalace.Count)];
					RemoveBuilding(buildingToDestroy);

					message.Add($"Earthquake in {Name}!");
					message.Add($"{buildingToDestroy.Name} destroyed!");

					break;
				}
				case 1:
				{
					// Plague
					bool hasMedicine = Player.HasAdvance<Medicine>() || HasBuilding<Hospital>();
					bool hasAqueduct = HasBuilding<Aqueduct>();
					bool hasConstruction = Player.Advances.Any(a => a is Construction);

					if (!hasMedicine && !hasAqueduct && hasConstruction)
					{
						Size = (byte)(Size - Size / 4);

						message.Add($"Plague in {Name}!");
						message.Add($"Citizens killed!");
						message.Add($"Citizens demand AQUEDUCT.");
					}

					break;
				}
				case 2:
				{
					// Flooding
					bool riverNearby = CityTiles.Any(t => t.Type == Terrain.River);
					bool hasCityWalls = HasBuilding<CityWalls>();
					bool hasMasonry = Player.HasAdvance<Masonry>();

					if (riverNearby && !hasCityWalls && hasMasonry)
					{
						Size = (byte)(Size - Size / 4);

						message.Add($"Flooding in {Name}!");
						message.Add($"Citizens killed!");
						message.Add($"Citizens demand CITY WALLS.");
					}
					break;
				}
				case 3:
				{
					// Volcano
					bool mountainNearby = CityTiles.Any(t => t.Type == Terrain.Mountains);
					bool hasTemple = HasBuilding<Temple>();
					bool hasCeremonialBurial = Player.HasAdvance<CeremonialBurial>();

					if (mountainNearby && !hasTemple && hasCeremonialBurial)
					{
						Size = (byte)(Size - Size / 3);

						message.Add($"Volcano erupts near {Name}!");
						message.Add($"Citizens killed!");
						message.Add($"Citizens demand TEMPLE.");
					}

					break;
				}
				case 4:
				{
					// Famine
					bool hasGranary = HasBuilding<Granary>();
					bool hasPottery = Player.HasAdvance<Pottery>();

					if (!hasGranary)
					{
						Size = (byte)(Size - Size / 3);

						message.Add($"Famine in {Name}!");
						message.Add($"Citizens killed!");
						message.Add(hasPottery ? $"Citizens demand GRANARY." : $"Citizens demand POTTERY.");
					}

					break;
				}
				case 5:
				{
					// Fire
					IList<IBuilding> buildingsOtherThanPalace = Buildings.Where(b => !(b is Palace)).ToList();
					bool hasAqueduct = HasBuilding<Aqueduct>();
					bool hasConstruction = Player.HasAdvance<Construction>();

					if (buildingsOtherThanPalace.Any() && !hasAqueduct && hasConstruction)
					{
						IBuilding buildingToDestroy = buildingsOtherThanPalace[Common.Random.Next(0, buildingsOtherThanPalace.Count)];
						RemoveBuilding(buildingToDestroy);

						message.Add($"Fire in {Name}!");
						message.Add($"{buildingToDestroy.Name} destroyed!");
						message.Add($"Citizens demand AQUEDUCT.");
					}

					break;
				}
				case 6:
				{
					// Pirates
					bool oceanNearby = CityTiles.Any(t => t.Type == Terrain.Ocean);
					bool hasBarracks = HasBuilding<Barracks>();
					if (oceanNearby && !hasBarracks)
					{
						Food = 0;
						Shields = 0;

						message.Add($"Pirates plunder {Name}!");
						message.Add($"Production halted, Food Stolen.!");
						message.Add($"Citizens demand BARRACKS.");
					}

					break;
				}
				case 10:
				{
					// Fever: tropical disease in cities with jungle in the worked tiles,
					// before Medicine is researched. Same magnitude as Plague (¼ size loss).
					// Olvir civs are immune — their biology is adapted. Jungle tiles with
					// a Canopy Array are managed ecology and don't count as fever vectors.
					if (Player.Civilization is Civilizations.Olvir) break;

					bool jungleNearby = CityTiles.Any(t =>
						t.Type == Terrain.Jungle
						&& !(Game.OlvirImprovements.TryGetValue((t.X, t.Y), out var imp)
						     && imp == OlvirImprovementType.CanopyArray));
					bool hasMedicine = Player.HasAdvance<Medicine>();

					if (jungleNearby && !hasMedicine)
					{
						Size = (byte)(Size - Size / 4);

						message.Add($"Fever sweeps {Name}!");
						message.Add($"Citizens killed!");
						message.Add($"Citizens demand MEDICINE.");
					}

					break;
				}
				case 7:
				case 8:
				case 9:
					// Riot, scandal, corruption

					string[] disasterTypes = { "Scandal", "Riot", "Corruption" };
					string disasterType = disasterTypes[Common.Random.Next(0, disasterTypes.Length)];
					string buildingDemanded = "";

					if (HappyCitizens >= UnhappyCitizens)
						return;
					
					if (!HasBuilding<Temple>())
						buildingDemanded = nameof(Temple);
					else if (!HasBuilding<Courthouse>())
						buildingDemanded = nameof(Courthouse);
					else if (!HasBuilding<MarketPlace>())
						buildingDemanded = nameof(MarketPlace);
					else if (!HasBuilding<Cathedral>())
						buildingDemanded = nameof(Cathedral);
					else 
						buildingDemanded = "lower taxes";

					Food = 0;
					Shields = 0;

					message.Add($"{disasterType} in {Name}");
					message.Add($"Citizens demand {buildingDemanded}");

					if (HasBuilding<Palace>())
						return;

					if (Player.Cities.Length < 4)
						return;

					// Hostile occupiers hold their cities by force, not consent: a
					// barbarian- or story-faction-held city (the Registry, the Thing,
					// Skynet, the Olvir) does not defect to a prosperous neighbour.
					// Otherwise a player could reclaim seized cities for free and
					// defang the invasion arcs — matches the culture-defection guard.
					if (Owner == 0
						|| Player.Civilization is Civilizations.Olvir
							or Civilizations.TheOthers or Civilizations.TheThing or Civilizations.Skynet)
						return;

					City? admired = null;
					int mostAppeal = 0;

					foreach (City city in Game.GetCities())
					{
						if (city == this)
							continue;

						int appeal = ((city.HappyCitizens - city.UnhappyCitizens) * 32) / city.Tile.DistanceTo(this);
						if (appeal > 4 && appeal > mostAppeal)
						{
							admired = city;
							mostAppeal = appeal;
						}
					}

					if (admired is not null && admired.Owner != this.Owner)
					{
						message.Clear();
						message.Add($"Residents of {Name} admire the prosperity of {admired.Name}");
						message.Add($"and defect to their rule!");

						Player previousOwner = Game.GetPlayer(this.Owner);

						System.Action transferCity = () =>
						{
							while (this.Units.Length > 0)
								Game.DisbandUnit(this.Units[0]);
							this.Owner = admired.Owner;
							previousOwner.IsDestroyed();
							if (Human == admired.Owner)
								GameTask.Insert(Tasks.Show.CityManager(this));
						};

						if (Human == admired.Owner || Human == previousOwner)
						{
							humanGetsCity = (Human == admired.Owner);
							Show captureCity = Show.CaptureCity(this);
							captureCity.Done += (s1, a1) => transferCity();
							GameTask.Insert(captureCity);
						}
						else
						{
							transferCity();
						}

					}

					break;				
			}

			if (message.Count > 0 && (Human == Owner || humanGetsCity))
			{
				GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false, message.ToArray()));
			}
		}

		// Hurricanes/typhoons strike coastal and floating cities in the equatorial jungle band
		// and the two mid-latitude arid bands. Cities one tile removed from the ocean take half
		// damage. Global warming both increases strike frequency and shifts severity toward
		// catastrophic. Sea Platform blunts the worst: it eliminates Catastrophic strikes and
		// prevents building damage in Major ones.
		// Returns true when a storm actually landed, so the caller can enforce the global
		// one-storm cooldown (Game.cs) — every early return below is "no storm here".
		public bool HurricaneCheck(int warming)
		{
			// 1. Latitude band: tropical (~equator) or one of the two arid bands.
			double half = (Map.HEIGHT - 1) / 2.0;
			double d = Math.Abs(Y - half) / half;
			bool inBand = d < 0.15 || (d > 0.20 && d < 0.40);
			if (!inBand) return false;

			// 2. Coastal class: full damage when sea is adjacent (or the city itself sits on sea);
			// half damage when sea is one ring further out ("one tile removed"). Freshwater lakes
			// don't generate hurricanes, so they're excluded from every check via IsRealSea.
			bool coastal = IsRealSea(Tile) || Tile.GetBorderTiles().Any(IsRealSea);
			bool nearCoast = !coastal && AnyRealSeaInOuterRing();
			if (!coastal && !nearCoast) return false;

			// 3. Strike probability, intensified by warming (Game.WarmingIndicator,
			// computed once per global tick by the caller — it scans the whole map).
			int strikePct = coastal ? (1 + warming) : (1 + warming / 2);
			if (Common.Random.Next(0, 100) >= strikePct) return false;

			// 4. Severity. 0 = Minor, 1 = Major, 2 = Catastrophic.
			bool seaPlatform = HasBuilding<SeaPlatform>();
			int sevRoll = Common.Random.Next(0, 100);
			int sev;
			if (coastal)
			{
				// Warming widens both Major and Catastrophic, taken from Minor. Catastrophic
				// is gated behind actual warming: at warming 0 catThresh is 100, and sevRoll
				// (0–99) can never reach it, so a clean world sees no super-typhoons — they
				// are the price of a polluted planet, rising to ~28% when fully warmed.
				int catThresh = 100 - warming * 7;  // 100 → 72
				int majThresh = catThresh - 30;     // 70 → 42
				if (sevRoll >= catThresh)      sev = 2;
				else if (sevRoll >= majThresh) sev = 1;
				else                            sev = 0;
			}
			else
			{
				// Near-coast never reaches Catastrophic.
				int majThresh = 80 - warming * 5;   // 80 → 60
				sev = sevRoll >= majThresh ? 1 : 0;
			}
			bool wasCatastrophic = sev == 2;
			if (seaPlatform && sev == 2) sev = 1;  // Sea Platform demotes Catastrophic to Major.

			// 5. Apply damage.
			string title;
			int sizeLoss = 0;
			int buildingsToDestroy = 0;
			if (coastal)
			{
				if (sev == 0)      { title = "Tropical storm strikes";      sizeLoss = 1; }
				else if (sev == 1) { title = "Hurricane strikes";           sizeLoss = Math.Max(1, Size / 3); buildingsToDestroy = seaPlatform ? 0 : 1; }
				else               { title = "Super-typhoon devastates";    sizeLoss = Math.Max(1, Size / 2); buildingsToDestroy = 2; }
			}
			else
			{
				if (sev == 0) { title = "Tropical storm clips"; sizeLoss = Common.Random.Next(0, 2); }
				else          { title = "Hurricane buffets";    sizeLoss = Math.Max(0, Size / 6); buildingsToDestroy = (!seaPlatform && Common.Random.Next(0, 2) == 0) ? 1 : 0; }
			}

			if (sizeLoss > 0) Size = (byte)Math.Max(1, Size - sizeLoss);

			var demolished = new List<string>();
			if (buildingsToDestroy > 0)
			{
				var destroyable = Buildings.Where(b => !(b is Palace)).ToList();
				for (int i = 0; i < buildingsToDestroy && destroyable.Count > 0; i++)
				{
					int idx = Common.Random.Next(0, destroyable.Count);
					var bldg = destroyable[idx];
					destroyable.RemoveAt(idx);
					RemoveBuilding(bldg);
					demolished.Add(bldg.Name);
				}
			}

			// 5b. Coastline erosion: Major+ hurricanes convert one ocean-adjacent
			// worked tile to Swamp. Sea Platform suppresses (same coastal-engineering
			// rationale that saves buildings in Major). Eligible source terrain:
			// Forest, Plains, Grassland, Desert.
			ITile? eroded = null;
			if (sev >= 1 && !seaPlatform)
			{
				var candidates = new List<ITile>();
				for (int dy = -2; dy <= 2; dy++)
				for (int dx = -2; dx <= 2; dx++)
				{
					if (dx == 0 && dy == 0) continue;
					ITile t = Map[X + dx, Y + dy];
					if (t is null || t.City is not null || t.IsOcean) continue;
					if (!t.GetBorderTiles().Any(IsRealSea)) continue;
					if (t.Type != Terrain.Forest && t.Type != Terrain.Plains
						&& t.Type != Terrain.Grassland1 && t.Type != Terrain.Grassland2
						&& t.Type != Terrain.Desert) continue;
					candidates.Add(t);
				}
				if (candidates.Count > 0)
				{
					eroded = candidates[Common.Random.Next(0, candidates.Count)];
					eroded.Road = false;
					eroded.RailRoad = false;
					eroded.Irrigation = false;
					eroded.Mine = false;
					eroded.Fortress = false;
					Map.ChangeTileType(eroded.X, eroded.Y, Terrain.Swamp);
				}
			}

			// 6. Notify the human player only.
			if (Human != Owner) return true;

			GameTask.Enqueue(Show.EventArt("hurricane", $"{title} {Name}!"));

			// Advisor message: damage summary only — no title repeat, no unsolicited advice.
			List<string> msg = new();
			if (sizeLoss > 0)
				msg.Add($"Pop -{sizeLoss}.");
			foreach (var name in demolished)
				msg.Add($"{name} destroyed.");
			if (eroded is not null)
				msg.Add("Coastline eroded.");
			if (seaPlatform && (wasCatastrophic || sev == 1))
				msg.Add("Sea Platform reduced losses.");
			if (msg.Count > 0)
				GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false, msg.ToArray()));

			return true;
		}

		// Real sea: ocean tile that's NOT flagged as a freshwater lake. Lakes are stored as
		// ocean terrain but don't generate hurricanes.
		private static bool IsRealSea(ITile t)
			=> t is not null && t.IsOcean && !Map.Instance.IsFreshwaterAt(t.X, t.Y);

		// Returns true if any tile in the 5×5 area around (X,Y), excluding the inner 3×3, is open sea.
		// Used by HurricaneCheck to find "one tile removed from ocean" cities.
		private bool AnyRealSeaInOuterRing()
		{
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) continue;
				if (IsRealSea(Map[X + dx, Y + dy])) return true;
			}
			return false;
		}

		internal City(byte owner)
		{
			Owner = owner;
			if (!Game.Started) return;
			CurrentProduction = Reflect.GetUnits().Where(u => Player.ProductionAvailable(u)).OrderBy(u => Common.HasAttribute<Default>(u) ? -1 : (int)u.Type).First();
			SetResourceTiles();
		}
	}
}
