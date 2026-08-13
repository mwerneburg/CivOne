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
	// The ground half of the colony programme, and the reason a colony can be lost without
	// anybody going to Alpha Centauri to take it.
	//
	// A colony twenty turns old is not self-sufficient — it is being resupplied, and the
	// resupply is run from one building on Earth. That makes a spaceship victory something
	// rivals can reach: before this existed, nothing any other civilization did could touch a
	// ship already under way. Now there is a city on the map to defend, and a city to take.
	//
	// ONE PER CIVILIZATION, on the Palace's terms (City.cs, production completion): building a
	// second moves the first rather than duplicating it. A programme with two flight directors
	// is not a programme, but a civ that loses the city should be able to rebuild elsewhere —
	// at the cost of the shields and whatever the interruption did to the schedule.
	internal class MissionControl : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"MISSION CONTROL runs the colony",
			"programme from the ground.",
			"",
			"One per civilization. Building a",
			"second one MOVES it.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SPACE FLIGHT.",
			"",
			"A colony beyond the solar system",
			"depends on the city that flies it.",
			"Lose this city and the colony is",
			"on its own.",
			"",
			"Choose a city that can be defended.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MissionControl() : base(16, 3)
		{
			Name = "Mission Control";
			RequiredTech = new SpaceFlight();
			SetIcon(0, 1, true);
			SetSmallIcon(0, 2);
			Type = Building.MissionControl;
		}
	}
}
