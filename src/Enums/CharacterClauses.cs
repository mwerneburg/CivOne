// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Enums
{
	// Which clauses of Game.AssessCharacter a caller reads.
	//
	// There were two copies of this rubric, and they had already drifted: the visitor
	// archetype draw weighed government, wars, temples, culture and pollution, while the
	// South Pole expedition's curse roll weighed government, wars and pollution alone. That
	// difference may well have been deliberate — but it was invisible, recorded nowhere, and
	// the only way to see it was to read both copies side by side and notice what was
	// missing from one.
	//
	// One rubric now, and each caller states what it reads. The expedition's narrowness is
	// unchanged and no longer accidental.
	[System.Flags]
	internal enum CharacterClauses
	{
		None       = 0,
		Government = 1,
		Wars       = 2,
		Temples    = 4,
		Culture    = 8,
		Pollution  = 16,

		// What the visitors read when they judge the species, and what Starlab reads when it
		// judges the civilization that built it.
		Whole = Government | Wars | Temples | Culture | Pollution,

		// What comes back from the ice. Narrower on purpose: the expedition is a question
		// about competence and recklessness — who you sent, how distracted you were, how
		// filthy your industry is — and not about whether your cities have temples in them.
		Expedition = Government | Wars | Pollution,
	}
}
