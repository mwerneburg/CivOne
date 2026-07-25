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
	internal class TheCorporation : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE CORPORATION lets enterprise",
			"outlive the people who founded it.",
			"",
			"Allows ADAM SMITH'S TRADING HOUSE.",
		};

		private static readonly string[] _page2 =
		{
			"Adam Smith's pays the upkeep of",
			"your cheap buildings, which is",
			"worth more the wider your empire",
			"spreads.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TheCorporation() : base(7, 2, 2, Advance.Banking, Advance.Industrialization)
		{
			Name = "The Corporation";
			Type = Advance.TheCorporation;
		}
	}
}