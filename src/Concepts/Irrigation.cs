// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Concepts
{
	internal class Irrigation : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"SETTLERS build IRRIGATION on land",
			"beside fresh water — a RIVER,",
			"LAKE, or an irrigated tile.",
			"",
			"Irrigation adds +1 FOOD to the",
			"tile, feeding a larger city.",
			"",
			"Not on forest, mountains, or the",
			"open ocean.",
		};

		private static readonly string[] _page2 =
		{
			"Food is a city's lifeblood: more",
			"food means faster growth and more",
			"citizens to work tiles.",
			"",
			"Chain irrigation inland from a",
			"river or lake to green whole",
			"provinces.",
			"",
			"DESPOTISM docks tiles giving over",
			"2 food; change government to lift",
			"the penalty.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Irrigation()
		{
			Name = "Irrigation";
		}
	}
}