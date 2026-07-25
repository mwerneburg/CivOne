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
	internal class Astronomy : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ASTRONOMY charts the movements of",
			"the heavens and finds them",
			"regular.",
			"",
			"Allows COPERNICUS' OBSERVATORY.",
		};

		private static readonly string[] _page2 =
		{
			"The stars give sailors their",
			"position and scholars their first",
			"proof that the world obeys law.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Astronomy() : base(6, 0, 0, Advance.Mysticism, Advance.Mathematics)
		{
			Name = "Astronomy";
			Type = Advance.Astronomy;
		}
	}
}