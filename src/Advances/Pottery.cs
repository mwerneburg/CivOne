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
	internal class Pottery : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"POTTERY gives vessels that hold",
			"grain against the lean season.",
			"",
			"Allows the GRANARY and THE HANGING",
			"GARDENS.",
		};

		private static readonly string[] _page2 =
		{
			"A granary keeps a city's food",
			"store half full after each growth,",
			"which roughly doubles the speed at",
			"which the city grows.",
			"",
			"Nothing else in the tree depends",
			"on Pottery, but early expansion",
			"wins games.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Pottery() : base(7, 0, 2)
		{
			Name = "Pottery";
			Type = Advance.Pottery;
		}
	}
}