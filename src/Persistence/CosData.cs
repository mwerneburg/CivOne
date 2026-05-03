// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;

namespace CivOne.Persistence
{
	public class CosFile
	{
		public string Version { get; set; } = "1.0";
		public CosMeta Meta { get; set; }
		public CosGame Game { get; set; }
		public CosMap Map { get; set; }
		public List<CosPlayer> Players { get; set; }
		public List<CosCity> Cities { get; set; }
		public List<CosUnit> Units { get; set; }
	}

	public class CosMeta
	{
		public string Name { get; set; }
		public int Turn { get; set; }
		public int Difficulty { get; set; }
	}

	public class CosGame
	{
		public uint Turn { get; set; }
		public int HumanPlayer { get; set; }
		public int Difficulty { get; set; }
		public int Competition { get; set; }
		public uint AnthologyTurn { get; set; }
		public string[] CityNames { get; set; }
		public Dictionary<int, int> AdvanceOrigin { get; set; }
		public CosOptions Options { get; set; }
		public int[] SpaceshipLaunch { get; set; }
		public int[] SpaceshipArrival { get; set; }
		public int[] SpaceshipStructural { get; set; }
		public int[] SpaceshipComponent { get; set; }
		public int[] SpaceshipModule { get; set; }
		// base64-encoded byte array, Width*Height bytes; value = player index who first explored (255 = unvisited)
		public string FirstExplorer { get; set; }
		public bool MapRevealedNotified { get; set; }
		public List<CosReplayEntry> ReplayData { get; set; }
	}

	public class CosOptions
	{
		public bool InstantAdvice { get; set; }
		public bool AutoSave { get; set; }
		public bool EndOfTurn { get; set; }
		public bool Animations { get; set; }
		public bool Sound { get; set; }
		public bool EnemyMoves { get; set; }
		public bool CivilopediaText { get; set; }
		public bool Palace { get; set; }
	}

	public class CosReplayEntry
	{
		public string Type { get; set; }
		public int Turn { get; set; }
		// CivilizationDestroyed
		public int DestroyedId { get; set; }
		public int DestroyedById { get; set; }
		// CityBuilt / CityDestroyed
		public int CityId { get; set; }
		public int CityNameId { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int OwnerId { get; set; }
	}

	public class CosPlayer
	{
		public int CivilizationId { get; set; }
		public string LeaderName { get; set; }
		public string CitizenName { get; set; }
		public string CivilizationName { get; set; }
		public int Gold { get; set; }
		public int Science { get; set; }
		public int TaxRate { get; set; }
		public int ScienceRate { get; set; }
		public int StartX { get; set; }
		public int GovernmentId { get; set; }
		public int[] Advances { get; set; }
		public int FutureTechs { get; set; }
		public int[] AtWarWith { get; set; }
		// base64-encoded bitset: bit (y*80+x) set if player has explored that tile
		public string Visibility { get; set; }
	}

	public class CosCity
	{
		public int Id { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int NameId { get; set; }
		public int Owner { get; set; }
		public int Size { get; set; }
		public int Food { get; set; }
		public int Shields { get; set; }
		public string Production { get; set; }
		public string[] ProductionQueue { get; set; }
		public int[] Buildings { get; set; }
		public int[] Wonders { get; set; }
		public int[] ResourceTiles { get; set; }
		public int[] FortifiedUnits { get; set; }  // legacy: kept for loading old saves
		public List<CosTradeRoute> TradeRoutes { get; set; }
		public bool? WasInDisorder  { get; set; }
		public bool? WasWeLoveKing  { get; set; }
	}

	public class CosTradeRoute
	{
		public int PartnerX { get; set; }
		public int PartnerY { get; set; }
		public string Commodity { get; set; }
	}

	public class CosUnit
	{
		public int TypeId { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int Status { get; set; }
		public int MovesLeft { get; set; }
		public int PartMoves { get; set; }
		public int Owner { get; set; }
		public int? GotoX { get; set; }
		public int? GotoY { get; set; }
		public int HomeCityId { get; set; }
		// Settler build progress (null = not building)
		public int? BuildingRoad { get; set; }
		public int? BuildingIrrigation { get; set; }
		public int? BuildingMine { get; set; }
		public int? BuildingFortress { get; set; }
		// Air unit fuel (null = full / not an air unit)
		public int? FuelLeft { get; set; }
	}

	public class CosMap
	{
		public int TerrainSeed { get; set; }
		public int Width { get; set; } = 80;
		public int Height { get; set; } = 50;
		// base64-encoded byte array, Width*Height bytes, terrain type per tile (row-major)
		public string Terrain { get; set; }
		public List<CosImprovement> Improvements { get; set; }
	}

	public class CosImprovement
	{
		public int X { get; set; }
		public int Y { get; set; }
		public bool Road { get; set; }
		public bool Railroad { get; set; }
		public bool Irrigation { get; set; }
		public bool Mine { get; set; }
		public bool Hut { get; set; }
	}
}
