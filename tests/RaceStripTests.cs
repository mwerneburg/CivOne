// CivOne tests
//
// The race strip in the left sidebar: who is winning the three paths you cannot read off
// the map. A rival 29 turns into a Cultural Ascendancy used to be visible only on F9 —
// reported from a real game at 1310 AD, where the Japanese had been climbing for 29 turns
// and nothing on the main screen said so.
//
// The selection lives on Game rather than in the draw call precisely so it can be tested
// here: a readout pinned only by its source text proves the string exists, not that it
// names the right civilization.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;

namespace CivOne.Tests
{
	public class RaceStripTests
	{
		// Three civilizations that each hold a city, because StreakLeader skips the destroyed
		// and a player with no city and no homeless settler IS destroyed. A fixture of ghosts
		// would report nobody leading anything and every assertion below would pass on a
		// vacuum.
		private static (Game game, Player human, Player a, Player b) AWorldOfThree()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player human = g.HumanPlayer;
			Player a = ps.First(p => p != human);
			Player b = ps.First(p => p != human && p != a);

			int x0 = 25;
			foreach (Player p in new[] { human, a, b })
			{
				p.Government = new Monarchy();
				Assert.NotNull(g.AddCity(p, (byte)(x0 - 25), x0, 25));
				x0 += 6;
			}
			Sim.ClearTasks();
			return (g, human, a, b);
		}

		// Nothing held, nothing reported — the strip is absent in an early game rather than
		// drawing three zeroes every turn, which would train the player to ignore the line
		// that matters. This is the state the fixture starts in, so it is also the proof that
		// the assertions below are reading something that changed.
		[Fact]
		public void NobodyLeadingIsReportedAsNobody()
		{
			(Game g, _, _, _) = AWorldOfThree();

			Assert.Null(g.StreakLeader(pr => pr.CultureStreak).Holder);
			Assert.Equal(0u, g.StreakLeader(pr => pr.EconStreak).Streak);
			Assert.Null(g.SpaceLeader().Holder);
		}

		[Fact]
		public void TheDeepestStreakLeads()
		{
			(Game g, Player human, Player a, Player b) = AWorldOfThree();

			human.Progress.CultureStreak = 4;
			a.Progress.CultureStreak = 29;
			b.Progress.CultureStreak = 11;

			var lead = g.StreakLeader(pr => pr.CultureStreak);
			Assert.Same(a, lead.Holder);
			Assert.Equal(29u, lead.Streak);
		}

		// Ties go to the human: a tie means they are not losing the race, and it is their
		// screen. Same rule the score report's banner uses.
		//
		// The human is player 1 and so is always reached FIRST, which means a plain "strictly
		// greater wins" already keeps them on a tie — the obvious version of this test passed
		// with the tie clause deleted. So the human is moved to a LATER slot here, where only
		// the clause itself can produce the right answer. Both orders are checked, because a
		// rule that happens to be true in the common case is exactly how this suite has been
		// fooled before.
		[Theory]
		[InlineData(false)]   // human reached first — iteration order agrees with the rule
		[InlineData(true)]    // human reached last — only the tie clause can save them
		public void ATieGoesToTheHuman(bool humanLast)
		{
			(Game g, Player human, Player a, Player b) = AWorldOfThree();

			if (humanLast)
			{
				// b sits after a in player order, so a would win on arrival alone.
				Assert.True(g.PlayerNumber(b) > g.PlayerNumber(a), "the fixture's player order has changed");
				g.HumanPlayer = b;
				human = b;
			}

			human.Progress.EconStreak = 12;
			a.Progress.EconStreak = 12;

			Assert.Same(human, g.StreakLeader(pr => pr.EconStreak).Holder);
		}

		// The two paths are read separately. A civilization deep into one of them must not
		// colour the other's line — they share a helper and a lambda is all that separates
		// them, which is exactly the kind of thing that gets wired up once and copied wrong.
		[Fact]
		public void TheTwoStreaksAreReadIndependently()
		{
			(Game g, _, Player a, Player b) = AWorldOfThree();

			a.Progress.CultureStreak = 30;
			b.Progress.EconStreak = 20;

			Assert.Same(a, g.StreakLeader(pr => pr.CultureStreak).Holder);
			Assert.Same(b, g.StreakLeader(pr => pr.EconStreak).Holder);
		}

