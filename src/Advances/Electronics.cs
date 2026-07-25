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
	internal class Electronics : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ELECTRONICS controls current",
			"finely enough to compute, switch",
			"and broadcast.",
			"",
			"Allows the HYDRO PLANT and THE",
			"HOOVER DAM.",
		};

		private static readonly string[] _page2 =
		{
			"A hydro plant doubles a factory's",
			"output and halves its smoke. The",
			"Hoover Dam does the same for a",
			"whole continent.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Electronics() : base(4, 2, 1, Advance.Electricity)
		{
			Name = "Electronics";
			Type = Advance.Electronics;
		}
	}
}