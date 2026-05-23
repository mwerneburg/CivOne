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
	internal class AquaticColonization : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"AQUATIC COLONIZATION applies",
			"gravitics to the deep ocean,",
			"allowing habitats to withstand",
			"crushing pressures and cultivate",
			"the seafloor. Enables the Sea",
			"Platform and advances toward",
			"Graviton Engineering.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.AquaticColonization;
		public override string Name => "Aquatic Colonization";
		public AquaticColonization() : base(Advance.Gravitics) { }
	}
}
