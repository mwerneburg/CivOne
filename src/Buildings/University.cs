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
	internal class University : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A UNIVERSITY adds a further 50% to",
			"the SCIENCE output of the city.",
			"",
			"Its effect stacks with the",
			"LIBRARY.",
		};

		private static readonly string[] _page2 =
		{
			"Requires UNIVERSITY.",
			"",
			"Expensive to maintain, so build it",
			"in cities with real trade to",
			"multiply rather than in every",
			"settlement you own.",
			"",
			"COPERNICUS' OBSERVATORY doubles",
			"the science of its city outright.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public University() : base(16, 3)
		{
			Name = "University";
			RequiredTech = new Advances.University();
			SetIcon(2, 2, false);
			SetSmallIcon(2, 1);
			Type = Building.University;
		}
	}
}