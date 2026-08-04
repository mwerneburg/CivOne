// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Enums;
using CivOne.Events;

namespace CivOne
{
	internal static partial class SDL
	{
		internal abstract partial class Window
		{
			protected ScreenEventHandler OnMouseMove, OnMouseUp, OnMouseDown, OnMouseWheel;

			// SDL flips wheel events when the OS-level natural-scroll setting is on; this
			// constant matches SDL_MOUSEWHEEL_FLIPPED in SDL2 headers.
			private const uint SDL_MOUSEWHEEL_FLIPPED = 1;

			private readonly bool[] _mouseButtonState = new bool[3];
			protected int MouseX { get; private set; }
			protected int MouseY { get; private set; }

			private bool _cursorVisible = true;
			protected bool CursorVisible
			{
				get => _cursorVisible;
				set
				{
					if (value == _cursorVisible) return;
					_cursorVisible = value;
					SDL_ShowCursor(_cursorVisible ? 1 : 0);
				}
			}

			private void CheckMouseButton(MouseButton button, uint buttonMask, int mask)
			{
				bool state = (buttonMask & mask) > 0;
				if (_mouseButtonState[(int)button] == state) return;
				_mouseButtonState[(int)button] = state;
				if (state)
				{
					OnMouseDown?.Invoke(this, new ScreenEventArgs(MouseX, MouseY, button));
				}
				else
				{
					OnMouseUp?.Invoke(this, new ScreenEventArgs(MouseX, MouseY, button));
				}
			}

			private void HandleMouse()
			{
				uint buttonMask = SDL_GetMouseState(out int x, out int y);
				if (MouseX != x || MouseY != y)
				{
					MouseX = x;
					MouseY = y;
					MouseButton buttons = MouseButton.None;
					if ((buttonMask & 1) > 0) buttons |= MouseButton.Left;
					if ((buttonMask & 4) > 0) buttons |= MouseButton.Right;
					OnMouseMove?.Invoke(null, new ScreenEventArgs(x, y, buttons));
				}

				CheckMouseButton(MouseButton.Left, buttonMask, 1);
				CheckMouseButton(MouseButton.Right, buttonMask, 4);
			}

			// Dispatched by Window.cs:HandleEvent on SDL_MOUSEWHEEL. Captures both wheel
			// axes, modifier keys (for Ctrl+wheel zoom), and current mouse position
			// (for cursor-focused zoom). The deltas are the scroll amounts; we negate both
			// when the SDL_MOUSEWHEEL_FLIPPED flag is set so up always means "zoom in".
			//
			// Horizontal sign convention: X11 negates horizontal ticks before SDL sees them
			// and Windows' WM_MOUSEHWHEEL is already positive-right, so left = negative and
			// right = positive on both without per-platform correction. The user's OS-level
			// "natural scrolling" setting flips both axes and is deliberately left alone.
			private void HandleMouseWheel(SDL_MouseWheelEvent ev)
			{
				_ = SDL_GetMouseState(out int x, out int y);
				MouseX = x;
				MouseY = y;

				int wheelDelta = ev.Y;
				int wheelDeltaX = ev.X;
				if (ev.Direction == SDL_MOUSEWHEEL_FLIPPED)
				{
					wheelDelta = -wheelDelta;
					wheelDeltaX = -wheelDeltaX;
				}

				KeyModifier modifier = ConvertModifier(SDL_GetModState());
				OnMouseWheel?.Invoke(this, new ScreenEventArgs(x, y, MouseButton.None, modifier, wheelDelta, wheelDeltaX));
			}

			// Amount the fingers must spread or pinch, as a fraction of the screen diagonal,
			// before one zoom step fires. SDL reports DDist in small fractional increments per
			// event, so they accumulate until they cross this threshold.
			private const float PinchZoomStepThreshold = 0.02f;
			private float _pinchZoomAccumulator;

			// A pinch becomes a synthetic Ctrl+wheel event centred on the gesture, which
			// reuses the map's existing cursor-focused zoom path unchanged.
			//
			// SDL2 derives multi-gesture events from touch events only. macOS reports trackpad
			// gestures as touch, so this fires there; Windows translates touchpad pinch into
			// Ctrl+wheel itself, which lands in the normal path anyway. On X11 a touchpad is a
			// pointer device with no touch class, and SDL2 does not implement Wayland's
			// pointer-gestures protocol, so this handler is simply never called on Linux —
			// Ctrl + two-finger scroll is the intended fallback there. Do not work around it.
			private void HandleMultiGesture(SDL_MultiGestureEvent ev)
			{
				if (ev.NumFingers < 2) return;

				_pinchZoomAccumulator += ev.DDist;
				while (Math.Abs(_pinchZoomAccumulator) >= PinchZoomStepThreshold)
				{
					int wheelDelta = _pinchZoomAccumulator > 0 ? 1 : -1;
					_pinchZoomAccumulator -= wheelDelta * PinchZoomStepThreshold;

					int pixelX = (int)(ev.X * Width);
					int pixelY = (int)(ev.Y * Height);
					OnMouseWheel?.Invoke(this, new ScreenEventArgs(pixelX, pixelY, MouseButton.None, KeyModifier.Control, wheelDelta));
				}
			}
		}
	}
}