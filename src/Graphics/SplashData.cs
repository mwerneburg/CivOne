// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Graphics
{
	// Raw decoded image data set by the runtime layer (e.g. from splash.png).
	// The Splash screen consumes this to scale and quantize at canvas resolution.
	public class SplashData
	{
		public readonly int Width;
		public readonly int Height;
		public readonly byte[] Rgba; // row-major RGBA, 4 bytes per pixel

		public SplashData(int width, int height, byte[] rgba)
		{
			Width = width;
			Height = height;
			Rgba = rgba;
		}
	}
}
