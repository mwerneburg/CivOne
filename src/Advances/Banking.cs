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
	internal class Banking : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"BANKING lends, holds and multiplies",
			"money that would otherwise sit",
			"still.",
			"",
			"Allows the BANK and the",
			"INFRASTRUCTURE BOND.",
		};

		private static readonly string[] _page2 =
		{
			"Banking is also a condition of the",
			"PAX MERCATORIA victory: a",
			"civilization cannot dominate world",
			"trade without it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Banking() : base(1, 2, 2, Advance.Trade, Advance.TheRepublic)
		{
			Name = "Banking";
			Type = Advance.Banking;
		}
	}
}