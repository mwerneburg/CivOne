// CivOne tests
//
// The briefings told the player to do something the rules forbade for 150 turns.
//
// Spaceship parts are gated five ways (Player.cs:701), and the one that bites is EXOTIC
// FUEL: before it, not one part can be laid. That gate was added deliberately and is
// documented at length — it began as a speed limit, which proved useless because a full
// 51/16/12 hull crosses in 45 turns even at 0.1c, so it became a construction gate to buy
// the ~150 years the arc needs.
//
// The council briefings were never updated to match. They still described the world before
// the gate existed, and so did the message at the other end: the fuel advisory announced a
// better crossing SPEED, which is exactly what the fuel did under the old model.
//
// Measured in game 3de868a5:
//
//   1782 AD (t366)  SETI: "Contingency A: Establish colony at Alpha Centauri II"
//   1822 AD (t386)  Tau Ceti: "OPTION B: ACCELERATE ALPHA CENTAURI COLONIZATION"
//   1990 AD (t540)  exotic fuel arrives — the first part becomes buildable
//
// 168 years of being told to accelerate something that could not be started. The player
// acted on it. Nothing was wrong with the rule; the text described a retired one.
//
// These are source pins because the screens build Picture[] and keep no strings, the same
// reason CultureBlockedByWarTests pins its advisory wording.

using System.IO;
using CivOne;

namespace CivOne.Tests
{
	public class SpaceRaceBriefingTests
	{
		private static string Source(params string[] parts) =>
			File.ReadAllText(Path.Combine(Sim.RepoRoot(), Path.Combine(parts)));

		private static string Seti => Source("src", "Screens", "SETISignalTransmission.cs");
		private static string TauCeti => Source("src", "Screens", "TauCetiApproachWarning.cs");
		private static string PlayerSrc => Source("src", "Player.cs");
		private static string GameSrc => Source("src", "Game.cs");

		// The council may recommend the destination. It may not order a colony the yards
		// cannot begin.
		[Fact]
		public void TheSetiBriefingDoesNotOrderAColonyThatCannotBeStarted()
		{
			string s = Seti;

			Assert.Contains("Directive 7. SUSPENDED.", s);
			Assert.Contains("Reason: propulsion.", s);
		}

		// Same for the approach warning, which was the more misleading of the two: it read as
		// an imperative with a deadline attached.
		[Fact]
		public void TheApproachWarningNamesPropulsionAsTheBlocker()
		{
			string s = TauCeti;

			Assert.DoesNotContain("OPTION B: ACCELERATE ALPHA CENTAURI COLONIZATION.", s);
			Assert.Contains("NOT PRESENTLY POSSIBLE", s);
			Assert.Contains("Blocker: propulsion.", s);
		}

		// ...and it still points somewhere, or the option is just a closed door. The hook is a
		// deduction the council can honestly make: whatever is coming crossed the same gulf.
		[Fact]
		public void TheApproachWarningStillGivesThePlayerAThreadToPull()
		{
			Assert.Contains("crossed further, faster", TauCeti);
		}

		// The other end. The news when the fuel lands is that construction is possible at all,
		// not that crossings got quicker.
		[Fact]
		public void TheFuelAdvisoryAnnouncesBuildabilityNotJustSpeed()
		{
			string s = GameSrc;
			int at = s.IndexOf("fp.HasExoticFuel = true;");
			Assert.True(at > 0, "the fuel grant has moved");
			string block = s.Substring(at, 900);

			Assert.Contains("lay a hull", block);
			Assert.DoesNotContain("\"Our ships can cross at a fifth\"", block);
		}

		// The coupling, and the point of the whole file: the prose is only correct BECAUSE the
		// fuel gate exists. If somebody removes that gate the briefings become wrong again in
		// the opposite direction — telling the player they cannot start something they can —
		// and this fails to say so, rather than the drift going unnoticed for another 150
		// turns of somebody's game.
		[Fact]
		public void TheBriefingsAgreeWithTheGateTheyDescribe()
		{
			string p = PlayerSrc;
			int at = p.IndexOf("if (building is ISpaceShip)");
			Assert.True(at > 0, "the spaceship availability gate has moved");
			string gate = p.Substring(at, 2200);

			bool fuelGatesConstruction = gate.Contains("HasExoticFuel");

			Assert.True(fuelGatesConstruction,
				"exotic fuel no longer gates spaceship construction — the SETI and Tau Ceti " +
				"briefings say colonization is blocked on propulsion and must be rewritten " +
				"to match, or the player is told the opposite of the truth again");
		}
	}
}
