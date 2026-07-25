// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Wonders
{
	internal class JSBachsCathedral : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"J.S.BACH'S CATHEDRAL fills a",
			"continent with music.",
			"",
			"It calms unhappy citizens in every",
			"city on the SAME CONTINENT.",
		};

		private static readonly string[] _page2 =
		{
			"Requires RELIGION.",
			"",
			"A powerful cure for the unrest of",
			"a large, crowded empire — build it",
			"where most of your cities lie.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public JSBachsCathedral() : base(40)
		{
			Name = "J.S.Bach's Cathedral";
			RequiredTech = new Religion();
			ObsoleteTech = null;
			SetSmallIcon(6, 3);
			Type = Wonder.JSBachsCathedral;
		}
	}
}