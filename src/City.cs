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

namespace CivOne
{
	public class City : BaseInstance, ITurn
	{
		internal int NameId { get; set; }
		internal byte X;
		internal byte Y;
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
		internal IProduction CurrentProduction { get; private set; }
		// Persisted to <savename>.civ1q alongside the .sve file (see Game.LoadSave.cs).
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
		private int?          _cachedShieldTotal;
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
		private List<Citizen> _cachedCitizens;

		internal void InvalidateCache()
		{
			_cachedFoodRaw        = null;
			_cachedShieldTotal    = null;
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
		// Prevents a diplomat from stealing from the same city twice until it changes hands.
		public bool TechStolen { get; set; } = false;

		internal int ShieldCosts
		{
			get
			{
				IGovernment government = Game.GetPlayer(_owner).Government;
				if (government is Anarchy || government is Despotism)
				{
					int costs = 0;
					for (int i = 0; i < Units.Count(u => (!(u is Diplomat) && !(u is Caravan))); i++)
					{
						if (i < _size) continue;
						costs++;
					}
					return costs;
				}
				return Units.Count(u => (!(u is Diplomat) && !(u is Caravan)));
			}
		}

		internal int ShieldIncome => ShieldTotal - ShieldCosts;
		
		internal int FoodCosts
		{
			get
			{
				int costs = (_size * 2);
				IGovernment government = Game.GetPlayer(_owner).Government;
				if (government is Anarchy || government is Despotism)
				{
					costs += Units.Count(u => (u is Settlers));
				}
				else
				{
					costs += (Units.Count(u => (u is Settlers)) * 2);
				}
				return costs;
			}
		}

		private int FoodRaw => (int)(_cachedFoodRaw ??= ResourceTiles.Sum(t => FoodValue(t)));
		internal int FoodIncome => (HasBuilding<Buildings.MassTransit>() ? (int)(FoodRaw * 1.2) : FoodRaw) - FoodCosts;
		internal int FoodRequired => (int)(Size + 1) * 10;
		internal int FoodTotal => HasBuilding<Buildings.MassTransit>() ? (int)(FoodRaw * 1.2) : FoodRaw;

		internal int FoodValue(ITile tile)
		{
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
			return output;
		}

		internal int ShieldValue(ITile tile)
		{
			int output = tile.Shield;
			switch (tile.Type)
			{
				case Terrain.Hills:
					if (!Player.AnarchyDespotism && tile.Mine) output += 1;
					break;
			}
			if (tile.RailRoad) output = (int)Math.Floor((double)output * 1.5);
			return output;
		}

		internal int ShieldTotal
		{
			get
			{
				if (_cachedShieldTotal.HasValue) return _cachedShieldTotal.Value;
				int shields = ResourceTiles.Sum(t => ShieldValue(t));
				if (HasBuilding<Buildings.MassTransit>()) shields = (int)(shields * 1.2);
				if (_buildings.Any(b => (b is Factory))) shields += (short)Math.Floor((double)shields * (_buildings.Any(b => (b is NuclearPlant || b is PowerPlant || b is HydroPlant)) || HooverDamActive ? 1.0 : 0.5));
				if (_buildings.Any(b => (b is MfgPlant))) shields += (short)Math.Floor((double)shields * 1.0);
				return (_cachedShieldTotal = shields).Value;
			}
		}

		internal int TradeValue(ITile tile)
		{
			int output = tile.Trade;

			if (tile.RailRoad) output = (int)Math.Floor((double)output * 1.5);
			switch (tile.Type)
			{
				case Terrain.Desert:
				case Terrain.Grassland1:
				case Terrain.Grassland2:
				case Terrain.Plains:
					if (!tile.Road) break;
					if (Player.RepublicDemocratic) output += 1;
					break;
				case Terrain.Ocean:
					if (Player.HasAdvance<Trade>()) output += 1;
					if (Player.RepublicDemocratic) output += 1;
					break;
				case Terrain.River:
					output += 1; // rivers are natural trade corridors regardless of government
					if (Player.RepublicDemocratic) output += 1;
					break;
				case Terrain.Jungle:
					if (!tile.Special) break;
					if (Player.MonarchyCommunist) output += 1;
					if (Player.RepublicDemocratic) output += 2;
					break;
				case Terrain.Mountains:
					if (!tile.Special) break;
					if (Player.MonarchyCommunist) output += 1;
					if (Player.RepublicDemocratic) output += 2;
					break;
			}
			if (output > 0 && HasWonder<Colossus>() && !Game.WonderObsolete<Colossus>()) output += 1;
			return output;
		}

		private int RawTrade => (int)(_cachedRawTrade ??= ResourceTiles.Sum(t => TradeValue(t)));

		private int BaseTrade => (int)(_cachedBaseTrade ??= RawTrade - Corruption);

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
		internal short TradeTaxes => (short)(_cachedTradeTaxes ??= (short)Math.Round(((double)TradeTotal / 10) * Player.TaxesRate, MidpointRounding.AwayFromZero));
		internal short TradeLuxuries => (short)(_cachedTradeLuxuries ??= (short)Math.Round(((double)(TradeTotal - TradeTaxes) / (10 - Player.TaxesRate)) * Player.LuxuriesRate, MidpointRounding.AwayFromZero));
		internal short TradeScience => (short)(_cachedTradeScience ??= (short)(TradeTotal - TradeLuxuries - TradeTaxes));

		internal int Corruption
		{
			get
			{
				if (_cachedCorruption.HasValue) return _cachedCorruption.Value;

				IGovernment government = Game.GetPlayer(_owner).Government;
				if (government.CorruptionMultiplier == 0) return (_cachedCorruption = 0).Value;

				int distance;
				switch (government)
				{
					case Governments.Communism _:
						distance = 10;
						break;
					default:
						if (HasBuilding<Palace>()) return (_cachedCorruption = 0).Value;
						if (Game.GetPlayer(Owner).Cities.Any(x => x.HasBuilding<Palace>()))
						{
							City capital = Game.GetPlayer(Owner).Cities.First(x => x.HasBuilding<Palace>());
							distance = Common.DistanceToTile(X, Y, capital.X, capital.Y);
						}
						else
						{
							distance = 32;
						}
						break;
				}

				int totalTrade = RawTrade;
				int corruption = (int)Math.Round((float)(totalTrade * distance * 3) / (10 * government.CorruptionMultiplier));

				if (HasBuilding<Courthouse>() || (HasBuilding<Palace>() && government is Governments.Communism)) corruption /= 2;

				return (_cachedCorruption = corruption).Value;
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
				bool newtonActive = !Game.WonderObsolete<IsaacNewtonsCollege>() && Player.HasWonder<IsaacNewtonsCollege>() && !Player.HasWonder<SETIProgram>();
				double libUniBonus = newtonActive ? (2.0 / 3.0) : 0.5;
				if (HasBuilding<Library>()) science += (short)Math.Floor(science * libUniBonus);
				if (HasBuilding<UniversityBuilding>()) science += (short)Math.Floor(science * libUniBonus);
				if (HasBuilding<ObservatoryBuilding>()) science += (short)Math.Floor(science * libUniBonus);
				if (!Game.WonderObsolete<CopernicusObservatory>() && HasWonder<CopernicusObservatory>()) science += science;
				if (Player.HasWonder<SETIProgram>()) science += (short)Math.Floor((double)science * 0.5);
				science += (short)(_specialists.Count(c => c == Citizen.Scientist) * 2);
				return (_cachedScience = science).Value;
			}
		}

		internal short TotalMaintenance => (short)_buildings.Sum(b => b.Maintenance);

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
			return (Game.GetCities().Where(c => c != this).Any(c => c.ResourceTiles.Any(t => t.X == tile.X && t.Y == tile.Y)) || tile.Units.Any(u => u.Owner != Owner));
		}

