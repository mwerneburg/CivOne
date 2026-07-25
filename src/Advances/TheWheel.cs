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
	internal class TheWheel : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE WHEEL turns and everything",
			"that must be carried moves faster.",
			"",
			"Allows the CHARIOT.",
		};

		private static readonly string[] _page2 =
		{
			"The chariot is the strongest",
			"attacker of the ancient world.",
			"A civilization with wheels and a",
			"neighbour without them rarely",
			"stays the smaller of the two.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TheWheel() : base(3, 2, 2)
		{
			Name = "The Wheel";
			Type = Advance.TheWheel;
		}
	}
}