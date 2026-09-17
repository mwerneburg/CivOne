// CivOne tests
//
// An unarmed ship does not attack.
//
// BaseUnit.Confront refused combat for unarmed LAND units (UnarmedCaptureTests) and, since
// the Dirigible, unarmed aircraft (DirigibleTests.ItWillNotAttack). A ship with attack 0,
// moved by hand onto a foreign unit, still fought it at strength 0 and was sunk with
// whatever it carried. The guard now covers every unit with attack 0. The Sea Caravan keeps
// its own Confront (trade with a city, refuse anything else) and is not affected.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UnarmedVesselTests
	{
		[Theory]
		[InlineData((int)UnitType.Transport)]
		[InlineData((int)UnitType.Longboat)]
		[InlineData((int)UnitType.HydroEngineer)]
		public void AnUnarmedShipWillNotAttack(int type)
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			// Neither side is the human, so no combat screen waits on a click (see
			// UnarmedCaptureTests for why that matters headless).
			Player[] ps = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player mover = ps[0], target = ps[1];
			foreach (Player p in ps.Take(2)) p.Government = new Monarchy();
			mover.DeclareWar(target);

			IUnit ship = g.CreateUnit((UnitType)type, 40, 25, g.PlayerNumber(mover))!;
			IUnit trireme = g.CreateUnit(UnitType.Trireme, 41, 25, g.PlayerNumber(target))!;
			Assert.Equal(0, ship.Attack);   // scenario: it is unarmed
			Sim.ClearTasks();

			ship.MoveTo(1, 0);
			Sim.Settle();

			Assert.Contains(ship, g.GetUnits());
			Assert.Contains(trireme, g.GetUnits());
			Assert.Equal((40, 25), (ship.X, ship.Y));
		}
	}
}
