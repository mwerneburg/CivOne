// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne
{
	// One civilization's progress toward the things it can win.
	//
	// This state used to live on Game as parallel arrays indexed by player number —
	// EconStreak[n], CultureStreak[n], and so on — and every one of them had to be sized in
	// TWO places: Array.Resize in AddPlayer for a live game, and `new uint[slotCount]` in the
	// Game(CosFile) constructor, because the load path builds _players directly and never
	// calls AddPlayer. Missing the second made LoadCos return false for every save in the
	// suite, and no targeted test could see it: none of them load a save.
	//
	// Owned by the Player and built with it, there is nothing to size and nothing to keep in
	// step. A player that exists has its progress; the bug is unwritable rather than merely
	// unwritten.
	internal sealed class PlayerProgress
	{
		// ── Pax Mercatoria ───────────────────────────────────────────────────
		// Consecutive turns holding more than half the world's gross output, with the
		// other clauses met. Broken by any turn that misses one.
		public uint EconStreak;

		// ── Cultural Ascendancy ──────────────────────────────────────────────
		// Consecutive turns holding the cultural shadow and the lead over the best rival.
		public uint CultureStreak;

		// ── Diaspora ─────────────────────────────────────────────────────────
		// A colony stands at Alpha Centauri II. Cleared if it is lost — to the organism,
		// or to the Registry's pickets taking the ship that would have founded it.
		public bool ColonyFounded;

		// Consecutive turns the colony has been supplied from a standing Mission Control.
		// Losing the building resets this to zero; it can be rebuilt and the clock restarts.
		public uint DiasporaStreak;

		// Arrival rank at Alpha Centauri: 0 = not landed, 1 = the first colony in this
		// world, and so on. Being first is the achievement; the fifth ship to make the same
		// crossing has proved nothing new, and Game.DiasporaAward reads this to say so.
		public int ColonyOrder;

		// ── the ship itself ──────────────────────────────────────────────────
		// Parts are counted on the civilization rather than held by a city, which is what
		// lets several cities contribute to one hull.

		// Turn the ship left Earth, 0 if it has not. Stays set for the rest of the game,
		// including after the ship is lost — ArrivalTurn is what tells you it is still
		// flying, and ResetSpaceProgramme clears both when a programme ends.
		public int SpaceshipLaunchTurn;

		// Turn it reaches Alpha Centauri, 0 if nothing is in flight. Zeroed by arrival AND
		// by interception alike; ColonyFounded distinguishes the two.
		public int SpaceshipArrivalTurn;

		public int SpaceshipStructural;
		public int SpaceshipComponent;
		public int SpaceshipModule;

		// ── diplomacy ────────────────────────────────────────────────────────
		// Player numbers this civilization DECLARED WAR ON, pact-honouring excluded. Both
		// streak victories refuse a civ that started a war it is still fighting; a defensive
		// war breaks nothing.
		//
		// Was Game.HumanStartedWars, a single set, because only the human could win those
		// paths. Generalising it beat the alternative — "any war breaks your streak" for the
		// AI against "wars you started" for the human — which would have been a thumb on the
		// scale needing explanation forever.
		public readonly System.Collections.Generic.HashSet<byte> StartedWarsWith = new();
	}
}
