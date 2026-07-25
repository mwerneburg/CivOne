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
	internal class Engineering : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ENGINEERING applies the wheel and",
			"the arch to works of real scale.",
			"",
			"Allows the SEWER SYSTEM.",
		};

		private static readonly string[] _page2 =
		{
			"A sewer system lets a city grow",
			"past size 12, as the aqueduct once",
			"let it pass 6.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Engineering() : base(4, 1, 1, Advance.TheWheel, Advance.Construction)
		{
			Name = "Engineering";
			Type = Advance.Engineering;
		}
	}
}