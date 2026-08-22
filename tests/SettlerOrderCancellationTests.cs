// CivOne tests
//
// Settlers that refuse to change orders.
//
// RoadTo, AutoClean and AutoImprove are standing modes: each re-issues Goto from NewTurn
// every turn for as long as it is set, and nothing the player could do cancelled one. A new
// destination was overwritten that same turn and the settler walked back; Sentry, Fortify
// and "No Orders" never touched the flags; and the menu HID the entry that switched a mode
// on once it was on, so there was no off switch to find either. The documented exits are
// arriving at RoadTo — never, if the target sits across water — and running the world clean
// of pollution and unimproved tiles, which an industrial empire never does.
//
// Reported as settlers that "refuse to change orders, and will try to resume an old GoTo no
// matter what", one of which had to be disbanded.
//
// MovementDone:311 already describes the narrow version of this — "the settler that keeps
// returning to the same square to blink at it" — and fixes only that case.

using System.Drawing;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SettlerOrderCancellationTests
	{
		// Plains around a city, so auto-improve has road work to find under Despotism (its
		// policy allows only roads and rail before Monarchy) and pollution has somewhere to
		// sit within the radius the cleaner searches.
		private static (Game game, Player player, City city) AWorldWithACity()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Plains);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			p.Explore(40, 25, range: 20);
			City c = g.AddCity(p, 0, 40, 25)!;
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static Settlers ASettlerAt(Game g, Player p, int x, int y)
			=> (Settlers)g.CreateUnit(UnitType.Settlers, x, y, g.PlayerNumber(p))!;

		private static readonly Point Elsewhere = new Point(30, 20);

		// ── the three standing modes, each given a new destination ───────────────
		//
		// One test per mode, because they are three independent re-issue sites in NewTurn and
		// a fix that caught two of them would leave the report half-true.

		[Fact]
		public void ASettlerBuildingARoadObeysANewDestination()
		{
			var (g, p, _) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);
			Map.Instance[41, 25].Road = true;      // nothing to build here, so NewTurn re-routes
			s.RoadTo = new Point(55, 25);

			s.CancelAutomation();                  // what the player's new order now does
			s.Goto = Elsewhere;
			s.NewTurn();

			Assert.Equal(Elsewhere, s.Goto);
		}

		[Fact]
		public void AnAutoCleaningSettlerObeysANewDestination()
		{
			var (g, p, c) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);
			Map.Instance[42, 26].Pollution = true;
			s.AutoClean = true;
			// The scenario is only meaningful if there is a target to be dragged back to.
			Assert.True(s.Automated, "scenario: the settler is running a standing mode");

			s.CancelAutomation();
			s.Goto = Elsewhere;
			s.NewTurn();

			Assert.Equal(Elsewhere, s.Goto);
		}

		[Fact]
		public void AnAutoImprovingSettlerObeysANewDestination()
		{
			var (g, p, _) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);
			s.TestEnableAutoImprove();

			s.CancelAutomation();
			s.Goto = Elsewhere;
			s.NewTurn();

			Assert.Equal(Elsewhere, s.Goto);
		}

		// ── the mechanism the three tests above depend on ────────────────────────

		// Without this, the tests above would pass just as well against a NewTurn that had
		// been taught to leave a non-empty Goto alone — which is a DIFFERENT fix, and a worse
		// one: it defers the standing order instead of cancelling it, so the settler resumes
		// its obsession the moment it arrives.
		[Fact]
		public void AStandingModeStillOverwritesAGotoItWasNotAskedToCancel()
		{
			var (g, p, _) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);
			Map.Instance[41, 25].Road = true;
			s.RoadTo = new Point(55, 25);

			s.Goto = Elsewhere;
			s.NewTurn();

			Assert.Equal(new Point(55, 25), s.Goto);
		}

		[Fact]
		public void CancelAutomationClearsAllThreeModesTogether()
		{
			var (g, p, _) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);
			s.RoadTo = new Point(55, 25);
			s.AutoClean = true;
			s.TestEnableAutoImprove();

			s.CancelAutomation();

			Assert.True(s.RoadTo.IsEmpty, "still building a road somewhere");
			Assert.False(s.AutoClean, "still auto-cleaning");
			Assert.False(s.AutoImprove, "still auto-improving");
			Assert.False(s.Automated);
		}

		// ── the off switch ───────────────────────────────────────────────────────

		// Each mode's own menu entry is hidden while that mode runs, so before this there was
		// no menu path back to a settler that takes orders. This is the half of the report
		// that ended in a disbanded unit.
		[Fact]
		public void TheMenuOffersCancelOnlyWhileTheSettlerIsAutomated()
		{
			var (g, p, _) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);

			bool Offered() => s.MenuItems.Any(m => m?.Text is not null && m.Text.Contains("Cancel Automation"));

			Assert.False(Offered(), "offered to a settler that is taking orders already");

			s.AutoClean = true;
			Assert.True(Offered(), "an automated settler has no way back");

			s.CancelAutomation();
			Assert.False(Offered());
		}

		// The shortcut must not collide, or the entry is unreachable by keyboard and the
		// player is back to hunting through the menu.
		[Fact]
		public void TheCancelShortcutIsNotAlreadyTaken()
		{
			var (g, p, _) = AWorldWithACity();
			Settlers s = ASettlerAt(g, p, 41, 25);
			s.AutoClean = true;
			s.RoadTo = new Point(55, 25);

			var shortcuts = s.MenuItems.Where(m => m is not null)
				.Select(m => m.Shortcut).Where(x => !string.IsNullOrEmpty(x)).ToList();

			Assert.Equal(shortcuts.Count, shortcuts.Distinct().Count());
		}

		// ── the player-facing wiring ─────────────────────────────────────────────

		// Show.Goto builds a screen and hangs the order off its Closed event, which a headless
		// test cannot drive. Pinned at the source instead: the cancel must survive in the one
		// handler that turns a player's map click into a settler's destination.
		[Fact]
		public void TheGotoOrderCancelsAutomation()
		{
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(Sim.RepoRoot(), "src", "Tasks", "Show.cs"));
			int at = src.IndexOf("public static Show Goto");
			Assert.True(at > 0, "the Goto order has moved");
			string block = src.Substring(at, src.IndexOf("public static Show RoadTo", at) - at);

			Assert.Contains("CancelAutomation()", block);
		}
	}
}
