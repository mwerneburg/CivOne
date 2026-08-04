// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class BreedingShrine : BaseBuilding, IOlvirBuilding
	{
		private static readonly string[] _page1 =
		{
			"A BREEDING SHRINE is a warm",
			"shallow basin where the Olvir",
			"spawn under lamplight.",
			"",
			"Suspended above the water hang",
			"the clutch-jars: each holds one",
			"generation at a different stage,",
			"lit from within so the elders may",
			"watch them turn.",
			"",
			"Only the Olvir build these.",
		};

		private static readonly string[] _page2 =
		{
			"The fleet arrived with no temples",
			"and no dead to bury. What it had",
			"instead was the spawning rite, the",
			"one ceremony that could not be",
			"left behind: a species that lays",
			"in open water must agree on where.",
			"",
			"Human observers took a long while",
			"to grasp that the shrine is not a",
			"place of worship but a place of",
			"consent. The lamps are not",
			"offerings. They are the register.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public BreedingShrine() : base(5, 1)
		{
			Name = "Breeding Shrine";
			Type = Building.BreedingShrine;
		}
	}
}
