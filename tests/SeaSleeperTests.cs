// CivOne tests
//
// One sleeping unit severed a tube line for a hundred turns.
//
// AI.cs sentries a unit that has nowhere to go, and the unit-selection loop in Game.cs skips
// anything sentried — so an AI unit that sentries never wakes. Harmless on land. On a sea
// tube it is a permanent wall: Common.Blocks makes a foreign unit impassable to a non-combat
// unit even at PEACE, so every caravan routed around the tile and stepped ashore instead,
// and the tile could not be entered to dismantle the tube either.
//
// Observed in game 3de868a5 at 1968 AD: an Olvir settler asleep on the Frankish
// trans-Atlantic tube two north and two east of Panama, with a Goto it never resumed.
//
// The hull test is the part that has to be right. A sentried land unit on ocean is also how
// cargo rides a Transport, and waking those would unload every fleet at sea.

using System.Drawing;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SeaSleeperTests
	{
		private static (Game game, Player ai, ITile tile) ATubeAtSea()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(40, 25, Terrain.Ocean);
			// Stated, not assumed: the generated map decides what (42,25) is, and the
			// on-land case below is meaningless if it happens to be sea.
			Map.Instance.ChangeTileType(42, 25, Terrain.Grassland1);
			ITile t = Map.Instance[40, 25];
			t.TransportTube = true;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			Sim.ClearTasks();
			return (g, ai, t);
		}

		// The reported case, and the fix: it wakes, and it keeps the orders it fell asleep on.
		[Fact]
		public void ASettlerAsleepOnASeaTubeIsWokenAndKeepsItsGoto()
		{
			(Game g, Player ai, _) = ATubeAtSea();
			IUnit u = g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(ai))!;
			u.Sentry = true;
			u.Goto = new Point(50, 30);

			AI.Instance(ai).WakeSeaSleepers();

			Assert.False(u.Sentry, "it is still asleep on the tube");
			Assert.Equal(new Point(50, 30), u.Goto);
		}

		// Cargo aboard a hull is the state this must NOT touch: AssignMission sentries the
		// passenger on purpose, and waking it mid-ocean unloads an invasion at sea.
		[Fact]
		public void CargoAboardATransportIsLeftAsleep()
		{
			(Game g, Player ai, _) = ATubeAtSea();
			byte own = g.PlayerNumber(ai);
			IUnit cargo = g.CreateUnit(UnitType.Settlers, 40, 25, own)!;
			cargo.Sentry = true;
			g.CreateUnit(UnitType.Transport, 40, 25, own);

			AI.Instance(ai).WakeSeaSleepers();

			Assert.True(cargo.Sentry, "the passenger was thrown overboard");
		}

		// On land, sentry is ordinary and stays ordinary — this is not a general anti-sentry
		// rule, and a fortified garrison must not be roused by it.
		[Fact]
		public void ASentriedUnitOnLandIsLeftAlone()
		{
			(Game g, Player ai, _) = ATubeAtSea();
			Assert.False(Map.Instance[42, 25].IsOcean, "fixture: (42,25) must be land");
			IUnit u = g.CreateUnit(UnitType.Settlers, 42, 25, g.PlayerNumber(ai))!;
			u.Sentry = true;

			AI.Instance(ai).WakeSeaSleepers();

			Assert.True(u.Sentry);
		}
	}
}
