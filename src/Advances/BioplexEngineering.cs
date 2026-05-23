// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class BioplexEngineering : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"BIOPLEX ENGINEERING integrates",
			"multiple biological systems",
			"across species lines. Drawing on",
			"synthetic ecology, it creates",
			"living architectures that serve",
			"both human and alien inhabitants,",
			"advancing Planetary Stewardship.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.BioplexEngineering;
		public override string Name => "Bioplex Engineering";
		public BioplexEngineering() : base(Advance.SyntheticEcology) { }
	}
}
