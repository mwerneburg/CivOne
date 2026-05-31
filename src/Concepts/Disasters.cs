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
	internal class Disasters : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"Each turn, two random cities of",
			"size 5 or larger may suffer a",
			"DISASTER. The kind is rolled at",
			"random and only fires if its",
			"local trigger is met:",
			"",
			"  EARTHQUAKE — Hills nearby",
			"  PLAGUE     — no AQUEDUCT",
			"  FLOODING   — River, no Walls",
			"  VOLCANO    — Mountains, no Temple",
			"  FAMINE     — no GRANARY",
			"  FIRE       — no AQUEDUCT",
			"  PIRATES    — Ocean, no Barracks",
			"  FEVER      — Jungle, no Medicine",
			"  RIOT       — unhappy > happy",
		};

		private static readonly string[] _page2 =
		{
			"Each disaster has its own effect.",
			"Most kill 1/4 to 1/3 of the city's",
			"size; EARTHQUAKE and FIRE instead",
			"destroy a random building (never",
			"the Palace); PIRATES zero food and",
			"shields for the turn; RIOT can let",
			"a happier city annex yours.",
			"",
			"FEVER spares the Olvir and any",
			"jungle tile with a Canopy Array.",
			"",
			"HURRICANES are tracked separately",
			"— see the HURRICANE entry for the",
			"latitude-band rules and the SEA",
			"PLATFORM mitigation.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Disasters()
		{
			Name = "Disasters";
		}
	}
}
