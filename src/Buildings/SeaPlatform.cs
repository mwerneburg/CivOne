// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class SeaPlatform : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A SEA PLATFORM anchors an offshore",
			"habitat above the continental",
			"shelf. Its aquaculture systems",
			"cultivate the surrounding waters,",
			"yielding one extra food unit from",
			"every ocean tile this city works.",
		};

		private static readonly string[] _page2 =
		{
			"Aquatic Colonization adapted",
			"gravitics to resist ocean pressure",
			"at depth. Early Sea Platforms were",
			"stationed above dead zones; their",
			"engineered kelp forests revived",
			"fisheries within a single decade.",
			"Cities followed once the ocean",
			"had already begun producing again.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public SeaPlatform() : base(10, 2)
		{
			Name = "Sea Platform";
			RequiredTech = new AquaticColonization();
			Type = Building.SeaPlatform;
		}
	}
}
