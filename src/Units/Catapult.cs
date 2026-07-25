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
	internal class Catapult : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Cannon;
		private static readonly string[] _page1 =
		{
			"The CATAPULT is a SIEGE ENGINE,",
			"built to break cities rather than",
			"fight armies.",
			"",
			"Powerful in attack and nearly",
			"helpless in defence.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MATHEMATICS.",
			"Made obsolete by METALLURGY.",
			"",
			"Slow, and lost the moment it is",
			"caught in the open. Move it with",
			"an escort and strike from an",
			"adjacent tile.",
			"",
			"It is the first answer to a walled",
			"city.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Catapult() : base(4, 6, 1, 1)
		{
			Type = UnitType.Catapult;
			Name = "Catapult";
			RequiredTech = new Mathematics();
			ObsoleteTech = new Metallurgy();
			SetIcon('B', 0, 2);
		}
	}
}