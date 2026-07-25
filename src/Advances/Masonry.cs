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
	internal class Masonry : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MASONRY teaches the cutting and",
			"fitting of stone.",
			"",
			"Allows CITY WALLS, the PALACE,",
			"THE GREAT WALL and THE PYRAMIDS.",
		};

		private static readonly string[] _page2 =
		{
			"City walls multiply a defender's",
			"strength twelvefold. No other",
			"early advance protects so much",
			"for so little.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Masonry() : base(2, 1, 2)
		{
			Name = "Masonry";
			Type = Advance.Masonry;
		}
	}
}