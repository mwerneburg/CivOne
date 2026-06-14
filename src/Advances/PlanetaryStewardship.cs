#nullable enable
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
	internal class PlanetaryStewardship : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"PLANETARY STEWARDSHIP is the art",
			"of managing a world as a living",
			"system. Built on Bioplex",
			"Engineering and Canopy",
			"Cultivation, it treats every",
			"ecosystem as infrastructure to",
			"be maintained for all who share",
			"the planet.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.PlanetaryStewardship;
		public override string Name => "Planetary Stewardship";
		public override bool AvailablePreContact => true;
		public PlanetaryStewardship() : base(Advance.BioplexEngineering, Advance.CanopyCultivation) { }
	}
}
