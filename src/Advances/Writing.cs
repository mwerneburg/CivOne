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
	internal class Writing : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"WRITING fixes words where memory",
			"cannot reach.",
			"",
			"Allows the LIBRARY, the DIPLOMAT",
			"and MARCO POLO'S VOYAGE.",
		};

		private static readonly string[] _page2 =
		{
			"The library adds half again to a",
			"city's science, and the diplomat",
			"opens every quieter path to power:",
			"embassies, bribery and theft.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Writing() : base(3, 1, 1, Advance.Alphabet)
		{
			Name = "Writing";
			Type = Advance.Writing;
		}
	}
}