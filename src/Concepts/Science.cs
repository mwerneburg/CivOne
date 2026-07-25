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
	internal class Science : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"SCIENCE turns your cities' TRADE",
			"into research toward new advances.",
			"",
			"On the tax rates screen you split",
			"trade between TAXES, LUXURIES, and",
			"science.",
			"",
			"The more you fund science, the",
			"sooner you make discoveries.",
		};

		private static readonly string[] _page2 =
		{
			"Libraries, Universities, and the",
			"right wonders multiply a city's",
			"research.",
			"",
			"An economy that leans into science",
			"keeps you ahead in the arms race",
			"and the space race.",
			"",
			"Your government caps the science",
			"rate.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Science()
		{
			Name = "Science";
		}
	}
}