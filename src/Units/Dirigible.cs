// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.UserInterface;

namespace CivOne.Units
{
	// Cargo airship. Carries land units — Caravans above all — over anything: ocean,
	// mountains, and another civilization's sea tubes.
	//
	// It exists because a tube line can be cut and leave a continent's trade stranded. Sea
	// tubes are claimed by whoever lays them first (Common.TubeBarred), so a rival network
	// laid across your route is a wall you have no right to cross, and a caravan that cannot
	// reach a foreign city cannot trade with it. The dirigible is the answer that does not
	// require a war: go over.
	//
	// NOT a BaseUnitAir, deliberately. That class disbands a unit that ends a turn away from
	// a city or Carrier, which for a freighter means the cargo drowns with it — and the whole
	// point of this unit is the long crossing. It is UnitClass.Air for everything that
	// matters (it ignores terrain, it is exempt from zone of control, it is attacked as an
	// air unit) and simply carries no fuel rule. "Great or unlimited travel" was the ask.
	internal class Dirigible : BaseUnit, IBoardable
	{
		public int Cargo => 4;

		// Base.Role infers from type: IBoardable only reads as Transport for a BaseUnitSea,
		// and this is not one. Stated here so the AI's hull accounting and the unit lists see
		// a freighter rather than a land attacker.
		public override UnitRole Role => UnitRole.Transport;

		// ponytail: a near-copy of BaseUnitSea's MoveUnits/MovementStart/MovementDone carry.
		// Kept separate rather than extracted because BaseUnitSea's version also holds the
		// coastal auto-wake convenience, which is sea-only and load-bearing for the AI's
		// board/unload loop. Extract into a shared carrier if a THIRD hull type ever appears.
		private IEnumerable<IUnit> Manifest(ITile previousTile)
		{
			if (previousTile is null || !previousTile.Units.Any(u => u.Class == UnitClass.Land))
				yield break;

			IUnit[] aboard = previousTile.Units.Where(u => u.Class == UnitClass.Land).ToArray();
			// In a city, only what was deliberately put aboard: a city tile is full of units
			// that live there and are not going anywhere. Sentry is how a passenger says so,
			// exactly as it does for a Transport.
			if (previousTile.City is not null)
				aboard = aboard.Where(u => u.Sentry).ToArray();
			foreach (IUnit unit in aboard.Take(Cargo)) yield return unit;
		}

		protected override void MovementStart(ITile previousTile)
		{
			foreach (IUnit unit in Manifest(previousTile))
			{
				unit.Sentry = true;
				unit.Fortify = false;
			}
			base.MovementStart(previousTile);
		}

		protected override void MovementDone(ITile previousTile)
		{
			foreach (IUnit unit in Manifest(previousTile))
			{
				unit.X = X;
				unit.Y = Y;
			}
			base.MovementDone(previousTile);
		}

		// Set the cargo down. A land unit woken over open water would have nowhere to stand,
		// so this only does anything on ground the passengers can hold.
		public void Unload()
		{
			ITile here = Map[X, Y];
			if (here.IsOcean && here.City is null && !here.TransportTube) return;
			foreach (IUnit unit in here.Units.Where(u => u.Class == UnitClass.Land).Take(Cargo))
			{
				unit.Sentry = false;
				unit.MovesLeft = unit.Move;
			}
		}

		// It flies: every tile is a legal destination. Same answer BaseUnitAir gives, for the
		// same reason — this unit is only not a BaseUnitAir because of the fuel rule.
		protected override bool ValidMoveTarget(ITile tile) => tile is not null;

		// Altitude, as for any aircraft.
		public override void Explore() => Explore(2);

		public override IEnumerable<MenuItem<int>> MenuItems
		{
			get
			{
				yield return MenuNoOrders();
				yield return MenuWait();
				yield return MenuSentry();
				yield return MenuGoTo();
				// Unload is the whole reason a player flies one of these anywhere.
				if (!Map[X, Y].IsOcean || Map[X, Y].City is not null || Map[X, Y].TransportTube)
				{
					MenuItem<int> unload = MenuItem<int>.Create("Unload");
					unload.Shortcut = "u";
					unload.Selected += (s, a) => Unload();
					yield return unload;
				}
				if (Map[X, Y].City is not null)
				{
					yield return MenuHomeCity();
				}
				yield return null!; // separator
				yield return MenuDisbandUnit();
			}
		}

		private static readonly string[] _page1 =
		{
			"The DIRIGIBLE carries 4 land units",
			"over any terrain, and over any",
			"sea lane it does not own.",
			"",
			"It cannot fight at all.",
		};

		private static readonly string[] _page2 =
		{
			"Requires FLIGHT.",
			"",
			"A transport tube belongs to the",
			"civilization that laid it, and a",
			"caravan may not set foot on",
			"another nation's line. When the",
			"sea road is closed to you, the",
			"air above it is not.",
			"",
			"Unarmed. What it carries is lost",
			"with it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Dirigible() : base(5, 0, 1, 16)
		{
			Class = UnitClass.Air;
			Type = UnitType.Dirigible;
			Name = "Dirigible";
			RequiredTech = new Flight();
		}
	}
}
