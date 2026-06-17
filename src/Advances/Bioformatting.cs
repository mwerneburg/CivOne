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
	internal class Bioformatting : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"BIOFORMATTING templates entire",
			"biomes from seed stock and",
			"directed growth fields. Barren",
			"ground can be planted with",
			"forest, mature woodland coaxed",
			"into jungle, and frozen tundra",
			"thawed into productive",
			"grassland.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.Bioformatting;
		public override string Name => "Bioformatting";
		public override bool AvailablePreContact => true;
		public Bioformatting() : base(Advance.PlanetaryStewardship) { }
	}
}
