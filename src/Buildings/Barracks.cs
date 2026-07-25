// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class Barracks : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"BARRACKS train new land units to",
			"VETERAN status, granting them a",
			"50% combat bonus.",
			"",
			"Damaged units resting in the city",
			"also recover fully in one turn.",
		};

		private static readonly string[] _page2 =
		{
			"Requires no advance; available",
			"from the first turn.",
			"",
			"Cheap, but the upkeep is a steady",
			"drain. A city that is not building",
			"an army does not need them.",
			"",
			"Coastal cities without barracks",
			"attract PIRATES.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Barracks() : base(4)
		{
			Name = "Barracks";
			RequiredTech = null;
			SetIcon(0, 0, true);
			SetSmallIcon(0, 1);
			Type = Building.Barracks;
		}
	}
}