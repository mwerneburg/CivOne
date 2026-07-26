// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Screens.Debug;
using CivOne.Graphics.Sprites;
using CivOne.Tasks;
using CivOne.UserInterface;

namespace CivOne.Screens
{
	internal class DebugOptions : BaseScreen
	{
		private bool _update = true;
		
		private void MenuCancel(object sender, EventArgs args)
		{
			Destroy();
		}

		private void MenuReloadFreeTiles(object sender, EventArgs args)
		{
			Free.Instance.ReloadTiles();
			Common.ReloadPalette256();
			MapTile.ReloadTileCaches();
			Destroy();
		}

		private void MenuSetGameYear(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<SetGameYear>());
			Destroy();
		}

		private void MenuSetPlayerGold(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<SetPlayerGold>());
			Destroy();
		}

		private void MenuSetPlayerScience(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<SetPlayerScience>());
			Destroy();
		}

		private void MenuSetPlayerAdvances(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<SetPlayerAdvances>());
			Destroy();
		}

		private void MenuSetCitySize(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<SetCitySize>());
			Destroy();
		}

		private void MenuCityDisaster(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<CauseDisaster>());
			Destroy();
		}

		private void MenuChangeHumanPlayer(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<ChangeHumanPlayer>());
			Destroy();
		}

		private void MenuSpawnUnit(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<SpawnUnit>());
			Destroy();
		}

		private void MenuMeetWithKing(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<MeetWithKing>());
			Destroy();
		}

		private void MenuRevealWorld(object sender, EventArgs args)
		{
			Settings.Instance.RevealWorldCheat();
			Destroy();
		}

		private void MenuShowPowerGraph(object sender, EventArgs args)
		{
			GameTask.Enqueue(Show.Screen<PowerGraph>());
			Destroy();
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (_update)
			{
				_update = false;

				Picture menuGfx = new Picture(131, 111)
					.Tile(Pattern.PanelGrey)
					.DrawRectangle3D()
					.DrawText("Debug Options:", 0, 15, 4, 4)
					.As<Picture>();

				// Crop the active-item strip to the menu width. Was 136 wide taken from
				// x=2 of a 131-wide picture — a 7px overrun that sampled out of bounds
				// and threw the highlight out of alignment.
				IBitmap menuBackground = menuGfx[2, 11, 127, 88].ColourReplace((7, 11), (22, 3));

				this.AddLayer(menuGfx, 25, 17);

				Menu menu = new Menu(Palette, menuBackground)
				{
					X = 27,
					Y = 28,
					MenuWidth = 127,
					// Cassette palette: the panel behind this menu is PanelGrey, built
					// from indices 3-4 (near-black). The old TextColour 5 is BORDER, a
					// dark brown, and DisabledColour 3 was the background colour itself
					// — both were invisible once the original asset palette went away.
					ActiveColour = CassetteTheme.PHOS_DIM,
					TextColour = CassetteTheme.INK_HIGH,
					DisabledColour = CassetteTheme.INK_LOW,
					FontId = 0,
					Indent = 8
				};
				menu.MissClick += MenuCancel;
				menu.Cancel += MenuCancel;

				menu.Items.Add("Set Game Year").OnSelect(MenuSetGameYear);
				menu.Items.Add("Set Player Gold").OnSelect(MenuSetPlayerGold);
				menu.Items.Add("Set Player Science").OnSelect(MenuSetPlayerScience);
				menu.Items.Add("Set Player Advances").OnSelect(MenuSetPlayerAdvances);
				menu.Items.Add("Set City Size").OnSelect(MenuSetCitySize);
				menu.Items.Add("Cause City Disaster").OnSelect(MenuCityDisaster);
				menu.Items.Add("Change Human Player").OnSelect(MenuChangeHumanPlayer);
				menu.Items.Add("Spawn Unit").OnSelect(MenuSpawnUnit);
				menu.Items.Add("Meet With King").OnSelect(MenuMeetWithKing);
				menu.Items.Add("Toggle Reveal World").OnSelect(MenuRevealWorld);
				menu.Items.Add("Show PowerGraph").OnSelect(MenuShowPowerGraph);
				menu.Items.Add("Reload Free Tiles").OnSelect(MenuReloadFreeTiles);

				this.FillRectangle(24, 16, 105, menu.RowHeight * (menu.Items.Count + 1), 5);

				AddMenu(menu);
			}
			return true;
		}

		public DebugOptions() : base(MouseCursor.Pointer)
		{
			using var p = Common.DefaultPalette;
			Palette = p;
			this.AddLayer(Common.Screens.Last(), 0, 0)
				.FillRectangle(24, 16, 133, 113, 5);
		}
	}
}