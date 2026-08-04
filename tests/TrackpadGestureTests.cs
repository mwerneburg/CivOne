// CivOne tests
//
// Ctrl+wheel zoom had never once fired in a running game. Three independent breaks, each
// sufficient on its own:
//
//   1. GameWindow.Transform rebuilt the event with `new ScreenEventArgs(x, y)`, dropping
//      Modifier and WheelDelta on the floor before any screen saw them. The ScaledFixed
//      branch below it then read args.WheelDelta from the already-stripped event.
//   2. BaseScreen.MouseArgsOffset did the same on the way down to a panel.
//   3. GamePlay had no MouseWheel override at all, so BaseScreen's `return false` ended the
//      dispatch and GameMap.MouseWheel — which implements the zoom — was unreachable.
//
// The fix is ScreenEventArgs.Moved: one clone-with-new-coordinates that carries everything,
// used by both re-wrap sites. These tests pin the carrying, since that is what silently
// regresses — a dropped field throws nothing, it just reads zero.

using CivOne;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Screens;
using CivOne.Screens.GamePlayPanels;

namespace CivOne.Tests
{
	public class TrackpadGestureTests
	{
		private static ScreenEventArgs AWheelEvent(int deltaY, int deltaX = 0, KeyModifier mod = KeyModifier.None)
			=> new ScreenEventArgs(160, 100, MouseButton.None, mod, deltaY, deltaX);

		// The defect, stated directly: a re-wrapped wheel event keeps its payload.
		[Fact]
		public void MovingAWheelEventKeepsItsPayload()
		{
			ScreenEventArgs args = AWheelEvent(deltaY: 1, deltaX: -3, mod: KeyModifier.Control);
			ScreenEventArgs moved = args.Moved(args.X - 80, args.Y - 8);

			Assert.Equal(80, moved.X);
			Assert.Equal(92, moved.Y);
			Assert.Equal(1, moved.WheelDelta);
			Assert.Equal(-3, moved.WheelDeltaX);
			Assert.Equal(KeyModifier.Control, moved.Modifier);
		}

		// Mouse events that are not wheel events must be unaffected — the old constructors
		// left these at zero/None and nothing downstream should start seeing values.
		[Fact]
		public void AnOrdinaryClickCarriesNoWheelPayload()
		{
			ScreenEventArgs click = new ScreenEventArgs(10, 20, MouseButton.Left);
			ScreenEventArgs moved = click.Moved(0, 12);

			Assert.Equal(MouseButton.Left, moved.Buttons);
			Assert.Equal(0, moved.WheelDelta);
			Assert.Equal(0, moved.WheelDeltaX);
			Assert.Equal(KeyModifier.None, moved.Modifier);
		}

		private static GameMap AMap()
		{
			Sim.NewGame(width: 80, height: 50);
			GameMap map = new GameMap();
			map.Resize(240, 192);
			map.CenterOnPoint(40, 25);
			return map;
		}

		// A plain vertical swipe scrolls; it must not be mistaken for a zoom request, or
		// two-finger scrolling would jump zoom levels instead of moving the view.
		[Fact]
		public void APlainVerticalSwipePansWithoutZooming()
		{
			GameMap map = AMap();
			int y = map.Y, zoom = Game.Instance.HumanPlayer.MapZoomBasisPoints;

			Assert.True(map.MouseWheel(AWheelEvent(deltaY: -1)));

			Assert.Equal(y + 1, map.Y);
			Assert.Equal(zoom, Game.Instance.HumanPlayer.MapZoomBasisPoints);
		}

		// The horizontal axis is the one that silently reads zero when a re-wrap drops it —
		// panning left/right looks like "trackpad support doesn't work" rather than a crash.
		[Fact]
		public void AHorizontalSwipePansAlongX()
		{
			GameMap map = AMap();
			int x = map.X, zoom = Game.Instance.HumanPlayer.MapZoomBasisPoints;

			Assert.True(map.MouseWheel(AWheelEvent(deltaY: 0, deltaX: 1)));

			Assert.Equal(x + 1, map.X);
			Assert.Equal(zoom, Game.Instance.HumanPlayer.MapZoomBasisPoints);
		}

		// X wraps with the world; the map has no left or right edge.
		[Fact]
		public void PanningWrapsAroundTheWorld()
		{
			GameMap map = AMap();
			map.CenterOnPoint(0, 25);
			int x = map.X;
			for (int i = 0; i <= x; i++) map.MouseWheel(AWheelEvent(deltaY: 0, deltaX: -1));

			Assert.Equal(Map.WIDTH - 1, map.X);
		}

		// Y clamps: there is nothing north of the north pole.
		[Fact]
		public void PanningStopsAtThePole()
		{
			GameMap map = AMap();
			for (int i = 0; i < 60; i++) map.MouseWheel(AWheelEvent(deltaY: 1));

			Assert.Equal(0, map.Y);
		}

		// Break 3, end to end: GamePlay had no MouseWheel override, so nothing below it ever
		// saw a wheel event. Driving the real screen is what pins the routing.
		[Fact]
		public void CtrlWheelOnTheGameScreenReachesTheMapAndZooms()
		{
			Sim.NewGame(width: 80, height: 50);
			GamePlay screen = new GamePlay();
			int before = Game.Instance.HumanPlayer.MapZoomBasisPoints;

			// Down, not up: the default 1000 basis points is already Presets[0], the maximum
			// zoom-in, so a zoom-in step is a legal no-op and would prove nothing.
			Assert.True(screen.MouseWheel(AWheelEvent(deltaY: -1, mod: KeyModifier.Control)),
				"a Ctrl+wheel over the map must be handled");
			Assert.NotEqual(before, Game.Instance.HumanPlayer.MapZoomBasisPoints);
		}

		// The same routing for a plain swipe: through the screen, past the side bar offset,
		// into the map. Break 2 lives on this path — MouseArgsOffset is what carries the
		// horizontal delta across the 80-pixel side-bar shift.
		[Fact]
		public void APlainSwipeOnTheGameScreenPansTheMap()
		{
			Sim.NewGame(width: 80, height: 50);
			GamePlay screen = new GamePlay();
			screen.CenterOnPoint(40, 25);
			int x = screen.X;

			Assert.True(screen.MouseWheel(AWheelEvent(deltaY: 0, deltaX: 1)));
			Assert.Equal(x + 1, screen.X);
		}

		// The menu bar owns the top eight rows; a wheel event up there is not the map's.
		[Fact]
		public void TheMenuBarDoesNotPanTheMap()
		{
			Sim.NewGame(width: 80, height: 50);
			GamePlay screen = new GamePlay();

			Assert.False(screen.MouseWheel(
				new ScreenEventArgs(160, 2, MouseButton.None, KeyModifier.None, 1)));
		}
	}
}
