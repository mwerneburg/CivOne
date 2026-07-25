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
	internal class TheoryOfGravity : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE THEORY OF GRAVITY explains the",
			"fall of an apple and the orbit of",
			"a moon with one law.",
			"",
			"Allows ISAAC NEWTON'S COLLEGE.",
		};

		private static readonly string[] _page2 =
		{
			"Newton's College raises the",
			"science bonus of LIBRARIES,",
			"UNIVERSITIES and OBSERVATORIES",
			"from a half to two thirds.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TheoryOfGravity() : base(2, 1, 1, Advance.Astronomy, Advance.University)
		{
			Name = "Theory of Gravity";
			Type = Advance.TheoryOfGravity;
		}
	}
}