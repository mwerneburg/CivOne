// CivOne tests
//
// A refit at sea drowned the passengers.
//
// Reported from a crossing to Australia: a Frigate carrying a Settler and its escort became
// an Ironclad, and the two units aboard were gone. Nothing killed them on purpose — "aboard"
// in this game is not a property of the ship, it is a land unit standing on an ocean tile
// with enough berths under it. A Frigate carries four, an Ironclad carries none, so the free
// upgrade from the Nanobot Factory removed the deck and left them floating.
//
// UpgradeUnit now refuses a refit that would leave more land units on the tile than the new
// hull can carry, and the two free-upgrade appliers skip such a hull when choosing a target,
// so the turn's upgrade goes to something else instead of being silently spent on nothing.

using System.Linq;
using System.Reflection;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UpgradeCargoTests
	{
		// A stretch of open water with a coast well away from it, and a player who has the
		// Steam Engine — the gate on the Frigate → Ironclad rung.
		private static (Game game, Player player, byte num) AtSea()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 28; x <= 44; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			p.Explore(36, 25, range: 20);
			foreach (IAdvance a in new IAdvance[] { new Navigation(), new Magnetism(), new SteamEngine() })
				p.AddAdvance(a, false);
			Sim.ClearTasks();
			return (g, p, g.PlayerNumber(p));
		}

		private static void LoadTwo(Game g, byte num, int x, int y)
		{
			g.CreateUnit(UnitType.Settlers, x, y, num, false);
			g.CreateUnit(UnitType.Musketeers, x, y, num, false);
		}

		// The invariant the report is really about. The refit does not delete anybody — the
		// Settler and its escort are still in the unit list afterwards, which is why asserting
		// their existence proves nothing. What it does is take the deck out from under them:
		// two land units on open ocean with nought berths beneath. They cannot move (a land
		// unit at sea may only step ashore), the ship sails on without them because cargo
		// follows the hull only when the hull can carry it, and nothing will ever collect them.
		[Fact]
		public void NoPassengerIsLeftOnOpenWaterWithoutADeck()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			LoadTwo(g, num, 36, 25);

			g.UpgradeUnit(frigate, UnitType.Ironclad, 0);

			ITile tile = Map.Instance[36, 25];
			int aboard = tile.Units.Count(u => u.Class == UnitClass.Land);
			int berths = tile.Units.Where(u => u is IBoardable).Sum(u => ((IBoardable)u).Cargo);
			Assert.True(berths >= aboard,
				$"{aboard} land units at sea with {berths} berths under them");
		}

		// ...and the crossing the report was on actually continues: the hull keeps its cargo
		// and the cargo keeps up with the hull.
		[Fact]
		public void TheCrossingContinuesWithEveryoneAboard()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			IUnit settler = g.CreateUnit(UnitType.Settlers, 36, 25, num, false)!;
			IUnit escort = g.CreateUnit(UnitType.Musketeers, 36, 25, num, false)!;

			g.UpgradeUnit(frigate, UnitType.Ironclad, 0);

			// Whatever hull is on that tile now — the captured reference would be a ghost if
			// the refit had gone through, and a ghost Frigate happily carries its cargo.
			IUnit hull = Map.Instance[36, 25].Units.First(u => u.Class == UnitClass.Water);
			hull.MoveTo(1, 0);
			Sim.Settle();

			Assert.Equal(37, hull.X);
			Assert.Equal((37, 25), (settler.X, settler.Y));
			Assert.Equal((37, 25), (escort.X, escort.Y));
		}

		// ...and the ship they are standing on is still a ship that carries them. Removing the
		// passengers' rescue while still performing the refit would leave them on an ocean tile
		// with no hull, which is the same loss one turn later.
		[Fact]
		public void TheLoadedHullIsNotRefitted()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			LoadTwo(g, num, 36, 25);

			g.UpgradeUnit(frigate, UnitType.Ironclad, 0);

			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Frigate);
			Assert.DoesNotContain(g.GetUnits(), u => u.Owner == num && u is Ironclad);
		}

		// The guard is about berths, not about cargo being present at all: a refit INTO a
		// bigger hull carries everyone and must still happen.
		[Fact]
		public void ARefitIntoARoomierHullStillHappens()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			LoadTwo(g, num, 36, 25);

			g.UpgradeUnit(frigate, UnitType.Transport, 0);

			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Transport);
			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Settlers);
			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Musketeers);
		}

		// An empty Frigate has nobody to lose. Blocking every sea refit would "fix" the report
		// by disabling the rung.
		[Fact]
		public void AnEmptyFrigateAtSeaIsStillUpgraded()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;

			g.UpgradeUnit(frigate, UnitType.Ironclad, 0);

			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Ironclad);
			Assert.DoesNotContain(g.GetUnits(), u => u.Owner == num && u is Frigate);
		}

		// A second hull on the same tile with room to spare means nobody goes into the water,
		// so the refit is free to proceed — the count that matters is berths on the TILE.
		[Fact]
		public void ASecondHullWithRoomLetsTheRefitProceed()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			g.CreateUnit(UnitType.Transport, 36, 25, num, false);
			LoadTwo(g, num, 36, 25);

			g.UpgradeUnit(frigate, UnitType.Ironclad, 0);

			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Ironclad);
			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Settlers);
		}

		// In port the land units are garrisoned, not carried, so the harbour refit that Civ
		// players expect keeps working.
		[Fact]
		public void AFrigateInPortUpgradesWithTroopsPresent()
		{
			(Game g, Player p, byte num) = AtSea();
			Map.Instance.ChangeTileType(36, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			g.AddCity(p, 0, 36, 25);
			IUnit frigate = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			LoadTwo(g, num, 36, 25);

			g.UpgradeUnit(frigate, UnitType.Ironclad, 0);

			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Ironclad);
		}

		// End to end through the wonder that reported it. The factory must not spend its free
		// upgrade on the loaded hull and quietly achieve nothing: the empty Frigate is the one
		// that gets refitted.
		[Fact]
		public void TheFactoryRefitsTheEmptyFrigateAndLeavesTheLoadedOne()
		{
			(Game g, Player p, byte num) = AtSea();
			IUnit loaded = g.CreateUnit(UnitType.Frigate, 36, 25, num, false)!;
			LoadTwo(g, num, 36, 25);
			g.CreateUnit(UnitType.Frigate, 40, 28, num, false);
			Sim.ClearTasks();

			var apply = typeof(Game).GetMethod("ApplyNanobotUpgrades",
				BindingFlags.NonPublic | BindingFlags.Instance)!;
			apply.Invoke(g, new object[] { p });

			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Ironclad);
			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Settlers);
			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Musketeers);
			Assert.Contains(g.GetUnits(), u => u.Owner == num && u is Frigate && u.X == 36 && u.Y == 25);
		}
	}
}
