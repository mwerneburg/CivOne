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
	internal class Fighter : BaseUnitAir
	{
		private static readonly string[] _page1 =
		{
			"The FIGHTER is a fast aircraft",
			"that hunts other aircraft and",
			"strafes ground units.",
			"",
			"It must return to a CITY or",
			"CARRIER before its FUEL runs out.",
		};

		private static readonly string[] _page2 =
		{
			"Requires FLIGHT.",
			"Needs OIL: +50% shields without.",
			"",
			"Nothing else can intercept an",
			"enemy BOMBER, so a civilization",
			"without fighters has no defence",
			"in the air.",
			"",
			"Count the moves home before",
			"attacking. A fighter that runs",
			"out of fuel is lost.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Fighter() : base(6, 4, 2, 10)
		{
			Type = UnitType.Fighter;
			Name = "Fighter";
			RequiredTech = new Flight();
			// Retired by the Reaper Drone, which flies further, sees further and costs less.
			// Fighters already built serve out their lives; no new ones are offered.
			ObsoleteTech = new Robotics();
			SetIcon('A', 1, 1);
		}
	}
}