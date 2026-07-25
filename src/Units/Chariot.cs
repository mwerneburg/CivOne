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
	internal class Chariot : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Knights;
		private static readonly string[] _page1 =
		{
			"CHARIOTS are the strongest early",
			"attackers, and swift with it.",
			"",
			"Two moves let them strike and",
			"withdraw, or reach a threatened",
			"border in time.",
		};

		private static readonly string[] _page2 =
		{
			"Requires THE WHEEL.",
			"Made obsolete by CHIVALRY.",
			"",
			"Costly for the age, and poor in",
			"defence. Keep them in the field",
			"and let Phalanxes hold the walls.",
			"",
			"They cannot cross mountains and",
			"rough ground quickly.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Chariot() : base(4, 4, 1, 2)
		{
			Type = UnitType.Chariot;
			Name = "Chariot";
			RequiredTech = new TheWheel();
			ObsoleteTech = new Chivalry();
			SetIcon('D', 0, 2);
		}
	}
}