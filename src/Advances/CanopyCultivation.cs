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
	internal class CanopyCultivation : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"CANOPY CULTIVATION engineers the",
			"upper strata of living ecosystems",
			"into productive zones. Drawing on",
			"synthetic ecology, it transforms",
			"forest canopies and orbital",
			"biospheres into food and resource",
			"sources. Advances Planetary",
			"Stewardship.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.CanopyCultivation;
		public override string Name => "Canopy Cultivation";
		public override bool AvailablePreContact => true;
		public CanopyCultivation() : base(Advance.SyntheticEcology) { }
	}
}
