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
	internal class Cavalry : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Knights;
		private static readonly string[] _page1 =
		{
			"CAVALRY are mounted scouts and",
			"raiders, fast but lightly armed.",
			"",
			"Useful early for exploring and for",
			"chasing down BARBARIANS in the",
			"open.",
		};

		private static readonly string[] _page2 =
		{
			"Requires HORSEBACK RIDING.",
			"Made obsolete by CONSCRIPTION.",
			"",
			"Do not send them against defended",
			"cities; their strength is reach,",
			"not force.",
			"",
			"CHIVALRY replaces them with the",
			"far heavier KNIGHTS.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Cavalry() : base(2, 2, 1, 2)
		{
			Type = UnitType.Cavalry;
			Name = "Cavalry";
			RequiredTech = new HorsebackRiding();
			ObsoleteTech = new Conscription();
			SetIcon('C', 1, 1);
		}
	}
}