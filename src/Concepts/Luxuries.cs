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
	internal class Luxuries : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"LUXURIES turn your cities' TRADE",
			"into contentment.",
			"",
			"On the tax rates screen you split",
			"trade between TAXES, luxuries, and",
			"SCIENCE.",
			"",
			"Luxuries calm unhappy citizens and",
			"keep a city out of DISORDER.",
		};

		private static readonly string[] _page2 =
		{
			"A city with many happy citizens",
			"and no unhappy ones celebrates a",
			"WE LOVE THE PRESIDENT day and",
			"prospers faster.",
			"",
			"Temples and Colosseums ease unrest",
			"so you can spend less on luxuries.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Luxuries()
		{
			Name = "Luxuries";
		}
	}
}