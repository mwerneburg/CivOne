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
	internal class Currency : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"CURRENCY replaces barter with",
			"coin, and trade no longer needs",
			"two parties who each want what",
			"the other holds.",
			"",
			"Allows the MARKETPLACE.",
		};

		private static readonly string[] _page2 =
		{
			"The marketplace is the cheapest",
			"way to turn trade into gold, and",
			"it raises luxuries too, easing",
			"unhappiness as it fills the",
			"treasury.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Currency() : base(5, 0, 0, Advance.BronzeWorking)
		{
			Name = "Currency";
			Type = Advance.Currency;
		}
	}
}