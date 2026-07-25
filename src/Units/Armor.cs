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
	internal class Armor : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.HoverTank;

		private static readonly string[] _page1 =
		{
			"ARMOR are tanks: heavy attack,",
			"sound defence and three moves.",
			"",
			"The decisive land unit of the",
			"modern age.",
		};

		private static readonly string[] _page2 =
		{
			"Requires THE AUTOMOBILE.",
			"",
			"Automobile also retires KNIGHTS.",
			"The change from horse to engine",
			"happens in a single advance.",
			"",
			"Armor can strike, take a city and",
			"hold it, which no earlier attacker",
			"managed well.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Armor() : base(8, 10, 5, 3)
		{
			Type = UnitType.Armor;
			Name = "Armor";
			RequiredTech = new Automobile();
			ObsoleteTech = null;
			SetIcon('D', 0, 1);
		}
	}
}