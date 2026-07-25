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

namespace CivOne.Buildings
{
	internal class Cathedral : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A CATHEDRAL calms 2 unhappy",
			"citizens, making it the strongest",
			"single cure for civil disorder.",
			"",
			"It also shelters the faithful from",
			"certain darker influences.",
		};

		private static readonly string[] _page2 =
		{
			"Requires RELIGION.",
			"",
			"MICHELANGELO'S CHAPEL grants the",
			"same effect to every city on its",
			"continent.",
			"",
			"A city with a cathedral is immune",
			"to the madness of the KING IN",
			"YELLOW.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Cathedral() : base(16, 3)
		{
			Name = "Cathedral";
			RequiredTech = new Religion();
			SetIcon(2, 1, true);
			SetSmallIcon(2, 0);
			Type = Building.Cathedral;
		}
	}
}