// CivOne tests
//
// The Barbarians do not claim exploration credit.
//
// Score gives 1 point per 10 tiles a player was FIRST in the world to reveal, and
// Game.ClaimTile awarded that to whoever got there first without asking who they were. Slot
// 0 is the Barbarians: they roam early, wide, and by sea, and they score nothing. Decoded
// from a real turn-379 save, they held 3,736 of the world's 16,085 claims — 23% of every
// tile ever claimed, locked away from the civs that could have earned from it, and the
// single largest holding in the game. The 2200 AD save was the same story at 4,090.
//
// This is what made exploration feel unrewarded late: a tile still black on your own map
// pays nothing if a barbarian legion crossed it in 3000 BC. Refusing the claim leaves the
// tile at 255 so the first real civ through still takes it.
//
// Slot 0 is also what PlayerNumber returns for a Player belonging to no game, so this
// covers the detached-player case too. The story factions (Olvir, The Others, The Thing,
// Skynet) only DECLARE PreferredPlayerNumber 0 — they join mid-game through AddPlayer,
// which appends, so they hold their own high slots and still claim normally.

using System.IO;
using System.Linq;

namespace CivOne.Tests
{
	public class BarbarianExplorationTests
	{
		// A tile nobody has revealed yet, so a claim attempt is actually decided by the rule
		// rather than by having lost a race at game setup.
		private static (int x, int y) AnUnclaimedTile(Game g)
		{
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				if (g.FirstExplorer[x, y] == 255) return (x, y);
			Assert.Fail("every tile on the map is already claimed");
			return default;
		}

		private static Player Barbarians(Game g) => g.Players.ElementAt(0);

		private static Player ACiv(Game g) =>
			g.Players.First(p => p is not null && g.PlayerNumber(p) != 0);

		// The rule, stated directly.
		[Fact]
		public void BarbariansNeverClaimATile()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			(int x, int y) = AnUnclaimedTile(g);

			Barbarians(g).Explore(x, y, 0);

			Assert.Equal(255, g.FirstExplorer[x, y]);
			Assert.Equal(0, Barbarians(g).ExplorationCredits);
		}

		// The payoff, and the test that separates this fix from the lazy version of it.
		// Skipping only the CREDIT while still stamping the tile would pass the test above
		// and leave the map exactly as locked as before.
		[Fact]
		public void ACivStillClaimsWhatTheBarbariansWalkedPast()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			(int x, int y) = AnUnclaimedTile(g);
			Player civ = ACiv(g);
			int before = civ.ExplorationCredits;

			Barbarians(g).Explore(x, y, 0);   // legion, 3000 BC
			civ.Explore(x, y, 0);             // settlers, some centuries later

			Assert.Equal(g.PlayerNumber(civ), g.FirstExplorer[x, y]);
			Assert.Equal(before + 1, civ.ExplorationCredits);
		}

		// ...and claiming still works at all. Making ClaimTile return false unconditionally
		// would satisfy both tests above.
		[Fact]
		public void ACivStillClaimsGroundNobodyTouched()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			(int x, int y) = AnUnclaimedTile(g);
			Player civ = ACiv(g);
			int before = civ.ExplorationCredits;

			civ.Explore(x, y, 0);

			Assert.Equal(g.PlayerNumber(civ), g.FirstExplorer[x, y]);
			Assert.Equal(before + 1, civ.ExplorationCredits);
		}

		// Saves already on disk carry the dead claims baked in, and a game in progress would
		// keep them forever. The load path releases them — without this the fix does nothing
		// for any game already being played.
		[Fact]
		public void LoadingReleasesClaimsTheBarbariansAlreadyHold()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			Player civ = ACiv(g);
			byte civIdx = g.PlayerNumber(civ);

			// Stamp the ledger the way an older binary would have left it: a barbarian
			// holding, and a civ holding beside it to prove the release is selective.
			(int bx, int by) = AnUnclaimedTile(g);
			g.FirstExplorer[bx, by] = 0;
			g.FirstExplorer[bx + 1, by] = civIdx;

			string path = Path.Combine(Settings.Instance.SavesDirectory, "barbclaims.cos");
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Game loaded = Game.Instance;
			Assert.Equal(255, loaded.FirstExplorer[bx, by]);
			Assert.Equal(civIdx, loaded.FirstExplorer[bx + 1, by]);
			Assert.Equal(0, loaded.Players.ElementAt(0).ExplorationCredits);
		}

		// The released tile is genuinely free afterwards, not merely blanked in a way some
		// other load-path pass would re-stamp.
		[Fact]
		public void AReleasedTileCanBeClaimedAfterTheLoad()
		{
			Sim.NewGame(width: 60, height: 40, competition: 4);
			Game g = Game.Instance;
			(int bx, int by) = AnUnclaimedTile(g);
			g.FirstExplorer[bx, by] = 0;

			string path = Path.Combine(Settings.Instance.SavesDirectory, "barbclaims2.cos");
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Game loaded = Game.Instance;
			Player civ = ACiv(loaded);
			int before = civ.ExplorationCredits;

			civ.Explore(bx, by, 0);

			Assert.Equal(loaded.PlayerNumber(civ), loaded.FirstExplorer[bx, by]);
			Assert.Equal(before + 1, civ.ExplorationCredits);
		}
	}
}
