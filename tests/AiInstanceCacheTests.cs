// CivOne tests
//
// AI._instances is a Dictionary<Player, AI>, and Player.GetHashCode() returns
// Game.PlayerNumber(this) — the player's SLOT INDEX IN THE CURRENT GAME (Player.cs:1107),
// with Equals to match. Player therefore has VALUE equality on a number that changes meaning
// the moment a new game starts.
//
// Nothing cleared the cache. So starting a new game, or loading a save without restarting the
// process, returned AI objects still bound to the PREVIOUS game's Player objects — and every
// decision those AIs made read the old game's advances, cities and gold.
//
// Found the hard way: DryGroundImprovementTests passed alone and failed in the full suite,
// because by the fortieth Sim.NewGame the cache was handing back AIs bound to long-dead
// players, and WorkAvailable's tech test read false for an advance the live player had.

using System.Linq;
using CivOne.Advances;

namespace CivOne.Tests
{
	public class AiInstanceCacheTests
	{
		// Slot 0 is where this bites, and only slot 0. Game.PlayerNumber returns 0 both for
		// the barbarians AND for any player not in the current game, so a DEAD player from a
		// previous game hashes to 0 and compares equal to whoever holds slot 0 now. Players
		// with a real slot number do not collide — which is how the first version of this
		// test, written against a non-zero player, passed with the fix removed.
		//
		// Slot 0 is not an edge case: Game.CurrentPlayer on a fresh game IS the barbarians.
		[Fact]
		public void ANewGameDoesNotInheritTheLastGamesAi()
		{
			Sim.NewGame(width: 80, height: 50);
			Player first = Game.Instance.CurrentPlayer;
			Assert.Equal(0, Game.Instance.PlayerNumber(first));
			AI firstAi = AI.Instance(first);
			Assert.Same(first, firstAi.Player);

			Sim.NewGame(width: 80, height: 50);
			Player second = Game.Instance.CurrentPlayer;
			Assert.NotSame(first, second);
			Assert.True(second.Equals(first), "fixture: these should collide, or the test proves nothing");

			AI secondAi = AI.Instance(second);

			Assert.Same(second, secondAi.Player);
		}

		// The symptom that exposed it: an advance given to the LIVE player is invisible to the
		// AI, because the AI is looking at a dead one.
		[Fact]
		public void TheAiSeesTheLivePlayersAdvances()
		{
			Sim.NewGame(width: 80, height: 50);
			AI.Instance(Game.Instance.CurrentPlayer);

			Sim.NewGame(width: 80, height: 50);
			Player live = Game.Instance.CurrentPlayer;
			live.AddAdvance(new Masonry(), false);

			Assert.True(AI.Instance(live).Player.HasAdvance<Masonry>(),
				"the AI is reasoning about a player from a previous game");
		}

		// Player's value equality is the mechanism, and it is load-bearing elsewhere (the
		// byte conversions and comparisons all over the codebase depend on it), so this pins
		// the hazard rather than proposing to change it.
		[Fact]
		public void PlayerHashesOnItsSlotNotItsIdentity()
		{
			Sim.NewGame(width: 80, height: 50);
			Player a = Game.Instance.CurrentPlayer;
			int hashA = a.GetHashCode();

			Sim.NewGame(width: 80, height: 50);
			Player b = Game.Instance.CurrentPlayer;

			Assert.NotSame(a, b);
			Assert.Equal(hashA, b.GetHashCode());
			// ...which is precisely why any Dictionary keyed by Player must be cleared when
			// the game is replaced.
		}
	}
}
