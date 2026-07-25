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
	internal class Navigation : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"NAVIGATION lets a ship find its",
			"way out of sight of land.",
			"",
			"Allows the SAIL, the SHIPYARD and",
			"MAGELLAN'S EXPEDITION.",
		};

		private static readonly string[] _page2 =
		{
			"It retires the coast-bound",
			"TRIREME. From here the ocean is",
			"a road rather than a wall, and",
			"other continents come within",
			"reach.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Navigation() : base(6, 2, 2, Advance.MapMaking, Advance.Astronomy)
		{
			Name = "Navigation";
			Type = Advance.Navigation;
		}
	}
}