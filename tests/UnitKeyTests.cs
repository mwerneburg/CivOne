// CivOne tests
//
// GameMap's keyboard switch is a hand-maintained list of letters, and the unit menus are a
// separate list of shortcut strings. Nothing makes them agree, and they had drifted apart in
// three ways at once:
//
//   'y'  Build Camp had a shortcut and no key case at all — "pressing y doesn't build a
//        camp, I have to go to the menu."
//   'p'  MenuPillage declares uppercase "P" and the Orders menu prints Shift+P, but the key
//        case pillaged on a bare 'p' — which also swallowed Clean Pollution's lowercase 'p'.
//   'c'  Auto-Clean Pollution's shortcut was never reachable: 'c' centres the view.
//
// Wait moved from 'w' to 'z' at the same time, because 'w' is claimed one method up for
// waking the next sleeping unit and never reaches a unit order.
//
// These drive the real key path, not the menu items, because the menu items all worked.

using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Screens.GamePlayPanels;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UnitKeyTests
	{
		// A settler standing on an iron seam, active, with the map screen focused on it.
		//
		// Owned by the CURRENT player, not the human: at turn 0 they are different, and
		// Game.ActiveUnit only ever offers a unit of the current player.
		private static (Settlers settler, GameMap map) ASettlerOnADeposit()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player player = g.CurrentPlayer;

			// The special lattice is positional, so the seam has to be found rather than
			// placed: ChangeTileType rebuilds the tile from its coordinates and would throw
			// away any Special set by hand.
			(int x, int y) seam = (0, 0);
			for (int y = 20; y <= 30 && seam.x == 0; y++)
			for (int x = 35; x <= 45; x++)
			{
				if (!Map.Instance.TileIsSpecial(x, y)) continue;
				Map.Instance.ChangeTileType(x, y, Terrain.Mountains);
				if (Game.ResourceAt(Map.Instance[x, y]) == StrategicResource.None) continue;
				seam = (x, y);
				break;
			}
			Assert.True(seam.x != 0, "no lattice special in the search window");

			player.Explore(seam.x, seam.y, range: 5);
			// The starting units first: Game.ActiveUnit picks by scanning the unit list, so
			// with them still around the key would go to whichever one it landed on.
			foreach (IUnit u in g.GetUnits().Where(u => player == u.Owner).ToArray())
				g.DisbandUnit(u);

			Settlers settler = (Settlers)g.CreateUnit(UnitType.Settlers, seam.x, seam.y, g.PlayerNumber(player))!;
			settler.MovesLeft = settler.Move;
			Sim.ClearTasks();
			g.ActiveUnit = settler;
			Assert.Same(settler, g.ActiveUnit);

			GameMap map = new GameMap();
			map.Resize(240, 192);
			map.CenterOnPoint(seam.x, seam.y);
			return (settler, map);
		}

		// GameMap.KeyDown's first act is to drop every key when the current player is not the
		// human, which no fixture can satisfy at turn 0. The letter dispatch under test is one
		// method down, so drive that: the gate above it is not what was broken.
		private static bool Press(GameMap map, char key, bool shift = false)
			=> (bool)typeof(GameMap)
				.GetMethod("KeyDownActiveUnit", BindingFlags.NonPublic | BindingFlags.Instance)!
				.Invoke(map, new object[] { new KeyboardEventArgs(key, shift ? KeyModifier.Shift : KeyModifier.None) })!;

		private static System.Collections.Generic.IEnumerable<IUnit> WaitingUnits()
			=> (System.Collections.Generic.IEnumerable<IUnit>)typeof(Game)
				.GetField("_waitingUnits", BindingFlags.NonPublic | BindingFlags.Instance)!
				.GetValue(Game.Instance)!;

		[Fact]
		public void PressingYBuildsACamp()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();

			Assert.True(Press(map, 'Y'), "'Y' was not handled");
			Assert.True(settler.BuildingCamp > 0, "no camp under construction");
		}

		// ...and the same key on ordinary ground does nothing, rather than starting a camp
		// on a tile with no deposit: the guard lives in MenuItems, which the key reuses.
		[Fact]
		public void PressingYOffADepositDoesNothing()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();
			Map.Instance.ChangeTileType(settler.X, settler.Y, Terrain.Grassland1);

			Assert.False(Press(map, 'Y'));
			Assert.Equal(0, settler.BuildingCamp);
		}

		// 'z' waits. It has to go through the menu item's own shortcut, so the key and the
		// hint the Orders menu prints cannot drift apart again.
		[Fact]
		public void PressingZWaits()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();

			Assert.True(Press(map, 'Z'), "'Z' was not handled");
			Assert.Contains(settler, WaitingUnits());
		}

		// A bare 'p' cleans pollution and does NOT pillage. The tile carries a road as well as
		// the pollution: if the old unshifted-pillage path ran, the road is what it would have
		// taken, so the road surviving is the real assertion here.
		[Fact]
		public void PressingPCleansPollutionWithoutPillaging()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();
			ITile tile = Map.Instance[settler.X, settler.Y];
			tile.Road = true;
			tile.Pollution = true;

			Assert.True(Press(map, 'P'), "'P' was not handled");
			Sim.Settle();   // Clean Pollution goes through a queued Orders task

			Assert.True(settler.BuildingCleanPollution > 0, "not cleaning");
			Assert.True(tile.Road, "the road was pillaged");
		}

		// Shift+P still pillages — the modifier the Orders menu has always advertised.
		[Fact]
		public void ShiftPPillages()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();
			ITile tile = Map.Instance[settler.X, settler.Y];
			tile.Road = true;

			// The return value is not the assertion: this case has always ended in `break`,
			// like 'M' and 'R', so the key reports unhandled either way. The road is the proof.
			Press(map, 'P', shift: true);

			Assert.False(tile.Road, "the road survived");
		}

		// 'x' reaches Auto-Clean Pollution, which needs pollution somewhere in a city radius.
		[Fact]
		public void PressingXStartsAutoCleaning()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();
			Game g = Game.Instance;
			g.AddCity(g.CurrentPlayer, 0, settler.X + 1, settler.Y);
			Map.Instance[settler.X + 2, settler.Y].Pollution = true;

			Assert.True(Press(map, 'X'), "'X' was not handled");
			Assert.True(settler.AutoClean, "auto-clean is off");
		}

		// ...and 'c' is still the navigation key it was, not the order it used to shadow.
		[Fact]
		public void PressingCDoesNotStartAutoCleaning()
		{
			(Settlers settler, GameMap map) = ASettlerOnADeposit();
			Game g = Game.Instance;
			g.AddCity(g.CurrentPlayer, 0, settler.X + 1, settler.Y);
			Map.Instance[settler.X + 2, settler.Y].Pollution = true;

			Press(map, 'C');

			Assert.False(settler.AutoClean, "'c' started an order instead of centring");
		}
	}
}
