// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Concepts;

namespace CivOne.Concepts
{
	internal class VeteranUnits : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"VETERAN units fight at +50%",
			"strength, attacking and defending.",
			"",
			"A unit built in a city with",
			"BARRACKS starts as a veteran.",
			"",
			"Green units can also earn the",
			"rank by winning a battle.",
		};

		private static readonly string[] _page2 =
		{
			"Veterans tip close fights and",
			"survive where raw recruits fall.",
			"",
			"Keep BARRACKS in frontline cities",
			"so every unit musters ready for",
			"war.",
			"",
			"Some wonders grant veteran status",
			"to all your new units.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public VeteranUnits()
		{
			Name = "Veteran Units";
		}
	}
}