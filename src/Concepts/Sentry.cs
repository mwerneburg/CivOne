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
	internal class Sentry : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"SENTRY sets a unit to watch and",
			"wait.",
			"",
			"It skips its turns quietly until",
			"an enemy comes into view, then",
			"wakes for your orders.",
			"",
			"It saves you clicking through",
			"idle scouts and pickets.",
		};

		private static readonly string[] _page2 =
		{
			"Use SENTRY for lookouts on hills,",
			"ships patrolling a coast, or units",
			"resting in a city.",
			"",
			"A sentry unit still defends",
			"normally if attacked.",
			"",
			"To hold ground and gain a defence",
			"bonus, use FORTIFY instead.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Sentry()
		{
			Name = "Sentry";
		}
	}
}