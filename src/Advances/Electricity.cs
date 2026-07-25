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
	internal class Electricity : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ELECTRICITY carries power along a",
			"wire to wherever it is needed.",
			"",
			"It grants no unit or building of",
			"its own.",
		};

		private static readonly string[] _page2 =
		{
			"The advance itself does little;",
			"everything that follows it —",
			"power, computing and flight —",
			"does a great deal.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Electricity() : base(8, 0, 0, Advance.Magnetism, Advance.Metallurgy)
		{
			Name = "Electricity";
			Type = Advance.Electricity;
		}
	}
}