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
	internal class Globalism : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"GLOBALISM is the recognition that",
			"no state settles its own affairs",
			"alone — that trade, industry and",
			"war have grown larger than any",
			"one border.",
			"",
			"Allows THE UNITED NATIONS.",
		};

		private static readonly string[] _page2 =
		{
			"The idea arrives late because it",
			"has to: a congress of nations is",
			"meaningless until nations can",
			"reach one another, and ruinous",
			"until they have learned what",
			"industrial war costs.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		// Deliberately the prerequisites Communism used to carry, so the United Nations
		// still becomes available at exactly the point in the tree it always did.
		public Globalism() : base(8, 1, 1, Advance.Philosophy, Advance.Industrialization)
		{
			Name = "Globalism";
			Type = Advance.Globalism;
		}
	}
}
