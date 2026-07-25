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
	internal class RecyclingCenter : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A RECYCLING CENTER cuts the",
			"POLLUTION produced by the city's",
			"INDUSTRY to a THIRD.",
		};

		private static readonly string[] _page2 =
		{
			"Requires RECYCLING.",
			"",
			"The strongest cure for industrial",
			"smoke, and the right answer for a",
			"city with a factory and a",
			"manufacturing plant.",
			"",
			"It does nothing about population",
			"pollution; that needs MASS",
			"TRANSIT.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public RecyclingCenter() : base(20, 2)
		{
			Name = "Recycling Cntr.";
			RequiredTech = new Recycling();
			SetIcon(4, 0, true);
			SetSmallIcon(3, 2);
			Type = Building.RecyclingCenter;
		}
	}
}