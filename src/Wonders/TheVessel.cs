// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Wonders
{
	// The organism did not come here to stay. It crashed, it waited out the ice, and once it
	// holds enough of a world to strip for parts it builds the thing it needs to leave.
	//
	// Only The Thing can raise it (Player.WonderAvailable), and only once it has assimilated
	// SPACE FLIGHT from someone — the organism does not invent, it inherits. A world that never
	// reaches for space never gives it the way out, and is stuck with it for good.
	//
	// Completion is the end of the faction: Game.ExecuteThingDeparture razes every Thing city.
	internal class TheVessel : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"THE VESSEL is not a ship. It is",
			"what the organism becomes when it",
			"has eaten enough to leave.",
			"",
			"Its cities are the material. They",
			"are consumed as it is built.",
		};

		private static readonly string[] _page2 =
		{
			"Built only by THE THING, and only",
			"once it has taken SPACE FLIGHT",
			"from someone who had it.",
			"",
			"When it is finished the organism",
			"departs, and nothing it held is",
			"left standing.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TheVessel() : base(50)
		{
			Name = "The Vessel";
			// No RequiredTech: the organism researches nothing. The Space Flight condition is
			// enforced in Player.WonderAvailable against what it has ASSIMILATED, which is a
			// different question from what it could research.
			RequiredTech = null;
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.TheVessel;
		}
	}
}
