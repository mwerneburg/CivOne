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
	internal class Steel : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"STEEL gives a metal both hard and",
			"tough, and ships may be built to",
			"any size.",
			"",
			"Allows the BATTLESHIP.",
		};

		private static readonly string[] _page2 =
		{
			"A battleship is a fleet in itself",
			"and a burden on the treasury.",
			"Build one, not four.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Steel() : base(3, 0, 0, Advance.Metallurgy, Advance.Industrialization)
		{
			Name = "Steel";
			Type = Advance.Steel;
		}
	}
}