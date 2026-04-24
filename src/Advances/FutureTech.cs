// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Graphics;

namespace CivOne.Advances
{
	internal class FutureTech : IAdvance
	{
		public byte Id => 255;
		public string Name => "Future Technology";
		public IAdvance[] RequiredTechs => new IAdvance[0];
		public Palette OriginalColours => null;
		public IBitmap Icon => new Picture(112, 68);
		public byte PageCount => 1;
		public Picture DrawPage(byte pageNumber) => new Picture(320, 200);
		public bool Requires(byte id) => false;
		public bool Is<T>() where T : IAdvance => this is T;
		public bool Not<T>() where T : IAdvance => !(this is T);
	}
}