		private void UpdateSpecialists()
		{
			while (_specialists.Count < (_size - ResourceTiles.Count())) _specialists.Add(Citizen.Entertainer);
			while (_specialists.Count > 0 && _specialists.Count > (_size - ResourceTiles.Count() - 1)) _specialists.RemoveAt(_specialists.Count - 1);
			InvalidateCache();
		}

		private void SetResourceTiles()
		{
			if (!Game.Started) return;
			while (_resourceTiles.Count > Size)
				_resourceTiles.RemoveAt(_resourceTiles.Count - 1);
			if (_resourceTiles.Count == Size) return;
			if (_resourceTiles.Count < Size)
			{
				IEnumerable<ITile> tiles = CityTiles.Where(t => !OccupiedTile(t) && !ResourceTiles.Contains(t)).OrderByDescending(t => FoodValue(t)).ThenByDescending(t => ShieldValue(t)).ThenByDescending(t => TradeValue(t));
				if (tiles.Count() > 0)
					_resourceTiles.Add(tiles.First());
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
						if (y == -1) output[1] |= (byte)(0x01 << 6);
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

			// Decode specialist types: 0=Entertainer, 1=Taxman, 2=Scientist (2 bits each, up to 12)
			_specialists.Clear();
			int specialistCount = Math.Max(0, Size - _resourceTiles.Count);
			for (int i = 0; i < specialistCount && i < 12; i++)
			{
				int type = (gameData[3 + i / 4] >> (i % 4 * 2)) & 0x3;
				_specialists.Add(type == 1 ? Citizen.Taxman : type == 2 ? Citizen.Scientist : Citizen.Entertainer);
			}
		}

		private void ResetResourceTiles()
		{
			_resourceTiles.Clear();
			for (int i = 0; i < Size; i++)
				SetResourceTiles();
			InvalidateCache();
		}

		public void RelocateResourceTile(ITile tile)
		{
			if (tile.X == X && tile.Y == Y) return;
			SetResourceTile(tile);
			SetResourceTiles();
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
					if (building is HydroPlant && !CityTiles.Any(t => t.Type == Terrain.River)) continue;
					yield return building;
				}
				foreach (IWonder wonder in Reflect.GetWonders().Where(b => Player.ProductionAvailable(b)))
				{
					if (!coastal && (wonder is Lighthouse || wonder is MagellansExpedition || wonder is DarwinsVoyage || wonder is Colossus || wonder is ZhengHeVoyage)) continue;
					if (wonder is ZhengHeVoyage && !Map.AllTiles().Any(t => t.ContinentId != Tile.ContinentId && t.ContinentId > 0 && t.City is not null && t.City.Owner != 0)) continue;
					if (wonder is Wonders.SETIProgram && !HasBuilding<UniversityBuilding>()) continue;
					if (wonder is Wonders.SETIProgram && Game.GetPlayer(Owner).Cities.Count(c => c.HasBuilding<ObservatoryBuilding>()) < 5) continue;
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
			return true;
		}

		// Find the next wonder that is not yet globally built and is researchable
		// by this city's player, skipping the optionally supplied beaten wonder.
		private IWonder NextAvailableWonder(IWonder beaten = null)
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

		internal short BuyPrice
		{
			get
			{
				if (Shields > 0)
				{
					// Thanks to Tristan_C (http://forums.civfanatics.com/threads/buy-unit-building-wonder-price.576026/#post-14490920)
					if (CurrentProduction is IUnit)
					{
						double x = (double)((CurrentProduction.Price * 10) - Shields) / 10;
						double price = 5 * (x * x) + (20 * x);
						return (short)(Math.Floor(price));
					}
					return (short)(((CurrentProduction.Price * 10) - Shields) * (CurrentProduction is IWonder ? 4 : 2));
				}
				return CurrentProduction.BuyPrice;
			}
		}

		public bool Buy()
		{
			int buyPrice = BuyPrice;
			if (buyPrice <= 0) return false;
			if (IsInDisorder && CurrentProduction is IBuilding) return false;
			if (Game.CurrentPlayer.Gold < buyPrice) return false;

			Game.CurrentPlayer.Gold -= (short)buyPrice;
			Shields = (int)CurrentProduction.Price * 10;
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
			// Sync specialist list length with current city size and worked tiles.
			int resourceCount = ResourceTiles.Count();
			while (_specialists.Count < Size - (resourceCount - 1)) _specialists.Add(Citizen.Entertainer);
			while (_specialists.Count > Size - (resourceCount - 1)) _specialists.Remove(_specialists.Last());

			int happyCount = (int)Math.Floor((double)Luxuries / 2);
			if (Player.HasWonder<HangingGardens>() && !Game.WonderObsolete<HangingGardens>()) happyCount++;
			if (Player.HasWonder<CureForCancer>()) happyCount++;

			int unhappyCount = Size - (6 - Game.Difficulty) - happyCount;
			if (Player.RepublicDemocratic)
			{
				int penalty = Player.Government is Governments.Democracy ? 2 : 1;
				if (Player.HasWonder<WomensSuffrage>()) penalty = Math.Max(0, penalty - 1);
				int militaryAway = Units.Count(u => !(u is Diplomat) && !(u is Caravan) && !(u is Settlers) && (u.X != X || u.Y != Y));
				unhappyCount += militaryAway * penalty;
			}
			if (HasWonder<ShakespearesTheatre>() && !Game.WonderObsolete<ShakespearesTheatre>())
			{
				unhappyCount = 0;
			}
			else
			{
				if (HasBuilding<Temple>())
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
				bool chapelOnContinent = !Game.WonderObsolete<MichelangelosChapel>() &&
					Tile is not null &&
					Map.ContentCities(Tile.ContinentId).Any(x => x.Size > 0 && x.Owner == Owner && x.HasWonder<MichelangelosChapel>());
				if (HasBuilding<Cathedral>())
					unhappyCount -= chapelOnContinent ? 6 : 4;
				else if (chapelOnContinent)
					unhappyCount -= 4;
			}

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
					if ((xx == 0 || xx == 4) && (yy == 0 || yy == 4)) tiles[xx, yy] = null;
					if (!player.Visible(tile)) tiles[xx, yy] = null;
				}
				return tiles;
			}
		}

