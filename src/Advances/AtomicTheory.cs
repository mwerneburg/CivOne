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
	internal class AtomicTheory : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ATOMIC THEORY holds that matter is",
			"built from particles too small to",
			"see.",
			"",
			"It grants no unit or building of",
			"its own.",
		};

		private static readonly string[] _page2 =
		{
			"For a century it changes nothing",
			"anyone can point to. Then it",
			"changes everything.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public AtomicTheory() : base(5, 2, 1, Advance.TheoryOfGravity, Advance.Physics)
		{
			Name = "Atomic Theory";
			Type = Advance.AtomicTheory;
		}
	}
}