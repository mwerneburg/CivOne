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
	internal class Trade : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"TRADE sends goods further than the",
			"next valley.",
			"",
			"Allows the CARAVAN, the SEA",
			"CARAVAN and the SURPLUS DEPOT.",
		};

		private static readonly string[] _page2 =
		{
			"Caravans establish TRADE ROUTES",
			"between distant cities, or may be",
			"spent to help build a WONDER —",
			"often the fastest way to win a",
			"race for one.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Trade() : base(1, 0, 0, Advance.Currency, Advance.CodeOfLaws)
		{
			Name = "Trade";
			Type = Advance.Trade;
		}
	}
}