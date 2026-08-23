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
	// Twelve wonders can turn (docs/cursed_wonders.md). Nothing in the reference
	// book said so — a player could lose a city to the grey goo without ever
	// learning that wonders carry risk at all.
	//
	// DELIBERATELY NAMES NOBODY. Design rule 4 of the roster is that each cursed
	// wonder carries one quiet warning sentence in its OWN entry, "so the curse
	// is retrospectively fair" — the player is meant to be surprised, then able
	// to look back and find the line that told them. A roster here would replace
	// that with a lookup table and make twelve foreshadow sentences decorative.
	//
	// So this page documents the RULES and not the roster: that a minority of
	// wonders carry risk, that the blessing usually still arrives, that a curse
	// is a crisis to be played rather than a punishment to be absorbed, and that
	// the warning is always already on the wonder's own page. Everything a player
	// needs to make an informed decision; nothing that spoils the first meeting.
	//
	// Rule 5 exemption stated on page 2 because it is genuinely useful and
	// genuinely safe: SETI, the Interstellar Probe, Apollo, the Dome components,
	// the Fusion Core and the South Pole Expedition carry the Tau Ceti arc and
	// are never cursed. Knowing which wonders are SAFE spoils nothing.
	internal class CursedWonders : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"Most wonders are exactly what they",
			"promise. A few are not.",
			"",
			"A handful of the world's wonders",
			"carry a risk. Usually the blessing",
			"arrives as written. Sometimes",
			"something else does.",
			"",
			"A wonder that turns is not simply",
			"lost. It gives what it offered AND",
			"asks its own price, and the price",
			"is always something you can fight,",
			"outlast, or undo.",
			"",
			"Read the entry before you build.",
			"The old accounts always warn you.",
			"They are never specific.",
		};

		private static readonly string[] _page2 =
		{
			"Most wonders carry no risk at all,",
			"and none of the works that carry",
			"humanity to the stars ever do —",
			"the deep-space projects and the",
			"great Dome are safe to build.",
			"",
			"When a wonder does turn, you will",
			"know. The event announces itself.",
			"",
			"What follows has an end: a beast",
			"that can be killed, a sickness",
			"that can be cured, a ruin that can",
			"be cleansed. Nothing is permanent",
			"except what you let stand.",
			"",
			"Build boldly. Read first.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CursedWonders()
		{
			Name = "Cursed Wonders";
		}
	}
}
