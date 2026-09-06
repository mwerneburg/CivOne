// CivOne tests
//
// A tile with a unit on it drew as empty sea.
//
// TileExtensions.UnitsToPicture prefers a WATER unit on an ocean tile and otherwise skips
// any sentried LAND unit — correct for cargo asleep aboard a ship, where the boat is the
// thing to draw. Sea tubes broke the premise: a land unit can stand on ocean with no boat
// under it, and when every unit on the tile was a sentried land unit both lookups returned
// null and the tile rendered as open water.
//
// Reported from the 1968 AD end of game 3de868a5. An Olvir settler on sentry sat on a
// trans-Atlantic tube two north and two east of Panama for the rest of the game. Caravans
// would not path through it — a foreign unit blocks a non-combat unit's tile even at peace
// (Common.cs Blocks) — so they stepped ashore in open country instead, and the tile the
// player kept inspecting looked empty. The player's own caravans, sentried on the same tube
// line, disappeared from the map where they stood. Sixteen tiles in that save were occupied
// and invisible, holding 12 Olvir, 7 Frankish, 3 Mongol and 1 barbarian unit.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SeaTubeVisibilityTests
	{
		// An ocean tile carrying a tube, which is the only way a land unit stands on water
		// without a boat.
		private static (Game game, ITile tile) ATubeAtSea()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(40, 25, Terrain.Ocean);
			ITile t = Map.Instance[40, 25];
			t.TransportTube = true;
			Sim.ClearTasks();
			return (g, t);
		}

		// The reported case: one sleeping land unit, alone, on a sea tube.
		[Fact]
		public void ASentriedLandUnitOnASeaTubeIsDrawn()
		{
			(Game g, ITile t) = ATubeAtSea();
			IUnit u = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(g.HumanPlayer))!;
			u.Sentry = true;

			Assert.NotNull(t.UnitsToPicture());
		}

		// A whole stack of them, which is what the tube line out of Panama actually held —
		// three caravans on one tile, none of them drawn.
		[Fact]
		public void AStackOfSleepingLandUnitsOnASeaTubeIsDrawn()
		{
			(Game g, ITile t) = ATubeAtSea();
			for (int i = 0; i < 3; i++)
			{
				IUnit u = g.CreateUnit(UnitType.Caravan, 40, 25, g.PlayerNumber(g.HumanPlayer))!;
				u.Sentry = true;
			}

			Assert.NotNull(t.UnitsToPicture());
		}

		// ...and the rule the skip was written for still holds: with a boat present the boat
		// is what gets drawn, not the cargo asleep on its deck. This is the half that fails if
		// the fallback is written as "just draw units.First()".
		[Fact]
		public void CargoAsleepAboardAShipStillDefersToTheShip()
		{
			(Game g, ITile t) = ATubeAtSea();
			byte me = g.PlayerNumber(g.HumanPlayer);
			IUnit cargo = g.CreateUnit(UnitType.Caravan, 40, 25, me)!;
			cargo.Sentry = true;
			g.CreateUnit(UnitType.Transport, 40, 25, me);

			Assert.Contains(t.Units, x => x.Class == UnitClass.Water);
			Assert.NotNull(t.UnitsToPicture());
		}

		// An empty tile still draws nothing, or every open sea tile gains a phantom.
		[Fact]
		public void AnEmptySeaTileDrawsNothing()
		{
			(_, ITile t) = ATubeAtSea();

			Assert.Null(t.UnitsToPicture());
		}
	}
}
