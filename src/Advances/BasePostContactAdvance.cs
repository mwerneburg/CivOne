// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Advances
{
	internal abstract class BasePostContactAdvance : IAdvance
	{
		private readonly Advance[] _prereqs;

		public abstract byte Id { get; }
		public abstract string Name { get; }

		public IAdvance[] RequiredTechs => _prereqs
			.Select(a => Common.Advances.FirstOrDefault(x => x.Id == (byte)a))
			.Where(a => a is not null)
			.ToArray();

		public Palette OriginalColours => null;
		public IBitmap Icon => new Picture(112, 68);
		public byte PageCount => 1;
		public Picture DrawPage(byte pageNumber) => new Picture(320, 200);
		public bool Requires(byte id) => RequiredTechs.Any(t => t.Id == id);
		public bool Is<T>() where T : IAdvance => this is T;
		public bool Not<T>() where T : IAdvance => !(this is T);

		protected BasePostContactAdvance(params Advance[] prereqs)
		{
			_prereqs = prereqs;
		}
	}
}
