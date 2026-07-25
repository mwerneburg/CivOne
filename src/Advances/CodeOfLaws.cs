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
	internal class CodeOfLaws : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE CODE OF LAWS sets down what is",
			"owed and what is forbidden, so",
			"that rule does not rest on the",
			"will of one man.",
			"",
			"Allows the COURTHOUSE.",
		};

		private static readonly string[] _page2 =
		{
			"Courthouses halve corruption, which",
			"matters most in the cities furthest",
			"from your capital.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CodeOfLaws() : base(2, 0, 1, Advance.Alphabet)
		{
			Name = "Code of Laws";
			Type = Advance.CodeOfLaws;
		}
	}
}