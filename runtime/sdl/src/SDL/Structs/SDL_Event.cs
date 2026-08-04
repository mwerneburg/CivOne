// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Runtime.InteropServices;

namespace CivOne
{
	internal static partial class SDL
	{
		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct SDL_Event
		{
			public SDL_EventType SDL_EventType;
			private fixed byte _nil[53 - sizeof(SDL_EventType)];
		}

		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct SDL_WindowEvent
		{
			public SDL_EventType SDL_EventType;
			public uint Timestamp;
			public uint WindowId;
			public SDL_WindowEventID Event;
			private fixed byte _nil[3];
			public int Data1;
			public int Data2;
		}

		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct SDL_KeyboardEvent
		{
			public SDL_EventType Type;
			public uint Timestamp;
			public uint WindowId;
			public SDL_KeyState State;
			public byte Repeat;
			private fixed byte _nil[2];
			internal SDL_Keysym KeySym;
		}

		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct SDL_MouseWheelEvent
		{
			public SDL_EventType Type;
			public uint Timestamp;
			public uint WindowId;
			public uint Which;
			public int X;
			public int Y;
			public uint Direction;
		}

		// Touchpad pinch/rotate. DDist is the fractional change in the distance between the
		// fingers since the previous event (positive = spreading apart); X/Y are the gesture
		// centroid in normalized (0..1) window coordinates.
		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct SDL_MultiGestureEvent
		{
			public SDL_EventType Type;
			public uint Timestamp;
			public long TouchId;
			public float DTheta;
			public float DDist;
			public float X;
			public float Y;
			public ushort NumFingers;
			public ushort Padding;
		}
	}
}