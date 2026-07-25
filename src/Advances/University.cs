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
	internal class University : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"UNIVERSITY gathers scholars into",
			"one place and pays them to argue.",
			"",
			"Allows the UNIVERSITY building.",
		};

		private static readonly string[] _page2 =
		{
			"Universities stack with LIBRARIES.",
			"A trade city with both researches",
			"at twice the rate of one with",
			"neither.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public University() : base(1, 0, 2, Advance.Mathematics, Advance.Philosophy)
		{
			Name = "University";
			Type = Advance.University;
		}
	}
}