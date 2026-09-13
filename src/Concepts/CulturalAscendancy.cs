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
	// The culture VICTORY, as distinct from Concepts.Culture, which explains what
	// culture is and how a city defects. This page explains how the path is won,
	// because every clause of it surprises somebody:
	//
	//   - the measure is per head, and raw culture leads a different race (in
	//     run 3de868a5 the human's raw curve towered over the field from turn 170
	//     while the Lakota, on a third of the culture, ran the victory clock);
	//   - the divisor is PEAK populace, so starving cities buys nothing;
	//   - there is a populace FLOOR, and a deliberately tiny empire fails it —
	//     which is the trap a player reasoning only about per-head walks into.
	//
	// Numbers are stated once here and pinned to the constants by a test, for the
	// same reason CulturalPopulaceFloor has one definition: a reference page that
	// drifts from the rule is worse than no page, because the player believes it.
	internal class CulturalAscendancy : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"CULTURAL ASCENDANCY falls to the",
			"civilization the world most",
			"admires.",
			"",
			"The measure is culture PER HEAD:",
			"everything your cities have ever",
			"made, divided by the largest",
			"population you have ever held.",
			"",
			"From 1850 the clock may start.",
			"Stay a tenth ahead of every",
			"ranked rival for 75 years and the",
			"age is yours.",
			"",
			"Raw culture is a different race",
			"with a different leader. Read the",
			"per-head line on the score graph;",
			"that is the one that decides.",
		};

		private static readonly string[] _page2 =
		{
			"To be ranked you must be a nation",
			"and not a relic: your people must",
			"number at least half the median",
			"of the ranked civilizations.",
			"",
			"Small cities are efficient. A",
			"Temple costs the same in a town",
			"of three as in a city of twenty,",
			"and you are divided by people.",
			"",
			"So keep them small — and keep",
			"MANY. A handful of towns will",
			"never clear the floor.",
			"",
			"An ARTIST adds two culture and no",
			"new mouths. Starving a city adds",
			"nothing: you are divided by your",
			"greatest population, not today's.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CulturalAscendancy()
		{
			Name = "Cultural Ascendancy";
		}
	}
}