		// A ship in flight is reported by the year it arrives, and the EARLIEST arrival is
		// the one that matters — second place at Alpha Centauri has proved nothing new.
		[Fact]
		public void TheEarliestArrivalLeadsTheSpaceRace()
		{
			(Game g, Player human, Player a, Player b) = AWorldOfThree();

			human.Progress.SpaceshipArrivalTurn = 520;
			a.Progress.SpaceshipArrivalTurn = 495;
			b.Progress.SpaceshipArrivalTurn = 0;      // never launched

			var space = g.SpaceLeader();
			Assert.Same(a, space.Holder);
			Assert.Equal(495, space.ArrivalTurn);
			Assert.Equal(0u, space.Streak);           // nothing has landed
		}

		// A standing colony outranks anything still in flight, whatever the arrival dates say.
		[Fact]
		public void AColonyOutranksAShipStillFlying()
		{
			(Game g, Player human, Player a, _) = AWorldOfThree();

			a.Progress.SpaceshipArrivalTurn = 495;    // in flight, arriving soon
			human.Progress.ColonyFounded = true;
			human.Progress.ColonyOrder = 1;
			human.Progress.DiasporaStreak = 6;

			var space = g.SpaceLeader();
			Assert.Same(human, space.Holder);
			Assert.Equal(0, space.ArrivalTurn);       // the number means the streak now
			Assert.Equal(6u, space.Streak);
		}

		// Among colonies the FIRST to land leads. Being first is the achievement; a latecomer
		// further into its Diaspora hold has not overtaken anybody.
		[Fact]
		public void TheFirstColonyLeadsNotTheDeepestHold()
		{
			(Game g, _, Player a, Player b) = AWorldOfThree();

			a.Progress.ColonyFounded = true;
			a.Progress.ColonyOrder = 1;
			a.Progress.DiasporaStreak = 2;

			b.Progress.ColonyFounded = true;
			b.Progress.ColonyOrder = 2;
			b.Progress.DiasporaStreak = 15;

			var space = g.SpaceLeader();
			Assert.Same(a, space.Holder);
			Assert.Equal(2u, space.Streak);
		}

		// ─── wiring ──────────────────────────────────────────────────────────
		// The panel needs a live screen to render, so the wiring is pinned at the source —
		// the same reason RivalStreakDisplayTests reads text. These check the three things
		// the selection tests above cannot see: that the strip is drawn at all, that it is
		// drawn AFTER the two readouts that share its bottom edge, and that it yields to them
		// rather than painting over them.
		private static string SideBarSource()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "GamePlayPanels", "SideBar.cs"));
		}

		[Fact]
		public void TheStripIsDrawnAfterTheReadoutsItStacksOn()
		{
			string src = SideBarSource();

			int info = src.IndexOf("DrawGameInfo(gameTick);\n\t\t\t\tDrawNotifications();");
			Assert.True(info > 0, "the sidebar draw order has been rewritten");
			Assert.Contains("DrawNotifications();\n\t\t\t\tDrawRaceStrip();", src);
		}

		[Fact]
		public void TheStripYieldsToTheHoverReadoutAndTheKingStrip()
		{
			string src = SideBarSource();

			// Both are anchored to the same bottom edge. The WLTK strip reports what it
			// ACTUALLY drew, not what it wanted — it gives lines back when the hover readout
			// is showing, and reading NotifPanelH instead would leave a gap or an overlap.
			Assert.Contains("_notifReserve = ph;", src);
			Assert.Contains("int bottom = _gameInfo.Height - _hoverReserve - _notifReserve;", src);
			// ...and it never grows up into the active unit's details.
			Assert.Contains("bottom - HeaderRoom", src);
			// The working dot — drawn in the bottom-left corner while another civ moves, which
			// is most of a turn — claims the same pixels as the strip's last line. DrawHoverInfo
			// zeroes the reserve, so the busy path has to claim it back AFTER that call.
			Assert.Contains("_hoverReserve = Math.Max(_hoverReserve, 9);", src);
		}

		[Fact]
		public void TheStripReadsTheSharedSelectors()
		{
			string src = SideBarSource();

			Assert.Contains("Game.StreakLeader(pr => pr.CultureStreak), Game.CultureHoldTurns", src);
			Assert.Contains("Game.StreakLeader(pr => pr.EconStreak), Game.EconomicHoldTurns", src);
			Assert.Contains("Game.SpaceLeader()", src);
		}
	}
}
