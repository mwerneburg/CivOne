// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Governments
{
	internal class Anarchy : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"ANARCHY",
			"",
			"No government at all. The state",
			"between one constitution and the",
			"next, and the state a Republic or",
			"Democracy falls into when its",
			"cities riot too long.",
			"",
			"It collects NO taxes and funds NO",
			"research. Treasury and laboratory",
			"both stand idle until a new",
			"government is chosen.",
			"",
			"Revolution is the only exit, and",
			"it is not instant.",
		};

		private static readonly string[] _page2 =
		{
			"Corruption is ruinous — worse",
			"than under any despot.",
			"",
			"The despot's tile penalty applies:",
			"any tile producing three or more",
			"of anything loses one.",
			"",
			"No unit is supported free; every",
			"one costs its home city a shield.",
			"A settler eats one food a turn.",
			"",
			"Martial law still works. Troops",
			"in a city quiet its citizens,",
			"which in anarchy is the only",
			"instrument of government left.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Anarchy() : base(0, "Anarchy")
		{
			CorruptionMultiplier = 12;
			TilePenalty = true;
			FreeUnitSupport = -1;
			SettlerFoodCost = 1;
			MartialLaw = true;
		}
	}
}