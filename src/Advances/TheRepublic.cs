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
	internal class TheRepublic : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE REPUBLIC gives power to",
			"citizens and their representatives.",
			"",
			"Allows the REPUBLIC government:",
			"far greater TRADE, at the cost of",
			"citizens who dislike war.",
		};

		private static readonly string[] _page2 =
		{
			"Under a Republic, units abroad",
			"make citizens unhappy. It is the",
			"government of a civilization that",
			"intends to build rather than",
			"conquer.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TheRepublic() : base(2, 0, 0, Advance.CodeOfLaws, Advance.Literacy)
		{
			Name = "The Republic";
			Type = Advance.TheRepublic;
		}
	}
}