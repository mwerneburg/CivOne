// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

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

			// Dispatched by Window.cs:HandleEvent on SDL_MOUSEWHEEL. Captures wheel
			// delta, modifier keys (for Ctrl+wheel zoom), and current mouse position
			// (for cursor-focused zoom). The y delta is the scroll amount; we negate it
			// when the SDL_MOUSEWHEEL_FLIPPED flag is set so up always means "zoom in".
			private void HandleMouseWheel(SDL_MouseWheelEvent ev)
			{
				_ = SDL_GetMouseState(out int x, out int y);
				MouseX = x;
				MouseY = y;

				int wheelDelta = ev.Y;
				if (ev.Direction == SDL_MOUSEWHEEL_FLIPPED) wheelDelta = -wheelDelta;

				KeyModifier modifier = ConvertModifier(SDL_GetModState());
				OnMouseWheel?.Invoke(this, new ScreenEventArgs(x, y, MouseButton.None, modifier, wheelDelta));
			}
		}
	}
}