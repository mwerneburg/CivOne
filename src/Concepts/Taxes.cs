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
	internal class Taxes : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"TAXES turn your cities' TRADE into",
			"gold for the treasury.",
			"",
			"On the tax rates screen you split",
			"trade between taxes, LUXURIES, and",
			"SCIENCE.",
			"",
			"Gold pays upkeep for units and",
			"buildings, and can rush-buy.",
		};

		private static readonly string[] _page2 =
		{
			"Run dry and your treasury drains;",
			"empty it and buildings are sold",
			"off to cover the shortfall.",
			"",
			"Marketplaces, Banks, and Stock",
			"Exchanges raise a city's take.",
			"",
			"Your government caps the tax rate.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Taxes()
		{
			Name = "Taxes";
		}
	}
}