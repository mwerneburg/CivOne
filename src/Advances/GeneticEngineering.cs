// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class GeneticEngineering : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"GENETIC ENGINEERING reads and",
			"rewrites the instructions of life",
			"itself.",
			"",
			"Allows THE CURE FOR CANCER and THE",
			"HUMAN GENOME PROJECT.",
		};

		private static readonly string[] _page2 =
		{
			"The Cure for Cancer makes every",
			"city in your empire happier; the",
			"Genome Project hastens all your",
			"research.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public GeneticEngineering() : base(1, 1, 2, Advance.Medicine, Advance.TheCorporation)
		{
			Name = "Genetic Engineering";
			Type = Advance.GeneticEngineering;
		}
	}
}