		private readonly List<IUnit> _homeUnits = new();
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
			Game.CurrentPlayer.Gold += building.SellPrice;
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
			foreach (ITile tile in ResourceTiles.Where(t => InvalidTile(t)))
			{
				RelocateResourceTile(tile);
			}
		}

		// Industrial + population pollution, reduced by clean-power buildings.
		public int SmokeStacks
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

				int stacks = industrial + (Size * popMult / 100);
				return Math.Max(0, stacks - 20); // first 20 units are tolerated
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
							if (Player.Government is Governments.Republic || Player.Government is Governments.Democracy)
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
					if (Player.Government is Governments.Democracy || Player.Government is Republic)
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
								var caravan = Game.Instance.CreateUnit(UnitType.Caravan, X, Y, Owner);
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

				if (Size == 7 && !_buildings.Any(b => b.Id == (int)Building.Aqueduct))
				{
					GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false, $"{Name} requires an AQUEDUCT", "for further growth."));
				}
				else if (Size == 12 && !_buildings.Any(b => b.Id == (int)Building.SewerSystem))
				{
					GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false, $"{Name} requires a SEWER SYSTEM", "for further growth."));
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
				if (!inDisorder)
				{
					int income = shieldIncome;
					// Higher difficulties give AI cities a production bonus (classic Civ 1 "cheat").
					// +25 % per difficulty step — double speed at Emperor.
					if (Player != Human && Game.Difficulty > 0)
						income += income * Game.Difficulty / 4;
					Shields += income;
				}
			}

			if (CurrentProduction is not null && Shields >= (int)CurrentProduction.Price * 10)
			{
				if (CurrentProduction is Settlers && Size == 1 && Game.Difficulty == 0)
				{
					// On Chieftain level, it's not possible to create a Settlers in a city of size 1
				}
				else if (CurrentProduction is IUnit)
				{
					Shields = 0;
					IUnit unit = Game.Instance.CreateUnit((CurrentProduction as IUnit).Type, X, Y, Owner);
					unit.Veteran = (_buildings.Any(b => (b is Barracks)));
					if (CurrentProduction is Settlers)
					{
						if (Size == 1 && Player.Cities.Length == 1) Size++;
						if (Size > 1) unit.SetHome();
						Size--;
					}
					else
					{
						unit.SetHome();
					}
					if (Human == Owner && (unit is Settlers || unit is Diplomat || unit is Caravan))
					{
						GameTask.Enqueue(new ImprovementBuilt(this, unit));
					}
					if (!(CurrentProduction is Settlers || CurrentProduction is Diplomat || CurrentProduction is Caravan))
					{
						string uname = (CurrentProduction as ICivilopedia)?.Name;
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
					if (CurrentProduction is Buildings.SSStructural)      Game.SpaceshipStructural[playerIndex]++;
					else if (CurrentProduction is Buildings.SSComponent)  Game.SpaceshipComponent[playerIndex]++;
					else if (CurrentProduction is Buildings.SSModule)     Game.SpaceshipModule[playerIndex]++;
					Message message = Message.Newspaper(this, $"{this.Name} builds", $"{(CurrentProduction as ICivilopedia).Name}.");
					message.Done += (s, a) => GameTask.Insert(Show.CityManager(this));
					GameTask.Enqueue(message);
				}
				else if (CurrentProduction is IBuilding && !_buildings.Any(b => b.Id == (CurrentProduction as IBuilding).Id))
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
						_buildings.Add(CurrentProduction as IBuilding);

						Message message = Message.Newspaper(this, $"{this.Name} builds", $"{(CurrentProduction as ICivilopedia).Name}.");
						message.Done += (s, a) => {
							GameTask advisorMessage = Message.Advisor(Advisor.Foreign, true, $"{Player.TribeName} capital", $"moved to {Name}.");
							advisorMessage.Done += (s1, a1) => GameTask.Insert(Show.CityManager(this));
							GameTask.Enqueue(advisorMessage);
						};
						GameTask.Enqueue(message);
					}
					else
					{
						_buildings.Add(CurrentProduction as IBuilding);
						GameTask.Enqueue(new ImprovementBuilt(this, (CurrentProduction as IBuilding)));
					}
					string bname = (CurrentProduction as ICivilopedia)?.Name;
					if (bname is not null && !Game.Instance.GetReplayData<ReplayData.BuildingBuilt>().Any(b => b.BuildingName == bname))
						Game.Instance.AddReplayEvent(new ReplayData.BuildingBuilt(Game.GameTurn, Owner, bname));
				}
				if (CurrentProduction is IWonder)
				{
					IWonder wonder = CurrentProduction as IWonder;
					if (!Game.WonderBuilt(wonder))
					{
						Shields = 0;
						AddWonder(wonder);
						Game.Instance.AddReplayEvent(new ReplayData.WonderBuilt(Game.GameTurn, Owner, (wonder as ICivilopedia).Name, X, Y));
						var impTask = new ImprovementBuilt(this, wonder);
						if (wonder is Wonders.SouthPoleExpedition && Player == Human)
						{
							Game.SpaceshipComponent[Game.PlayerNumber(Player)] += 2;
							string gameYear = Game.GameYear;
							Game.Instance.RecordTransmission("SouthPoleExpedition", gameYear);
							impTask.Done += (s, a) => GameTask.Enqueue(Show.Screen(new SouthPoleExpeditionLog(gameYear)));
						}
						if (wonder is Wonders.SETIProgram)
							Game.SETISignalTurn = (uint)(Game.GameTurn + 5);
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
							int quality = Game.CalcProbeQuality(Player);
							int tier    = Game.CalcProbeOutcomeTier(quality, Game.Instance.VisitorType);
							Game.Instance.ProbeOutcomeTier = tier;

							int techCount = tier >= 4 ? 2 : tier >= 3 ? 1 : 0;
							string[] techNames = null;
							if (techCount > 0)
							{
								var grants = Human.AvailableResearch
									.Where(a => !(a is FutureTech))
									.Take(techCount)
									.ToArray();
								techNames = grants.Select(a => (a as ICivilopedia)?.Name).ToArray();
								foreach (var adv in grants)
									Human.AddAdvance(adv);
							}

							string gameDate = Game.GameYear;
							Game.Instance.RecordTransmission("ProbeResult", gameDate);
							impTask.Done += (s, a) => GameTask.Enqueue(Show.Screen(
								new ProbeResultTransmission(gameDate, Game.Instance.VisitorType, tier, techNames)));
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
			Player.Gold -= TotalMaintenance;
			Player.Science += Science;
			BuildingSold = false;
			GameTask.Enqueue(new ProcessScience(Player));

			if (Shields == 0 && !DequeueProduction() && Player != Human)
				Player.AI.CityProduction(this);
		}

		public void Disaster()
		{
			List<string> message = new();
			bool humanGetsCity = false;

			if (Player.Cities.Length == 1)
				return;

			if (Size < 5)
				return;

			switch (Common.Random.Next(0, 9))
			{
				case 0: 
				{
					// Earthquake
					bool hillsNearby = CityTiles.Any(t => t.Type == Terrain.Hills);
					IList<IBuilding> buildingsOtherThanPalace = Buildings.Where(b => !(b is Palace)).ToList();
					if (!hillsNearby || !buildingsOtherThanPalace.Any())
						return;
					
					IBuilding buildingToDestroy = buildingsOtherThanPalace[Common.Random.Next(0, buildingsOtherThanPalace.Count - 1)];
					RemoveBuilding(buildingToDestroy);

					message.Add($"Earthquake in {Name}!");
					message.Add($"{buildingToDestroy.Name} destroyed!");

					break;
				}
				case 1:
				{
					// Plague
					bool hasMedicine = Player.HasAdvance<Medicine>();
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
						IBuilding buildingToDestroy = buildingsOtherThanPalace[Common.Random.Next(0, buildingsOtherThanPalace.Count - 1)];
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
				case 7:
				case 8:
				case 9:
					// Riot, scandal, corruption

					string[] disasterTypes = { "Scandal", "Riot", "Corruption" };
					string disasterType = disasterTypes[Common.Random.Next(0, disasterTypes.Length - 1)];
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
					
					City admired = null;
					int mostAppeal = 0;

					foreach (City city in Game.GetCities())
					{
						if (city == this)
							break;

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
						message.Add($"{admired.Name} capture {Name}");

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

			if (message.Count > 0 && (Player == Owner || humanGetsCity))
			{
				GameTask.Enqueue(Message.Advisor(Advisor.Domestic, false, message.ToArray()));
			}
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
