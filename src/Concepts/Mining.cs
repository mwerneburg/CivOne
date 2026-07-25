// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Concepts
{
	internal class Mining : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"SETTLERS add a MINE to hills,",
			"mountains, or desert tiles.",
			"",
			"Mining raises PRODUCTION (shields)",
			"— most on hills and mountains,",
			"where the ore lies.",
			"",
			"A tile cannot be both mined and",
			"irrigated; pick shields or food.",
		};

		private static readonly string[] _page2 =
		{
			"Shields build your units, city",
			"improvements, and wonders, so",
			"mined hills power an industrial",
			"heartland.",
			"",
			"Ring a production city with mines",
			"and a food city with irrigation,",
			"then link them by road.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Mining()
		{
			Name = "Mining";
		}
	}
}