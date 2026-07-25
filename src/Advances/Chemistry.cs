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
	internal class Chemistry : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"CHEMISTRY separates substances and",
			"learns what they are made of.",
			"",
			"It grants no unit or building of",
			"its own.",
		};

		private static readonly string[] _page2 =
		{
			"A quiet advance with loud",
			"consequences: everything from",
			"engineering charges to fuel oil",
			"begins in the laboratory.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Chemistry() : base(1, 2, 1, Advance.University, Advance.Medicine)
		{
			Name = "Chemistry";
			Type = Advance.Chemistry;
		}
	}
}