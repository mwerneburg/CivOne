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
	internal class Religion : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"RELIGION binds a people to a",
			"shared faith and a shared calendar.",
			"",
			"Allows the CATHEDRAL, HAGIA SOFIA,",
			"MICHELANGELO'S CHAPEL and J.S.",
			"BACH'S CATHEDRAL.",
		};

		private static readonly string[] _page2 =
		{
			"The cathedral calms two unhappy",
			"citizens, the strongest single",
			"remedy for disorder.",
			"",
			"A cathedral also shelters a city",
			"from certain influences that",
			"reason cannot argue with.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Religion() : base(3, 2, 0, Advance.Philosophy, Advance.Writing)
		{
			Name = "Religion";
			Type = Advance.Religion;
		}
	}
}