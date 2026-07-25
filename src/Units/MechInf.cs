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
	internal class MechInf : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.FusionInf;

		private static readonly string[] _page1 =
		{
			"MECHANIZED INFANTRY ride to battle",
			"in armoured carriers.",
			"",
			"The finest defensive unit in the",
			"game, and fast enough to cross",
			"a country to reach trouble.",
		};

		private static readonly string[] _page2 =
		{
			"Requires LABOR UNION.",
			"",
			"Their speed matters as much as",
			"their armour: a few of them can",
			"garrison a whole frontier by",
			"moving between the cities that",
			"need them.",
			"",
			"Nothing renders them obsolete.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MechInf() : base(5, 6, 6, 3)
		{
			Type = UnitType.MechInf;
			Name = "Mech. Inf.";
			RequiredTech = new LaborUnion();
			ObsoleteTech = null;
			SetIcon('C', 0, 0);
		}
	}
}