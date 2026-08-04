// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Drawing;
using CivOne.Enums;

namespace CivOne.Events
{
	public delegate void ScreenEventHandler(object sender, ScreenEventArgs args);

	public class ScreenEventArgs : EventArgs
	{
		public bool Handled { get; set; }
		public int X { get; private set; }
		public int Y { get; private set; }
		public MouseButton Buttons { get; private set; }
		// Modifier and the wheel deltas are populated by the SDL mouse-wheel path
		// (Window.MouseEvent.cs:HandleMouseWheel). Other constructors leave them
		// at zero / None, so non-wheel mouse events behave exactly as before.
		//
		// WheelDeltaX carries the horizontal axis a two-finger trackpad swipe produces.
		// Anything that rebuilds a ScreenEventArgs from an existing one must copy BOTH
		// deltas and the modifier across, or the field silently reads zero downstream —
		// see the Rebuild helper below, which exists so that cannot be forgotten again.
		public KeyModifier Modifier { get; private set; }
		public int WheelDelta { get; private set; }
		public int WheelDeltaX { get; private set; }

		public Point Location => new Point(X, Y);

		public ScreenEventArgs(int x, int y)
		{
			X = x;
			Y = y;
			Buttons = MouseButton.None;
			Modifier = KeyModifier.None;
			WheelDelta = 0;
		}

		public ScreenEventArgs(int x, int y, MouseButton buttons)
		{
			X = x;
			Y = y;
			Buttons = buttons;
			Modifier = KeyModifier.None;
			WheelDelta = 0;
		}

		public ScreenEventArgs(int x, int y, MouseButton buttons, KeyModifier modifier, int wheelDelta, int wheelDeltaX = 0)
		{
			X = x;
			Y = y;
			Buttons = buttons;
			Modifier = modifier;
			WheelDelta = wheelDelta;
			WheelDeltaX = wheelDeltaX;
		}

		// The same event at different coordinates. Every re-wrap in the codebase is a
		// coordinate rewrite — window pixels to canvas, canvas to panel-local — and each one
		// used to be written out longhand, which is how the modifier and the wheel deltas got
		// dropped: GameWindow.Transform and BaseScreen.MouseArgsOffset both rebuilt with the
		// two- and three-argument constructors, so by the time a wheel event reached a screen
		// it carried neither Ctrl nor a delta and Ctrl+wheel zoom could not fire at all.
		public ScreenEventArgs Moved(int x, int y)
			=> new ScreenEventArgs(x, y, Buttons, Modifier, WheelDelta, WheelDeltaX) { Handled = Handled };
	}
}