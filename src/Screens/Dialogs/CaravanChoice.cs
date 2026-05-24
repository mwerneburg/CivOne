// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.UserInterface;
using CivOne.Wonders;

namespace CivOne.Screens.Dialogs
{
	[OwnPalette]
	internal class CaravanChoice : BaseScreen
	{
		private const int FONT_ID = 0;

		private readonly Caravan _unit;
		private readonly City _city;
		private bool _update = true;

		private void KeepMoving(object sender, EventArgs args)
		{
			_unit.KeepMoving(_city);
			Destroy();
		}

		private void EstablishTradeRoute(object sender, EventArgs args)
		{
			_unit.EstablishTradeRoute(_city);
			Destroy();
		}

		private void HelpBuildWonder(object sender, EventArgs args)
		{
			_unit.HelpBuildWonder(_city);
			Destroy();
		}

		private bool IsOwnCity  => _city.Owner == _unit.Owner;
		private bool IsForeignDome => !IsOwnCity && _city.CurrentProduction is CivOne.Wonders.IDomeComponent;
		private bool ShowHelpWonder => IsOwnCity && _city.CurrentProduction is IWonder;

		private bool AllowEstablishTradeRoute => (_unit.Home is null) || (_unit.Home.Tile.DistanceTo(_city) >= 10);

		private int ChoiceCount => (ShowHelpWonder || IsForeignDome) ? 3 : 2;

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			int fh    = Resources.GetFontHeight(FONT_ID);
			int pw    = 140;
			int ph    = 16 + (ChoiceCount * (fh + 2));
			int px    = 100;
			int py    = 80;

			// Outer border
			this.FillRectangle(px,          py,          pw, ph, CassetteTheme.BORDER);
			// Body fill
			this.FillRectangle(px + 1,      py + 1,      pw - 2, ph - 2, CassetteTheme.BG1);
			// Title band
			this.FillRectangle(px + 1,      py + 1,      pw - 2, 13, CassetteTheme.BG3);
			this.FillRectangle(px + 1,      py + 13,     pw - 2, 1, CassetteTheme.BORDER);
			this.DrawText("Will you?", FONT_ID, CassetteTheme.PHOS, px + pw / 2, py + 3, TextAlign.Center);

			Menu menu = new Menu(Palette)
			{
				X          = px + 1,
				Y          = py + 15,
				MenuWidth  = pw - 2,
				ActiveColour   = CassetteTheme.PHOS_FAINT,
				TextColour     = CassetteTheme.INK_HIGH,
				DisabledColour = CassetteTheme.INK_LOW,
				FontId     = FONT_ID
			};

			menu.Items.Add("Keep moving").OnSelect(KeepMoving);
			menu.Items.Add("Establish trade route").OnSelect(EstablishTradeRoute).SetEnabled(AllowEstablishTradeRoute);
			if (ShowHelpWonder)
				menu.Items.Add("Help build WONDER.").OnSelect(HelpBuildWonder);
			else if (IsForeignDome)
				menu.Items.Add("Assist dome construction").OnSelect(HelpBuildWonder);

			AddMenu(menu);
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		internal CaravanChoice(Caravan unit, City city) : base(MouseCursor.Pointer)
		{
			_unit = unit;
			_city = city;

			Palette p = Common.GamePlayPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
