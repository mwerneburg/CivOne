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
	internal class Legion : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Musketeers;
		private static readonly string[] _page1 =
		{
			"The LEGION is the first unit built",
			"to ATTACK rather than endure.",
			"",
			"Disciplined swordsmen who can take",
			"a neighbour's city while it is",
			"still defended by Militia.",
		};

		private static readonly string[] _page2 =
		{
			"Requires IRON WORKING.",
			"Made obsolete by CONSCRIPTION.",
			"",
			"Legions are cheap enough to lose.",
			"Send several: one rarely takes a",
			"defended city alone.",
			"",
			"BARRACKS make them VETERANS, worth",
			"half again in battle.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Legion() : base(2, 3, 1, 1)
		{
			Type = UnitType.Legion;
			Name = "Legion";
			RequiredTech = new IronWorking();
			ObsoleteTech = new Conscription();
			SetIcon('E', 1, 0);
		}
	}
}