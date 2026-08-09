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

namespace CivOne.Units
{
	internal class Submarine : BaseUnitSea
	{
		private static readonly string[] _page1 =
		{
			"The SUBMARINE attacks from beneath",
			"the surface, with the heaviest",
			"torpedoes at sea.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MASS PRODUCTION.",
			"Needs OIL: +50% shields without.",
			"",
			"Deadly to transports and merchant",
			"shipping, and dangerous even to",
			"capital ships.",
			"",
			"Fragile once found. Keep it away",
			"from CRUISERS and from coasts",
			"where aircraft patrol.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Submarine() : base(5, 8, 2, 3, 2)
		{
			Type = UnitType.Submarine;
			Name = "Submarine";
			RequiredTech = new MassProduction();
			ObsoleteTech = null;
			SetIcon('C', 1, 2);
		}
	}
}