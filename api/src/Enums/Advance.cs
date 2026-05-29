// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Enums
{
	/// <summary>
	/// Tech tree advance IDs. Values 0–79 are original Civ 1 advances; they are persisted
	/// verbatim in .sve save files and must not be reordered. Values 80 and above are
	/// CivOne post-contact advances (unlocked after the SETI signal); they are stored only
	/// in the COS save format and may be extended freely.
	/// </summary>
	public enum Advance
	{
		None = -1,
		Alphabet = 0,
		CodeOfLaws,
		Currency,
		AtomicTheory,
		Democracy,
		Monarchy,
		Astronomy,
		MapMaking,
		Navigation,
		Mathematics,
		Medicine,
		Physics,
		Engineering,
		University,
		Magnetism,
		Electronics,
		Masonry,
		BronzeWorking,
		IronWorking,
		BridgeBuilding,
		Invention,
		Computers,
		Writing,
		SteamEngine,
		Trade,
		CeremonialBurial,
		Mysticism,
		NuclearFission,
		Philosophy,
		Religion,
		Literacy,
		HorsebackRiding,
		Feudalism,
		TheWheel,
		Gunpowder,
		Industrialization,
		Chemistry,
		Combustion,
		Flight,
		AdvancedFlight,
		SpaceFlight,
		MassProduction,
		Pottery,
		Communism,
		TheRepublic,
		Construction,
		Rocketry,
		TheCorporation,
		Metallurgy,
		RailRoad,
		NuclearPower,
		TheoryOfGravity,
		Steel,
		Banking,
		Electricity,
		Refining,
		Explosives,
		SuperConductor,
		Automobile,
		GeneticEngineering,
		Plastics,
		Recycling,
		Chivalry,
		Robotics,
		Conscription,
		LaborUnion,
		FusionPower,
		Xenobiology,
		Gravitics,
		SyntheticEcology,
		MemeticProtocols,
		AquaticColonization,
		TransitConduit,
		BioplexEngineering,
		CanopyCultivation,
		NeuralInterface,
		GravitonEngineering,
		PlanetaryStewardship,
		CollectiveMemory,
		Geoplasticity,
		Bioformatting,
		Hydroengineering
	}
}