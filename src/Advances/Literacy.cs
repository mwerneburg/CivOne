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
	internal class Literacy : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"LITERACY spreads reading beyond",
			"the priests and the clerks.",
			"",
			"Allows THE GREAT LIBRARY.",
		};

		private static readonly string[] _page2 =
		{
			"Four great roads leave this one",
			"advance: thought, machinery and",
			"two forms of government.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Literacy() : base(4, 1, 2, Advance.Writing, Advance.CodeOfLaws)
		{
			Name = "Literacy";
			Type = Advance.Literacy;
		}
	}
}