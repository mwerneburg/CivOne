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
	internal class Phalanx : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Musketeers;
		private static readonly string[] _page1 =
		{
			"A PHALANX is a wall of shields and",
			"long spears, the first true",
			"DEFENSIVE unit.",
			"",
			"Twice the defence of Militia for",
			"little more cost.",
		};

		private static readonly string[] _page2 =
		{
			"Requires BRONZE WORKING.",
			"Made obsolete by GUNPOWDER.",
			"",
			"FORTIFY them in your cities and",
			"behind CITY WALLS, where their",
			"defence is multiplied.",
			"",
			"They attack poorly; leave that to",
			"Legions and Chariots.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Phalanx() : base(2, 1, 2, 1)
		{
			Type = UnitType.Phalanx;
			Name = "Phalanx";
			RequiredTech = new BronzeWorking();
			ObsoleteTech = new Gunpowder();
			SetIcon('E', 0, 0);
		}
	}
}