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
		public CosMeta Meta { get; set; } = null!;
		public CosGame Game { get; set; } = null!;
		public CosMap Map { get; set; } = null!;
		public List<CosPlayer> Players { get; set; } = null!;
		public List<CosCity> Cities { get; set; } = null!;
		public List<CosUnit> Units { get; set; } = null!;
	}

	public class CosMeta
	{
		public string Name { get; set; } = null!;
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
		public string[] CityNames { get; set; } = null!;
		public Dictionary<int, int> AdvanceOrigin { get; set; } = null!;
		public CosOptions Options { get; set; } = null!;
		public int[] SpaceshipLaunch { get; set; } = null!;
		public int[] SpaceshipArrival { get; set; } = null!;
		public int[] SpaceshipStructural { get; set; } = null!;
		public int[] SpaceshipComponent { get; set; } = null!;
		public int[] SpaceshipModule { get; set; } = null!;
		// base64-encoded byte array, Width*Height bytes; value = player index who first explored (255 = unvisited)
		public string FirstExplorer { get; set; } = null!;
		public bool MapRevealedNotified { get; set; }
		public uint SETISignalTurn { get; set; }
		public bool SETISignalReceived { get; set; }
		public bool VisitorsArrived { get; set; }
		public int VisitorArchetype { get; set; }
		public uint TauCetiEscalationTurn { get; set; }
		public bool ProbeDispatched { get; set; }
		public uint ProbeDispatchTurn { get; set; }
		public int ProbeInterimPhase { get; set; }
		public int[] ProbeGrantedAdvanceIds { get; set; } = null!;
		public int ProbeOutcomeTier { get; set; }
		public uint OlvirArrivalTurn { get; set; }
		public uint OlvirProximityAlarmTurn { get; set; }
		public uint OlvirBloomEndTurn { get; set; }
		// Olvir improvements: list of [x, y, type] triples
		public List<int[]> OlvirImprovements { get; set; } = null!;
		// Thing outbreak clocks: list of [x, y, deadlineTurn] triples
		public List<int[]> ThingOutbreaks { get; set; } = null!;
		// Economic-dominance streak (consecutive qualifying turns) and the player
		// numbers of wars the human started (defensive wars don't break the streak).
		public uint EconStreak { get; set; }
		public int[] HumanStartedWars { get; set; } = null!;
		// Gozira: 0 = egg sleeps, 1 = rampaging, 2 = slain.
		public int GoziraState { get; set; }
		// Leviathan: 0 = deep is quiet, 1 = hunting, 2 = slain.
		public int LeviathanState { get; set; }
		// Stone door: 0 = shut, 1 = open (guardian + tithe), 2 = closed for good.
		public int DoorState { get; set; }
		public int DoorX { get; set; }
		public int DoorY { get; set; }
		// Oracle: the Other Voice speaks until Religion silences it.
		public bool OracleVoiceActive { get; set; }
		// Grey-infested city tiles: list of [x, y] pairs (The Portal's curse).
		public List<int[]> GreyCities { get; set; } = null!;
		// King in Yellow afflicted city tiles: [x, y] pairs.
		public List<int[]> YellowCities { get; set; } = null!;
		// Great Wall curse window and target continent (0 end turn = inactive).
		public uint WallCurseEndTurn { get; set; }
		public int WallCurseContinent { get; set; }
		// Newton's anomaly: afflicted city tile and end turn (0 = inactive).
		public int AnomalyX { get; set; }
		public int AnomalyY { get; set; }
		public uint AnomalyEndTurn { get; set; }
		// Pyramids visitations: beacon city tile; inactive unless the flag is set.
		public bool VisitationsActive { get; set; }
		public int VisitationsX { get; set; }
		public int VisitationsY { get; set; }
		// Grey goo tiles: [x, y, turnConsumed] triples, plus the doubling clock
		// and whether the Nanobot Factory rolled cursed (no upgrades ever).
		public List<int[]> GooTiles { get; set; } = null!;
		public uint GooNextDoubleTurn { get; set; }
		public bool NanobotCursed { get; set; }
		// Dome assignments: list of [ownerByte, wonderId] pairs
		public List<int[]> DomeAssignments { get; set; } = null!;
		public bool DomeVictoryFired { get; set; }
		// Each inner list: [gameTurn, score0, score1, ..., scoreN]
		public List<List<int>> ScoreHistory { get; set; } = null!;
		public List<CosReplayEntry> ReplayData { get; set; } = null!;
		public List<CosTransmission> Transmissions { get; set; } = null!;
	}

	public class CosTransmission
	{
		public string Type { get; set; } = null!;   // "SETISignal" | "SouthPoleIntel" | "SouthPoleExpedition"
		public string Year { get; set; } = null!;   // game year string when received
	}

	public class CosOptions
	{
		public bool InstantAdvice { get; set; }
		public bool AutoSave { get; set; }
		public bool EndOfTurn { get; set; }
		public bool Animations { get; set; }
		public bool EnemyMoves { get; set; }
		public bool CivilopediaText { get; set; }
		public bool? Circuses { get; set; }
		public bool? Barricades { get; set; }
	}

	public class CosReplayEntry
	{
		public string Type { get; set; } = null!;
		public int Turn { get; set; }
		// CivilizationDestroyed
		public int DestroyedId { get; set; }
		public int DestroyedById { get; set; }
		// CityBuilt / CityCaptured / CityDestroyed
		public int CityId { get; set; }
		public int CityNameId { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int OwnerId { get; set; }
		// WonderBuilt
		public string WonderName { get; set; } = null!;
		// TechDiscovered
		public string TechName { get; set; } = null!;
		// UnitBuilt / BuildingBuilt
		public string UnitName { get; set; } = null!;
		public string BuildingName { get; set; } = null!;
	}

	public class CosPlayer
	{
		public int CivilizationId { get; set; }
		public string LeaderName { get; set; } = null!;
		public string CitizenName { get; set; } = null!;
		public string CivilizationName { get; set; } = null!;
		public int Gold { get; set; }
		public int Science { get; set; }
		public int TaxRate { get; set; }
		public int ScienceRate { get; set; }
		public int StartX { get; set; }
		public int GovernmentId { get; set; }
		public int[] Advances { get; set; } = null!;
		public int? CurrentResearch { get; set; }
		public int FutureTechs { get; set; }
		public int? MilestoneScore { get; set; }
		// Accumulated culture points. Absent on saves predating the culture ledger.
		public int? Culture { get; set; }
		public int[] AtWarWith { get; set; } = null!;
		public int[] Embassies { get; set; } = null!;
		public int CityNamesSkipped { get; set; }
		public int? Anarchy { get; set; }
		// base64-encoded bitset: bit (y*80+x) set if player has explored that tile
		public string Visibility { get; set; } = null!;
		// Map zoom level in basis points (1000 = 100%). Default 1000 when the
		// key is absent; clamped to MapZoomSettings.Min/Max on read. Persisted so
		// reloading a save keeps the player's chosen zoom.
		public int? MapZoomBasisPoints { get; set; }
		// Tribute pacts where this player is the *payer*. Each entry records the
		// protector's player index and the annual gold amount. The inverse map
		// (this player's _tributeFrom) is reconstructed on load. Absent or empty
		// on saves predating the tribute system.
		public List<CosTribute> TributeTo { get; set; } = null!;
		// Peace-treaty countdowns: { otherPlayerIdx, turnsRemaining } pairs.
		// "I will not declare war on otherPlayerIdx for turnsRemaining turns."
		// Decremented at end of each turn; entries reaching 0 are dropped.
		// Absent or empty on saves predating peace persistence.
		public List<CosCountdown> PeaceTreaty { get; set; } = null!;
		// Attitude-bonus countdowns: same shape as PeaceTreaty. Boosts AI
		// acceptance for diplomacy with the named player for turnsRemaining
		// turns. Decremented per turn.
		public List<CosCountdown> AttitudeBonus { get; set; } = null!;
		// Mutual-defense-pact countdowns: same shape. Kept symmetric between the
		// two signatories. Absent or empty on saves predating defense pacts.
		public List<CosCountdown> DefensePact { get; set; } = null!;
	}

	public class CosTribute
	{
		public int Protector { get; set; }
		public int Annual    { get; set; }
	}

	public class CosCountdown
	{
		public int Player { get; set; }
		public int Turns  { get; set; }
	}

	public class CosCity
	{
		public int Id { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int NameId { get; set; }
		public int Owner { get; set; }
		public int? OriginalOwner { get; set; }
		public int Size { get; set; }
		public int Food { get; set; }
		public int Shields { get; set; }
		public string Production { get; set; } = null!;
		public string[] ProductionQueue { get; set; } = null!;
		public int[] Buildings { get; set; } = null!;
		public int[] Wonders { get; set; } = null!;
		public int[] ResourceTiles { get; set; } = null!;
		public int[] FortifiedUnits { get; set; } = null!;  // legacy: kept for loading old saves
		public List<CosTradeRoute> TradeRoutes { get; set; } = null!;
		public bool? WasInDisorder  { get; set; }  // legacy: kept for loading old saves
		public int?  DisorderTurns  { get; set; }
		public bool? WasWeLoveKing  { get; set; }
		public bool? TechStolen     { get; set; }
	}

	public class CosTradeRoute
	{
		public int PartnerX { get; set; }
		public int PartnerY { get; set; }
		public string Commodity { get; set; } = null!;
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
		public int? BuildingCanopyArray { get; set; }
		public int? BuildingAquafarm { get; set; }
		// Hydro Engineer sea-tube build progress (null = not building)
		public int? BuildingTube { get; set; }
		// Hydro Engineer ocean→plains reclamation progress (null = not building)
		public int? BuildingReclaim { get; set; }
		// Tier-4 terraform build progress (Settlers; null = not building)
		public int? BuildingLowerTerrain { get; set; }
		public int? BuildingRaiseTerrain { get; set; }
		public int? BuildingPlantForest { get; set; }
		public int? BuildingPlantJungle { get; set; }
		public int? BuildingThawTundra { get; set; }
		public int? BuildingAddRiver { get; set; }
		// Settler "build road to" destination (null = no road-to order)
		public int? RoadToX { get; set; }
		public int? RoadToY { get; set; }
		// Air unit fuel (null = full / not an air unit)
		public int? FuelLeft { get; set; }
	}

	public class CosMap
	{
		public int TerrainSeed { get; set; }
		public int Width { get; set; } = 80;
		public int Height { get; set; } = 50;
		// base64-encoded byte array, Width*Height bytes, terrain type per tile (row-major)
		public string Terrain { get; set; } = null!;
		public List<CosImprovement> Improvements { get; set; } = null!;
	}

	public class CosImprovement
	{
		public int X { get; set; }
		public int Y { get; set; }
		public bool Road { get; set; }
		public bool Railroad { get; set; }
		public bool TransportTube { get; set; }
		public bool Irrigation { get; set; }
		public bool Mine { get; set; }
		public bool Hut { get; set; }
	}
}