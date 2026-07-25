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
	internal class Cruiser : BaseUnitSea
	{
		private static readonly string[] _page1 =
		{
			"The CRUISER is a fast modern",
			"warship, strong enough to fight",
			"and quick enough to patrol.",
		};

		private static readonly string[] _page2 =
		{
			"Requires COMBUSTION.",
			"",
			"Cruisers hunt SUBMARINES and",
			"screen larger ships. They are the",
			"workhorse of a modern navy.",
			"",
			"A BATTLESHIP outguns them, but",
			"costs three times as much and",
			"cannot be everywhere.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Cruiser() : base(8, 6, 6, 6, 2)
		{
			Type = UnitType.Cruiser;
			Name = "Cruiser";
			RequiredTech = new Combustion();
			ObsoleteTech = null;
			SetIcon('C', 0, 1);
		}
	}
}