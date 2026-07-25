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
	[Default]
	internal class Riflemen : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.MechInf;
		private static readonly string[] _page1 =
		{
			"RIFLEMEN are conscripted soldiers",
			"with rifled arms, the backbone of",
			"a modern defence.",
			"",
			"Stronger in defence than anything",
			"before the machine age.",
		};

		private static readonly string[] _page2 =
		{
			"Requires CONSCRIPTION.",
			"",
			"Conscription retires MUSKETEERS,",
			"CAVALRY and LEGIONS together: one",
			"advance sweeps away the old army.",
			"",
			"Behind CITY WALLS, fortified",
			"Riflemen are very hard to shift",
			"without ARTILLERY.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Riflemen() : base(3, 3, 5, 1)
		{
			Type = UnitType.Riflemen;
			Name = "Riflemen";
			RequiredTech = new Conscription();
			ObsoleteTech = null;
			SetIcon('D', 1, 2);
		}
	}
}