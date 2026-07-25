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
	internal class Cannon : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Artillery;
		private static readonly string[] _page1 =
		{
			"The CANNON is gunpowder artillery,",
			"successor to the CATAPULT.",
			"",
			"It batters down defences that",
			"muskets cannot touch.",
		};

		private static readonly string[] _page2 =
		{
			"Requires METALLURGY.",
			"Made obsolete by ROBOTICS.",
			"",
			"Still slow and still fragile.",
			"Escort it, and never leave it",
			"alone on open ground at the end",
			"of a turn.",
			"",
			"Transport ships carry siege trains",
			"to another continent.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Cannon() : base(4, 8, 1, 1)
		{
			Type = UnitType.Cannon;
			Name = "Cannon";
			RequiredTech = new Metallurgy();
			ObsoleteTech = new Robotics();
			SetIcon('B', 1, 2);
		}
	}
}