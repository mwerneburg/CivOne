// CivOne tests
//
// A settler on auto-improve walks to a tile, cannot work it, and blinks. Move it away by
// hand and it walks straight back to the same square and blinks again.
//
// The mechanism: MovementDone tried exactly one thing on arrival — work THIS tile — and if
// that failed it left AutoImprove set with no target. Re-picking only happened in NewTurn,
// and NewTurn's only exit from auto-improve is StartAutoImproveStep's next==here branch. So
// a player who nudges the blinking settler before the turn rolls over moves it off that
// branch: NewTurn re-picks the same tile from the new position and sends it back.
//
// The fix re-picks on arrival. Either it moves on to a job it can do, or FindNextImprovementTile
// hands back the tile underfoot, the same execute fails again, and auto-improve switches off —
// which is what the player expected to happen in the first place.

using System.Drawing;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class AutoImproveStallTests
	{
		// A fully-improved landscape: roads everywhere, irrigation everywhere, so nothing in
		// any city radius needs work and FindNextImprovementTile returns null.
		private static (Game game, Player human, Settlers settler) AFinishedLandscape()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Explore(40, 25, range: 20);
			g.AddCity(human, 0, 40, 25);

			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
			{
				ITile t = Map.Instance[x, y];
				if (t is null || t.City is not null) continue;
				t.Road = true;
				t.Irrigation = true;
			}

			Settlers settler = (Settlers)g.CreateUnit(UnitType.Settlers, 42, 25, g.PlayerNumber(human))!;
			settler.MovesLeft = settler.Move;
			Sim.ClearTasks();
			return (g, human, settler);
		}

		private static void StepTo(Settlers settler, int dx, int dy)
		{
			settler.MoveTo(dx, dy);
			Sim.Settle();
		}

		// The defect, stated directly: arriving with nothing to do ends the assignment.
		[Fact]
		public void ASettlerThatArrivesWithNothingToDoLeavesAutoImprove()
		{
			var (_, _, settler) = AFinishedLandscape();
			settler.TestEnableAutoImprove();
			settler.Goto = new Point(43, 25);

			StepTo(settler, 1, 0);

			Assert.Equal(43, settler.X);
			Assert.False(settler.AutoImprove,
				"a settler still enrolled will be sent back to this square every turn");
		}

		// Re-picking on arrival must not fire mid-journey: Goto is still set on an
		// intermediate step, and a settler that re-targeted every tile it walked over would
		// never get anywhere.
		[Fact]
		public void AStepAlongTheWayIsNotAnArrival()
		{
			var (_, _, settler) = AFinishedLandscape();
			settler.TestEnableAutoImprove();
			settler.Goto = new Point(45, 25);

			StepTo(settler, 1, 0);          // now at 43, target is still 45

			Assert.Equal(new Point(45, 25), settler.Goto);
			Assert.True(settler.AutoImprove, "still travelling — the assignment is not over");
		}

		// A settler that arrives on something it CAN work must work it, not re-pick.
		// (Grassland road is instant — BuildRoad sets tile.Road rather than a countdown.)
		[Fact]
		public void ArrivingOnRealWorkStillDoesTheWork()
		{
			var (_, _, settler) = AFinishedLandscape();
			Map.Instance[43, 25].Road = false;
			settler.TestEnableAutoImprove();
			settler.Goto = new Point(43, 25);

			StepTo(settler, 1, 0);

			Assert.True(Map.Instance[43, 25].Road, "it should have built the road it came for");
		}

		// Work left elsewhere: the settler takes a new target rather than ending the
		// assignment. The re-pick is a continuation, not a resignation.
		[Fact]
		public void WorkLeftElsewhereBecomesTheNextTarget()
		{
			var (_, _, settler) = AFinishedLandscape();
			// Strip the roads back off the whole city radius. The per-city budget is Size+1
			// tiles, so leaving exactly one job undone can fall outside the scan.
			for (int y = 23; y <= 27; y++)
			for (int x = 38; x <= 42; x++)
				if (Map.Instance[x, y]?.City is null) Map.Instance[x, y].Road = false;

			settler.TestEnableAutoImprove();
			settler.Goto = new Point(43, 25);

			StepTo(settler, 1, 0);

			Assert.True(settler.AutoImprove);
			Assert.False(settler.Goto.IsEmpty, "there is work left — it should be on its way to it");
			Assert.True(Common.DistanceToTile(40, 25, settler.Goto.X, settler.Goto.Y) <= 2);
		}
	}
}
