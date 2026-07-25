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
	internal class Temple : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A TEMPLE calms 1 unhappy citizen,",
			"the earliest and cheapest remedy",
			"for civil disorder.",
			"",
			"Its effect stacks with the",
			"COLOSSEUM and the CATHEDRAL.",
		};

		private static readonly string[] _page2 =
		{
			"Requires CEREMONIAL BURIAL.",
			"",
			"Build one in every city as it",
			"passes size 4. A city in disorder",
			"produces nothing at all, and the",
			"temple is far cheaper than the",
			"lost turns.",
			"",
			"THE ORACLE doubles its effect.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Temple() : base(4, 1)
		{
			Name = "Temple";
			RequiredTech = new CeremonialBurial();
			SetIcon(0, 2, true);
			SetSmallIcon(0, 3);
			Type = Building.Temple;
		}
	}
}