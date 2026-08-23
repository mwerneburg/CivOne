// CivOne tests
//
// The Hydro Engineer's two sea orders were both keyboard-dead.
//
// "Build Sea Tube" was 't'. GameMap.KeyDown claims 'T' for the terrain view and RETURNS
// before KeyDownActiveUnit is ever reached, so the key never got near the unit menu — the
// order was reachable by mouse alone. Reported as a collision with reveal-terrain.
//
// "Reclaim Land" was 'r', and that was worse than dead: GameMap's case 'R' went straight to
// Orders.BuildRoad, whose Road() refuses anything that is not a Settlers and raises a
// "SETTLERS" error. Pressing it on a Hydro Engineer produced a complaint, not an order.
//
// The tube is now 'r' — the sea's road, the key a player already has in their fingers for
// road and rail — and Reclaim Land moves to 'l'. Every letter is spoken for at the map
// layer, but a shortcut only has to be unique inside ONE unit's menu: GameMap forwards 'L'
// to whatever the active unit calls 'l', which is "Lower to Plains" on a Settlers and
// "Reclaim Land" here. The two never appear in the same menu.

using System.IO;
using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.UserInterface;
using CivOne.Units;

namespace CivOne.Tests
{
	public class HydroEngineerShortcutTests
	{
		// A Hydro Engineer on open water next to a coast, so BOTH sea orders are offered at
		// once — which is the only arrangement in which their shortcuts can collide.
		private static (Game game, HydroEngineer eng) AnEngineerOffTheCoast()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 40; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.ChangeTileType(41, 25, Terrain.Grassland1);   // the coast to reclaim against
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			p.Explore(35, 25, range: 20);
			foreach (var a in new CivOne.Advances.IAdvance[]
				{ new CivOne.Advances.Hydroengineering(), new CivOne.Advances.BioplexEngineering() })
				p.AddAdvance(a, false);

			var eng = (HydroEngineer)g.CreateUnit(UnitType.HydroEngineer, 40, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (g, eng);
		}

		private static MenuItem<int>? ByText(HydroEngineer e, string text) =>
			e.MenuItems.FirstOrDefault(m => m?.Text is not null && m.Text.Contains(text));

		// The request, stated directly.
		[Fact]
		public void TheSeaTubeIsBuiltWithR()
		{
			(Game g, HydroEngineer eng) = AnEngineerOffTheCoast();

			MenuItem<int>? tube = ByText(eng, "Sea Tube");
			Assert.NotNull(tube);
			Assert.Equal("r", tube!.Shortcut);
		}

		// ...and 't' is gone, or the collision with the terrain view survives the change.
		[Fact]
		public void NothingOnThisUnitStillClaimsT()
		{
			(Game g, HydroEngineer eng) = AnEngineerOffTheCoast();

			Assert.DoesNotContain(eng.MenuItems.Where(m => m is not null), m => m.Shortcut == "t");
		}

		// Reclaim Land had to move for the tube to have 'r'. It must actually have moved
		// SOMEWHERE — deleting the shortcut would satisfy a uniqueness check just as well.
		[Fact]
		public void ReclaimLandMovedToL()
		{
			(Game g, HydroEngineer eng) = AnEngineerOffTheCoast();

			MenuItem<int>? reclaim = ByText(eng, "Reclaim Land");
			Assert.NotNull(reclaim);
			Assert.Equal("l", reclaim!.Shortcut);
		}

		// The scenario that made the swap necessary: both orders on offer at the same time.
		[Fact]
		public void NoTwoOrdersShareAKeyWhenBothAreOffered()
		{
			(Game g, HydroEngineer eng) = AnEngineerOffTheCoast();

			Assert.NotNull(ByText(eng, "Sea Tube"));
			Assert.NotNull(ByText(eng, "Reclaim Land"));

			string[] keys = eng.MenuItems.Where(m => m is not null)
				.Select(m => m.Shortcut).Where(s => !string.IsNullOrEmpty(s)).ToArray();

			Assert.Equal(keys.Length, keys.Distinct().Count());
		}

		// The key has to reach the order. Selecting the item is what GameMap now dispatches to
		// on 'r', so this drives the same path the keyboard does.
		[Fact]
		public void PressingItActuallyLaysATube()
		{
			(Game g, HydroEngineer eng) = AnEngineerOffTheCoast();
			Assert.Equal(0, eng.BuildingTube);

			ByText(eng, "Sea Tube")!.Select();

			Assert.True(eng.BuildingTube > 0, "the order was selected and no tube was started");
		}

		// ...and the map layer must forward it. Without this clause 'r' reaches
		// Orders.BuildRoad, which errors "SETTLERS" on a Hydro Engineer — the menu would
		// advertise an order that complains when used. Pinned at the source because KeyDown
		// needs a live screen.
		[Fact]
		public void TheMapForwardsRToANonSettlersUnitMenu()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "GamePlayPanels", "GameMap.cs"));
			int at = src.IndexOf("case 'R':");
			Assert.True(at > 0, "the road key has moved");
			string block = src.Substring(at, src.IndexOf("case 'S':", at) - at);

			Assert.Contains("ActivateUnitMenuShortcut(\"r\")", block);
			Assert.Contains("is not Settlers", block);
		}

		// Settlers keep the direct Orders.BuildRoad path. Re-routing the most-used key in the
		// game to a different implementation is not what this change is for.
		[Fact]
		public void SettlersStillBuildRoadsTheOldWay()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(),
				"src", "Screens", "GamePlayPanels", "GameMap.cs"));
			int at = src.IndexOf("case 'R':");
			string block = src.Substring(at, src.IndexOf("case 'S':", at) - at);

			Assert.Contains("Orders.BuildRoad(Game.ActiveUnit)", block);
		}
	}
}
