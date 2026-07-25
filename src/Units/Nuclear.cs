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
using CivOne.Wonders;

namespace CivOne.Units
{
	internal class Nuclear : BaseUnitAir
	{
		public override void Explore()
		{
			Explore(2);
		}
		
		private static readonly string[] _page1 =
		{
			"A NUCLEAR MISSILE destroys every",
			"unit on its target tile and around",
			"it, and halves the population of",
			"a city.",
			"",
			"The ground it touches is left",
			"POLLUTED.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROCKETRY and THE",
			"MANHATTAN PROJECT.",
			"",
			"The Manhattan Project lets EVERY",
			"civilization build these, not only",
			"the one that completed it.",
			"",
			"Fallout drives GLOBAL WARMING, and",
			"the world does not forgive it.",
			"Some say the blasts wake worse",
			"things than warming.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Nuclear() : base(16, 99, 0, 16)
		{
			Type = UnitType.Nuclear;
			Name = "Nuclear";
			RequiredTech = new Rocketry();
			RequiredWonder = new ManhattanProject();
			ObsoleteTech = null;
			SetIcon('D', 0, 0);
		}
	}
}