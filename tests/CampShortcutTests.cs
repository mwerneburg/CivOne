// CivOne tests
//
// "Pressing y doesn't build a camp. I have to go to the menu."
//
// MenuBuildCamp has carried the shortcut "y" since it was written, but GameMap's keyboard
// switch is a hand-maintained list of letters and 'Y' was never added to it — so the key
// fell out of the bottom of the switch and did nothing, while the identical menu item
// worked. Every other settler order in that same fall-through group (Aquafarm, Plant
// Forest, Canopy Array...) is there; the camp was simply missed.
//
// The test drives the real key path, not the menu item, because the menu item was never
// broken.

using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Screens.GamePlayPanels;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CampShortcutTests
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
		private static bool Press(GameMap map, char key)
			=> (bool)typeof(GameMap)
				.GetMethod("KeyDownActiveUnit", BindingFlags.NonPublic | BindingFlags.Instance)!
				.Invoke(map, new object[] { new KeyboardEventArgs(key) })!;

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
	}
}
