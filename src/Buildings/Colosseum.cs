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
	internal class Colosseum : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A COLOSSEUM calms 1 unhappy",
			"citizen with games and spectacle.",
			"",
			"Its effect stacks with the TEMPLE",
			"and the CATHEDRAL.",
		};

		private static readonly string[] _page2 =
		{
			"Requires CONSTRUCTION.",
			"",
			"Costly for what it gives, so build",
			"it only when a city has outgrown",
			"what a temple alone can soothe.",
			"",
			"Raising LUXURY rates is often the",
			"faster remedy.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Colosseum() : base(10, 4)
		{
			Name = "Colosseum";
			RequiredTech = new Construction();
			SetIcon(3, 0, false);
			SetSmallIcon(2, 3);
			Type = Building.Colosseum;
		}
	}
}