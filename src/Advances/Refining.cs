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
	internal class Refining : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"REFINING separates crude oil into",
			"the fuels that drive engines.",
			"",
			"Allows the POWER PLANT.",
		};

		private static readonly string[] _page2 =
		{
			"The power plant is the dirtiest",
			"of the three. Replace it with a",
			"HYDRO or NUCLEAR plant as soon as",
			"you are able.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Refining() : base(6, 2, 0, Advance.Chemistry, Advance.TheCorporation)
		{
			Name = "Refining";
			Type = Advance.Refining;
		}
	}
}