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
	internal class Monarchy : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"MONARCHY",
			"",
			"A crown, and a country that can",
			"finally keep what it grows.",
			"",
			"THE TILE PENALTY IS GONE. Every",
			"tile yields what the land yields,",
			"which makes irrigation and mines",
			"worth building for the first",
			"time. This alone is usually the",
			"largest single gain of the",
			"ancient era.",
			"",
			"Corruption falls well below",
			"despotism, though the far",
			"provinces still steal.",
		};

		private static readonly string[] _page2 =
		{
			"Each city supports THREE units",
			"free; only the fourth and beyond",
			"cost a shield.",
			"",
			"Special resources yield +1 trade,",
			"so the whale and the wine begin",
			"to pay.",
			"",
			"Martial law applies: troops in a",
			"city quiet its citizens, and a",
			"monarch may hold a restless",
			"empire together with soldiers",
			"rather than luxuries.",
			"",
			"A warlike civilization can win",
			"under a crown. A trading one",
			"should look to the Republic.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Monarchy() : base(2, "Monarchy", new Advances.Monarchy())
		{
			CorruptionMultiplier = 16;
			MartialLaw = true;
			FreeUnitSupport = 3;
			SpecialResourceTradeBonus = 1;
		}
	}
}