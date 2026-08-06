// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Units
{
	// A Scavenger extraction craft. Barbarian-owned and never producible, like the Gozira, the
	// Leviathan and the Henge Guardian — it belongs to nobody's empire and answers to nobody's
	// diplomacy.
	//
	// It is not here to fight. Attack 1 means it will not take your cities and cannot defend
	// itself well; the damage it does is to the map, not to your army. But it is tough enough
	// (defence 6) that an ancient civilization cannot simply walk out and switch it off, and
	// killing them is the ONLY counterplay — no harvesters, no extraction.
	//
	// Sits on land beside the water it is draining, which is what makes it reachable.
	internal class Harvester : BaseUnitLand
	{
		private static readonly string[] _page1 =
		{
			"A HARVESTER is a drinking straw",
			"with legs.",
			"",
			"It puts down beside water, empties",
			"it into orbit, and walks to the",
			"next lake.",
		};

		private static readonly string[] _page2 =
		{
			"It cannot be built and it cannot",
			"be reasoned with — it is machinery,",
			"not an enemy.",
			"",
			"Destroy them and the extraction",
			"stops. What has already gone does",
			"not come back.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Harvester() : base(30, 1, 6, 1)   // never priced for sale; attack 1, defense 6, move 1
		{
			Type = UnitType.Harvester;
			Name = "Harvester";
			RequiredTech = null;
			ObsoleteTech = null;
			SetIcon('D', 0, 1);   // ponytail: wears the Armor sprite until bespoke art exists
		}
	}
}
