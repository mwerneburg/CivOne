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
	internal class Invention : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"INVENTION is the habit of",
			"improving a thing rather than",
			"inheriting it.",
			"",
			"Allows LEONARDO'S WORKSHOP.",
		};

		private static readonly string[] _page2 =
		{
			"Leonardo's Workshop upgrades your",
			"obsolete units as each advance",
			"retires them — worth more the",
			"larger and older your army is.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Invention() : base(6, 2, 1, Advance.Engineering, Advance.Literacy)
		{
			Name = "Invention";
			Type = Advance.Invention;
		}
	}
}