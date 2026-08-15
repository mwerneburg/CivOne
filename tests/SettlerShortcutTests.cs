// CivOne tests
//
// Keyboard shortcuts for Terrace and Moisture Farm.
//
// Both shipped without one because every letter GameMap forwards to a unit menu was already
// claimed, and the note left in Settlers.cs was firm about the reason: a shortcut that
// silently does nothing is worse than an honest menu entry.
//
// The keys chosen are the mnemonic ones, taken behind Shift from the order each would be
// confused with:
//
//     i  Build Irrigation      Shift+I  Build Moisture Farm
//     y  Build Camp            Shift+Y  Build Terrace
//
// Both collisions are real rather than theoretical. Moisture Farm is desert-only and riverbank
// desert offers irrigation, so they appear on the same tile; Terrace is hills-only and
// Hills+Special is Coal, so a coal hill offers a camp and a terrace at once.
//
// The mechanism matters: an UPPERCASE Shortcut string is how this codebase says "shifted" —
// the Orders menu prints it as Shift+X, as MenuPillage's "P" always has. It also means
// GameMap.KeyDown must dispatch these explicitly, because its generic path lowercases the key
// (ActivateUnitMenuShortcut) and so can never reach an uppercase shortcut.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.UserInterface;

namespace CivOne.Tests
{
	public class SettlerShortcutTests
	{
		private static (Game g, Player p, Settlers s) ASettlerOn(Terrain terrain, params IAdvance[] advances)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			p.Explore(40, 25, range: 6);
			for (int y = 23; y <= 27; y++)
			for (int x = 38; x <= 42; x++)
				Map.Instance.ChangeTileType(x, y, terrain);
			Map.Instance.RecalculateContinentsIfDirty();
			foreach (IAdvance a in advances) p.AddAdvance(a, false);
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (g, p, s);
		}

		private static MenuItem<int>[] Menu(Settlers s) => s.MenuItems.Where(x => x is not null).ToArray();

		// The two new orders carry the shortcuts, in the shifted (uppercase) form.
		[Fact]
		public void TheTerraceIsOnShiftY()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Hills, new Masonry());

			MenuItem<int> terrace = Menu(s).Single(x => x.Text == "Build Terrace");

			Assert.Equal("Y", terrace.Shortcut);
		}

		[Fact]
		public void TheMoistureFarmIsOnShiftI()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Desert, new Refining());

			MenuItem<int> farm = Menu(s).Single(x => x.Text == "Build Moisture Farm");

			Assert.Equal("I", farm.Shortcut);
		}

		// ...and the unshifted keys still belong to the orders they always did. If these ever
		// coincide, one order becomes unreachable from the keyboard.
		[Fact]
		public void TheUnshiftedKeysStillBelongToIrrigationAndTheCamp()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Desert, new Refining());

			MenuItem<int> irrigation = Menu(s).Single(x => x.Text.Contains("Irrigation"));

			Assert.Equal("i", irrigation.Shortcut);
			Assert.NotEqual(irrigation.Shortcut, Menu(s).Single(x => x.Text == "Build Moisture Farm").Shortcut);
		}

		// The collision that made a shared key impossible: a coal hill offers both orders at
		// the same moment, so they must differ.
		[Fact]
		public void ACoalHillOffersBothTheCampAndTheTerrace()
		{
			(Game g, Player p, Settlers s) = ASettlerOn(Terrain.Hills, new Masonry());
			((BaseTile)Map.Instance[40, 25]).Special = true;
			Assert.NotEqual(StrategicResource.None, Game.ResourceAt(Map.Instance[40, 25]));

			MenuItem<int>[] menu = Menu(s);
			MenuItem<int> camp = menu.Single(x => x.Text.Contains("Camp"));
			MenuItem<int> terrace = menu.Single(x => x.Text == "Build Terrace");

			Assert.Equal("y", camp.Shortcut);
			Assert.NotEqual(camp.Shortcut, terrace.Shortcut);
		}

		// No two orders offered at the same time may share a key, or one of them is dead.
		[Theory]
		[InlineData(Terrain.Hills)]
		[InlineData(Terrain.Desert)]
		[InlineData(Terrain.Grassland1)]
		[InlineData(Terrain.Plains)]
		[InlineData(Terrain.Swamp)]
		public void NoTwoOrdersOnOneTileShareAShortcut(Terrain terrain)
		{
			(Game g, Player p, Settlers s) = ASettlerOn(terrain, new Masonry(), new Refining(),
				new Explosives(), new Construction(), new BridgeBuilding());
			((BaseTile)Map.Instance[40, 25]).Special = true;

			var duplicates = Menu(s)
				.Where(x => !string.IsNullOrEmpty(x.Shortcut))
				.GroupBy(x => x.Shortcut)
				.Where(grp => grp.Count() > 1)
				.Select(grp => $"{grp.Key}: {string.Join(", ", grp.Select(x => x.Text))}")
				.ToArray();

			Assert.True(duplicates.Length == 0,
				$"{terrain} offers orders sharing a key — {string.Join(" | ", duplicates)}");
		}

		// The dispatch has to exist, because the generic path cannot reach an uppercase
		// shortcut: ActivateUnitMenuShortcut lowercases the key before comparing. Pinned on
		// the source — a keyboard event cannot be staged headless, and the failure mode here
		// is precisely a shortcut that looks right in the menu and does nothing when pressed.
		[Theory]
		[InlineData("\"Y\"")]
		[InlineData("\"I\"")]
		public void GameMapDispatchesTheShiftedKeys(string shortcut)
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Screens", "GamePlayPanels", "GameMap.cs"));

			Assert.Contains($"args.Shift && ActivateUnitMenuShortcut({shortcut})", src);
		}
	}
}
