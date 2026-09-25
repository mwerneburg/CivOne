// CivOne tests
//
// A crash in play (Sep 2026, caught by the new crash.log): attacking Sparta before 1 AD
// took the Greeks' last city, and the next frame the sidebar's race strip threw
// "Collection was modified" from Game.StreakLeader.
//
// Player.IsDestroyed is not a question. The first call that finds a civ with nothing left
// fires PlayerDestroyed, and before 1 AD that respawns the buddy civilization INTO THE SAME
// PLAYER SLOT — rewriting the list any caller was walking. About thirty loops over the
// players call IsDestroyed, the start of EndTurn among them. The swap is now queued and
// applied at the top of EndTurn and Update, where nothing is iterating.

using System.Linq;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class RespawnDuringLoopTests
	{
		// An AI civ in an early slot, before 1 AD, about to be found destroyed: no city, and
		// its starting units gone.
		private static (Game g, byte slot, int buddyId) ACivAboutToFall()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player victim = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0
				&& p != g.HumanPlayer && p.Civilization.Id >= 1 && p.Civilization.Id <= 14);
			byte slot = g.PlayerNumber(victim);
			int id = victim.Civilization.Id;
			// As in play: a last city, captured. Disbanding asks IsDestroyed and would trigger
			// the respawn early, outside any loop — so the units go while the city still
			// stands, and the capture itself (an owner change) asks nothing.
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			City last = g.AddCity(victim, 0, 40, 25)!;
			last.Size = 1;
			foreach (IUnit u in g.GetUnits().Where(u => u.Owner == slot).ToArray())
				g.DisbandUnit(u);
			last.Owner = g.PlayerNumber(g.HumanPlayer);
			Assert.Equal(id, g.GetPlayer(slot).Civilization.Id);   // not yet noticed
			Assert.True(Common.TurnToYear(g.GameTurn) < 0, "fixture: the respawn rule only runs before 1 AD");
			Assert.True(g.Players.Last() != victim, "fixture: a destroyed LAST player cannot break the loop");
			Sim.ClearTasks();
			return (g, slot, id >= 8 ? id - 7 : id + 7);
		}

		[Fact]
		public void TheRaceStripSurvivesACivFallingMidLoop()
		{
			(Game g, byte _, int _) = ACivAboutToFall();

			var ex = Record.Exception(() => g.StreakLeader(p => p.CultureStreak));

			Assert.Null(ex);
		}

		// ...and the respawn still happens, at the next safe point.
		[Fact]
		public void TheBuddyStillArrivesAtTheNextTurn()
		{
			(Game g, byte slot, int buddyId) = ACivAboutToFall();
			g.StreakLeader(p => p.CultureStreak);   // finds it destroyed

			g.EndTurn();

			Assert.Equal(buddyId, g.GetPlayer(slot).Civilization.Id);
		}
	}
}
