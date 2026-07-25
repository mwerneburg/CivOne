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
	internal class Robotics : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ROBOTICS sets machines to build",
			"machines.",
			"",
			"Allows ARTILLERY, the",
			"MANUFACTURING PLANT and the SS",
			"MODULE.",
		};

		private static readonly string[] _page2 =
		{
			"It retires the CANNON.",
			"",
			"Artillery outguns every other land",
			"unit and ignores CITY WALLS.",
			"",
			"The manufacturing plant doubles a",
			"city's shields again — and",
			"pollutes heavily.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Robotics() : base(7, 2, 0, Advance.Plastics, Advance.Computers)
		{
			Name = "Robotics";
			Type = Advance.Robotics;
		}
	}
}