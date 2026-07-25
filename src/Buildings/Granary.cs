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
	internal class Granary : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A GRANARY keeps the city's food",
			"store HALF FULL after each time it",
			"grows or starves.",
			"",
			"Cities with one grow roughly twice",
			"as fast.",
		};

		private static readonly string[] _page2 =
		{
			"Requires POTTERY.",
			"",
			"Cheap, and its value rises with",
			"every food surplus the city can",
			"manage.",
			"",
			"It also softens famine: a starving",
			"city that shrinks does not begin",
			"again from nothing.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Granary() : base(6, 1)
		{
			Name = "Granary";
			RequiredTech = new Pottery();
			SetIcon(0, 1, true);
			SetSmallIcon(0, 2);
			Type = Building.Granary;
		}
	}
}