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
	internal class Militia : BaseUnitLand
	{
		public override UnitType? UpgradesTo => UnitType.Phalanx;
		private static readonly string[] _page1 =
		{
			"MILITIA are townsfolk with spears",
			"and whatever else came to hand.",
			"",
			"Weak in every respect, but cheap",
			"enough that a young city can",
			"afford a garrison at once.",
		};

		private static readonly string[] _page2 =
		{
			"Requires no advance.",
			"",
			"Made obsolete by GUNPOWDER, after",
			"which cities build MUSKETEERS",
			"instead.",
			"",
			"A city with no unit inside is",
			"taken by the first enemy to walk",
			"in. Militia at least prevent that.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Militia() : base(1, 1, 1, 1)
		{
			Type = UnitType.Militia;
			Name = "Militia";
			RequiredTech = null;
			ObsoleteTech = new Gunpowder();
			SetIcon('C', 0, 2);
		}
	}
}