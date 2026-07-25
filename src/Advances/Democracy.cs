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
	internal class Democracy : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"DEMOCRACY places the government in",
			"the hands of all citizens.",
			"",
			"Allows the DEMOCRACY government:",
			"the greatest trade and the least",
			"corruption of any rule.",
		};

		private static readonly string[] _page2 =
		{
			"Its citizens will not tolerate a",
			"long war, and sustained civil",
			"disorder collapses the government",
			"into ANARCHY.",
			"",
			"Even so, graft creeps into the",
			"largest cities. Build COURTHOUSES.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Democracy() : base(2, 2, 1, Advance.Philosophy, Advance.Literacy)
		{
			Name = "Democracy";
			Type = Advance.Democracy;
		}
	}
}