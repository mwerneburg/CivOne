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
	internal class Geoplasticity : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"GEOPLASTICITY applies industrial",
			"earthmoving to reshape solid",
			"terrain at scale. Engineers can",
			"raise plains into hills or grind",
			"hills back down to level ground,",
			"reconfiguring elevation as policy",
			"demands.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.Geoplasticity;
		public override string Name => "Geoplasticity";
		public override bool AvailablePreContact => true;
		public Geoplasticity() : base(Advance.PlanetaryStewardship) { }
	}
}
