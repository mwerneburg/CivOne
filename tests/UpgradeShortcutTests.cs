// CivOne tests
//
// Manual upgrade had no keyboard shortcut, so every refit meant opening the orders menu.
// It is now 'u', shared with Unload.
//
// Sharing is safe because the two can never appear on the same unit — a hull carries cargo
// and cannot upgrade, an upgradeable unit carries nothing — but GameMap's 'U' case has to
// dispatch carefully. It used to read `(ActiveUnit as BaseUnitSea)!.Unload()` guarded only
// by `is IBoardable`, which held while every carrier was a ship. The Dirigible is
// IBoardable and is NOT a BaseUnitSea, so that cast yielded null and the call would throw;
// the `!` silenced the compiler rather than the crash.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.UserInterface;

namespace CivOne.Tests
{
	public class UpgradeShortcutTests
	{
		// A city WITH BARRACKS, and gold. CanUpgrade requires all three — the unit must stand
		// in one of its owner's cities, that city must have a Barracks, and the treasury must
		// cover the refit. The first draft of these tests put the unit in an open field and
		// got no upgrade order at all, which is correct behaviour and a useless fixture.
		private static (Game game, Player human) AGame()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			Game g = Game.Instance;
			Player human = g.HumanPlayer;
			human.Explore(40, 25, range: 5);
			City c = g.AddCity(human, 0, 40, 25)!;
			c.AddBuilding(new CivOne.Buildings.Barracks());
			human.Gold = 5000;
			Sim.ClearTasks();
			return (g, human);
		}

		// The reported gap: the order existed, the key did not.
		[Fact]
		public void TheUpgradeOrderCarriesTheUKey()
		{
			(Game g, Player human) = AGame();
			human.AddAdvance(new CivOne.Advances.Chivalry());
			IUnit cavalry = g.CreateUnit(UnitType.Cavalry, 40, 25, g.PlayerNumber(human))!;

			MenuItem<int>? upgrade = cavalry.MenuItems
				.FirstOrDefault(m => m is not null && m.Text.StartsWith("Upgrade to"));

			Assert.NotNull(upgrade);
			Assert.Equal("u", upgrade!.Shortcut);
		}

		// A carrier that is not a ship must not be cast to one. This is the shape of the
		// crash, pinned so the next non-sea carrier does not reintroduce it.
		[Fact]
		public void ACarrierNeedNotBeAShip()
		{
			(Game g, Player human) = AGame();
			IUnit airship = g.CreateUnit(UnitType.Dirigible, 40, 25, g.PlayerNumber(human))!;

			Assert.True(airship is IBoardable, "the dispatch guard keys off IBoardable");
			Assert.False(airship is BaseUnitSea, "...and this is why that guard is not enough");
		}

		// Nothing that carries cargo offers an upgrade, so the shared key is unambiguous.
		[Fact]
		public void NoCarrierAlsoOffersAnUpgrade()
		{
			(Game g, Player human) = AGame();
			foreach (UnitType t in new[] { UnitType.Transport, UnitType.Carrier, UnitType.Dirigible })
			{
				IUnit hull = g.CreateUnit(t, 40, 25, g.PlayerNumber(human))!;
				Assert.DoesNotContain(hull.MenuItems,
					m => m is not null && m.Text.StartsWith("Upgrade to"));
			}
		}
	}
}
