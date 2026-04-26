// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;

namespace CivOne.Screens
{
	// Screens tagged [OwnPalette] have explicitly set indices 1-17 themselves.
	// RuntimeHandler will NOT merge the nearest [Expand] screen's palette over
	// those indices, so the screen's own Cassette colors are preserved.
	public class OwnPalette : Attribute
	{
	}
}
