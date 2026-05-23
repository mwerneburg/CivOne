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
	internal class SyntheticEcology : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"SYNTHETIC ECOLOGY engineers living",
			"systems from first principles.",
			"Merging genetic science and",
			"recycling technology, it produces",
			"designed ecosystems that sustain",
			"themselves without intervention.",
			"Enables Bioplex Engineering and",
			"Canopy Cultivation.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.SyntheticEcology;
		public override string Name => "Synthetic Ecology";
		public SyntheticEcology() : base(Advance.GeneticEngineering, Advance.Recycling) { }
	}
}
