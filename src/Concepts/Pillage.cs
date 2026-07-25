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
	internal class Pillage : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"PILLAGE orders a military unit to",
			"destroy a tile improvement — a",
			"road, railroad, irrigation, mine,",
			"or fortress.",
			"",
			"It takes the unit's turn on any",
			"tile the unit occupies.",
		};

		private static readonly string[] _page2 =
		{
			"Pillaging slows an invader,",
			"cripples an enemy's economy, and",
			"cuts their rail movement.",
			"",
			"Wrecking a border road can strand",
			"an approaching army in the open.",
			"",
			"Only military units pillage;",
			"SETTLERS rebuild what is lost.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Pillage()
		{
			Name = "Pillage";
		}
	}
}