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
	internal class Aqueduct : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"An AQUEDUCT carries fresh water to",
			"the city, allowing it to grow",
			"beyond SIZE 6.",
			"",
			"It also wards off PLAGUE, which",
			"strikes crowded cities that lack",
			"clean water.",
		};

		private static readonly string[] _page2 =
		{
			"Requires CONSTRUCTION.",
			"",
			"Without one, a city stalls at",
			"size 6 no matter how much food it",
			"gathers. Build it before the",
			"granary fills, or the surplus is",
			"simply wasted.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Aqueduct() : base(12, 2)
		{
			Name = "Aqueduct";
			RequiredTech = new Construction();
			SetIcon(1, 3, false);
			SetSmallIcon(1, 3);
			Type = Building.Aqueduct;
		}
	}
}