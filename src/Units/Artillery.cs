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
	internal class Artillery : BaseUnitLand
	{
		private static readonly string[] _page1 =
		{
			"ARTILLERY is the heaviest gun a",
			"civilization can field.",
			"",
			"Its attack surpasses every other",
			"land unit, and CITY WALLS do not",
			"stop it.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROBOTICS.",
			"",
			"Defenceless in the open, so pair",
			"it with ARMOR or MECH. INF.",
			"",
			"Against a walled city held by",
			"modern infantry, artillery is",
			"usually the only practical way in.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Artillery() : base(6, 12, 2, 2)
		{
			Type = UnitType.Artillery;
			Name = "Artillery";
			RequiredTech = new Robotics();
			ObsoleteTech = null;
			SetIcon('B', 0, 0);
		}
	}
}