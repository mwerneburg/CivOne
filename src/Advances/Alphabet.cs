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
	internal class Alphabet : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"The ALPHABET reduces speech to a",
			"handful of marks that anyone may",
			"learn.",
			"",
			"Every record, every law and every",
			"letter your civilization will ever",
			"write begins here.",
		};

		private static readonly string[] _page2 =
		{
			"It grants no unit and no building,",
			"yet almost the whole tree grows",
			"from it. Research it early.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Alphabet() : base(3, 2, 1)
		{
			Name = "Alphabet";
			Type = Advance.Alphabet;
		}
	}
}