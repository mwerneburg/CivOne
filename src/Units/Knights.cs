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
	internal class Knights : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Armor;
		private static readonly string[] _page1 =
		{
			"KNIGHTS are armoured horsemen who",
			"attack and defend well, and move",
			"twice each turn.",
			"",
			"For many centuries the finest",
			"unit a civilization can field.",
		};

		private static readonly string[] _page2 =
		{
			"Requires CHIVALRY.",
			"Made obsolete by THE AUTOMOBILE.",
			"",
			"Unlike earlier riders they can",
			"hold ground after taking it, which",
			"makes them a whole army in one",
			"unit.",
			"",
			"GUNPOWDER blunts them; MUSKETEERS",
			"behind walls will stop a charge.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Knights() : base(4, 4, 2, 2)
		{
			Type = UnitType.Knights;
			Name = "Knights";
			RequiredTech = new Chivalry();
			ObsoleteTech = new Automobile();
			SetIcon('E', 1, 1);
		}
	}
}