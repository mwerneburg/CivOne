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
	internal class Hurricane : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"HURRICANES and TYPHOONS strike",
			"coastal and floating cities, and",
			"inland cities one tile from the",
			"sea, in the equatorial JUNGLE",
			"band and the two mid-latitude",
			"DESERT bands. Freshwater lakes",
			"never spawn hurricanes — only",
			"the open ocean.",
		};

		private static readonly string[] _page2 =
		{
			"Strikes come in three severities:",
			"Tropical Storm, Hurricane, and",
			"Super-Typhoon. Pollution-driven",
			"global warming raises both the",
			"frequency and the chance of",
			"severe events.",
			"",
			"Build a SEA PLATFORM to eliminate",
			"Super-Typhoons and protect city",
			"improvements from storm damage.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Hurricane()
		{
			Name = "Hurricane";
		}
	}
}
