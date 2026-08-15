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
	// The Fighter's replacement, and the Fighter's ObsoleteTech points at Robotics to say
	// so: longer legs, cheaper, and it sees two tiles instead of one because seeing is most
	// of what it is for.
	//
	// No pilot, so no war weariness — City.ComputeCitizens bills a Republic or Democracy
	// for every unit in the field, and there is nobody aboard this one for anyone at home
	// to worry about.
	internal class ReaperDrone : BaseUnitAir
	{
		private static readonly string[] _page1 =
		{
			"A REAPER DRONE loiters where a",
			"fighter would have to turn back.",
			"",
			"It watches two tiles out, as any",
			"aircraft does, and costs less than",
			"the fighter it replaces.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROBOTICS, and makes the",
			"FIGHTER obsolete.",
			"",
			"There is no one aboard, so its",
			"absence troubles no one at home.",
			"",
			"SAM BATTERIES blunt it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		// No Explore override: the two-tile look asked for is inherited from BaseUnitAir,
		// where it now lives for every aircraft. It was written out three times identically
		// — Fighter, Bomber, Nuclear — and this would have been a fourth. So the drone sees
		// exactly as far as the fighter it replaces; its edge is range, price, and having
		// nobody aboard.

		public ReaperDrone() : base(5, 4, 2, 14)
		{
			Type = UnitType.ReaperDrone;
			Name = "Reaper Drone";
			RequiredTech = new Robotics();
			ObsoleteTech = null;
			SetIcon('A', 1, 1);   // ponytail: wears the Fighter sprite until bespoke art exists
		}
	}
}
