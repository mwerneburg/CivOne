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
	internal class Physics : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"PHYSICS seeks the laws that",
			"govern motion, force and matter.",
			"",
			"It grants no unit or building of",
			"its own.",
		};

		private static readonly string[] _page2 =
		{
			"One of the great junctions of the",
			"tree. Everything mechanical in the",
			"modern age is downstream of it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Physics() : base(1, 2, 0, Advance.Mathematics, Advance.Navigation)
		{
			Name = "Physics";
			Type = Advance.Physics;
		}
	}
}