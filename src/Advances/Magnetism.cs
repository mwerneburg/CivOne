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
	internal class Magnetism : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MAGNETISM gives the compass, and",
			"with it a bearing in any weather.",
			"",
			"Allows the FRIGATE.",
		};

		private static readonly string[] _page2 =
		{
			"The frigate both fights and",
			"carries troops, which no earlier",
			"ship managed. It retires the SAIL.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Magnetism() : base(6, 0, 1, Advance.Navigation, Advance.Physics)
		{
			Name = "Magnetism";
			Type = Advance.Magnetism;
		}
	}
}