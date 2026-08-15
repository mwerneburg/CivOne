// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Units
{
	// A one-way conventional strike, and the answer to a nuclear arsenal nobody wants to
	// use: the Nuclear costs 160 shields, irradiates the ground it lands on and can wake
	// worse things than warming, so it sits in the silo. This costs 40, hits as hard as a
	// bomber, leaves nothing behind, and is gone afterwards either way.
	//
	// It is an AIR unit, which buys two of its properties for free: cargo capacity counts
	// only land units (BaseUnitLand.cs:292), so a missile rides any warship without
	// displacing troops, and City.ComputeCitizens can exempt it from war weariness with
	// every other unmanned thing.
	//
	// Expendable: consumed by its own attack whether it wins or loses. See
	// BaseUnit.Confront.
	internal class CruiseMissile : BaseUnitAir
	{
		private static readonly string[] _page1 =
		{
			"A CRUISE MISSILE flies a bomber's",
			"mission at a third of the price,",
			"and does not come home.",
			"",
			"It launches from a city or from",
			"any warship, and takes no cargo",
			"space aboard.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROCKETRY.",
			"",
			"Set a destination and it flies",
			"there on its own. The strike",
			"consumes the missile — hit or",
			"miss.",
			"",
			"SAM BATTERIES blunt it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		// True for anything that does not survive its own attack. Read by BaseUnit.Confront.
		internal static bool IsExpendable(IUnit unit) => unit is CruiseMissile;

		// It never returns, so it never refuels — but it must not evaporate while it waits
		// in a silo or on a deck. BaseUnitAir crashes an air unit that ends a turn out of
		// fuel anywhere but a city or a Carrier; a missile parked on a Frigate is neither,
		// and would have been lost at anchor.
		public override void NewTurn()
		{
			if (Tile is not null && (Tile.City is not null
			    || Tile.Units.Any(u => u.Owner == Owner && u.Class == UnitClass.Water)))
			{
				FuelLeft = TotalFuel;
				MovesLeft = Move;
				return;
			}
			base.NewTurn();
		}

		public CruiseMissile() : base(4, 12, 0, 8)
		{
			Type = UnitType.CruiseMissile;
			Name = "Cruise Missile";
			RequiredTech = new Rocketry();
			ObsoleteTech = null;
			SetIcon('D', 0, 0);   // ponytail: wears the Nuclear sprite until bespoke art exists
		}
	}
}
