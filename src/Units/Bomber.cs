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
	internal class Bomber : BaseUnitAir
	{
		public override void Explore()
		{
			Explore(2);
		}
		
		private static readonly string[] _page1 =
		{
			"The BOMBER strikes ground and sea",
			"targets with heavy ordnance, far",
			"beyond your borders.",
			"",
			"It carries fuel for TWO turns",
			"before it must land.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ADVANCED FLIGHT.",
			"",
			"Bombers cannot take a city; they",
			"only empty it. Send ARMOR or",
			"infantry behind them.",
			"",
			"Watch the fuel. A bomber caught",
			"far from any city or CARRIER at",
			"the end of its second turn is",
			"lost with its crew.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Bomber() : base(12, 12, 1, 8)
		{
			Type = UnitType.Bomber;
			Name = "Bomber";
			RequiredTech = new AdvancedFlight();
			ObsoleteTech = null;
			SetIcon('A', 1, 2);

			TotalFuel *= 2;
			FuelLeft = TotalFuel;
		}
	}
}