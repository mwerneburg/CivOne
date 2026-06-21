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
	// Advances in this group are hidden from the research menu until Game.VisitorsArrived
	// is true — i.e. until the aliens actually arrive (first contact), not merely when their
	// SETI signal is detected ~80 turns earlier. The gate is enforced in Player.AvailableResearch:
	// all prereqs may be met, but the advance still won't appear until contact. Subclasses must
	// implement GetPageText() for Civilopedia display; icons and page art are stubbed (no legacy assets).
	//
	// Exception: subclasses that override AvailablePreContact to true represent Earth-bound
	// science humanity can develop without the Olvir kick-start; they appear as normal
	// research once their prereqs are met, signal or not.
	internal abstract class BasePostContactAdvance : IAdvance
	{
		private readonly Advance[] _prereqs;

		public abstract byte Id { get; }
		public abstract string Name { get; }

		// True for advances reachable by ordinary human research before first contact.
		public virtual bool AvailablePreContact => false;

		public IAdvance[] RequiredTechs => _prereqs
			.Select(a => Common.Advances.FirstOrDefault(x => x.Id == (byte)a))
			.Where(a => a is not null)
			.ToArray();

		public Palette OriginalColours => null!;
		public IBitmap Icon => new Picture(112, 68);
		public byte PageCount => 1;
		public Picture DrawPage(byte pageNumber) => new Picture(320, 200);
		public virtual string[] GetPageText(byte pageNumber) => System.Array.Empty<string>();
		public bool Requires(byte id) => RequiredTechs.Any(t => t.Id == id);
		public bool Is<T>() where T : IAdvance => this is T;
		public bool Not<T>() where T : IAdvance => !(this is T);

		protected BasePostContactAdvance(params Advance[] prereqs)
		{
			_prereqs = prereqs;
		}
	}
}