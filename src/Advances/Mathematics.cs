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
	internal class Mathematics : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MATHEMATICS measures angle, weight",
			"and trajectory.",
			"",
			"Allows the CATAPULT.",
		};

		private static readonly string[] _page2 =
		{
			"The catapult is the first answer",
			"to a walled city, and mathematics",
			"is the road to every science that",
			"follows.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Mathematics() : base(7, 1, 1, Advance.Alphabet, Advance.Masonry)
		{
			Name = "Mathematics";
			Type = Advance.Mathematics;
		}
	}
}