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
using CivOne.Graphics;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.UserInterface;
using CivOne.Buildings;
using CivOne.Tasks;

namespace CivOne.Screens.Dialogs
{
	internal class DiplomatIncite : BaseDialog
	{
		private const int FONT_ID = 0;

		private readonly City _cityToIncite;
		private readonly Diplomat _diplomat;

		private int _inciteCost;

		private bool _canIncite;

		private void DontIncite(object sender, EventArgs args)
		{
			Cancel();
		}

		private void Incite(object sender, EventArgs args)
		{
			Player previousOwner = Game.GetPlayer(_cityToIncite.Owner);

			// Show the incite-rebellion art for a diplomat-caused flip; fall back to the
			// generic city-capture view if the art file isn't present.
			string? artPath = CivOne.Screens.ImprovementArtScreen.FindArtPath("Incite Rebellion", "event_art");
			Show captureCity = artPath is not null
				? Show.Screen(new CivOne.Screens.ImprovementArtScreen(artPath, "Incite Rebellion", _cityToIncite.Name))
				: Show.CaptureCity(_cityToIncite);
			captureCity.Done += (s1, a1) =>
			{
				byte oldOwner = _cityToIncite.Owner;
				IUnit[] toDisband = _cityToIncite.Units
					.Concat(_cityToIncite.Tile.Units.Where(u => u.Owner == oldOwner))
					.Distinct()
					.ToArray();
				foreach (IUnit unit in toDisband)
					Game.DisbandUnit(unit);
				Game.DisbandUnit(_diplomat);
				_cityToIncite.Owner = _diplomat.Owner;
				_cityToIncite.TechStolen = false;

				// remove half the buildings at random
				foreach (IBuilding building in _cityToIncite.Buildings.Where(b => Common.Random.Next(0, 2) == 1).ToList())
				{
					_cityToIncite.RemoveBuilding(building);
				}

				_diplomat.Player.Gold -= _inciteCost;

				previousOwner.IsDestroyed();

				if (Human == _cityToIncite.Owner || Human == _diplomat.Owner)
				{
					GameTask.Insert(Tasks.Show.CityManager(_cityToIncite));
				}
			};

			if (Human == _cityToIncite.Owner || Human == _diplomat.Owner)
			{
				GameTask.Insert(captureCity);
			}

			Cancel();
		}

		protected override void FirstUpdate()
		{
			int choices = _canIncite ? 2 : 0;

			if (_canIncite)
			{
				Menu menu = new Menu(Palette)
				{
					X = 143,
					Y = 110,
					MenuWidth = 130,
					ActiveColour = CassetteTheme.PHOS_FAINT,
					TextColour = CassetteTheme.INK_HIGH,
					FontId = FONT_ID
				};

				menu.Items.Add("Forget It.").OnSelect(DontIncite);

				if (_canIncite)
				{
					menu.Items.Add("Incite revolt").OnSelect(Incite);
				}

				AddMenu(menu);
			}
		}

		internal DiplomatIncite(City cityToIncite, Diplomat diplomat) : base(100, 80, 180, 56)
		{
			_cityToIncite = cityToIncite ?? throw new ArgumentNullException(nameof(cityToIncite));
			_diplomat = diplomat ?? throw new ArgumentNullException(nameof(diplomat));

			IBitmap spyPortrait = Icons.Spy;

			using Palette palette = Common.DefaultPalette;
			using (Palette cass = CassetteTheme.CreatePalette())
				palette.MergePalette(cass, 1, 17);
			// Copy only the palette entries the portrait actually uses (not a blanket
			// 144-255 copy), so custom high-index terrain colours behind the popup
			// aren't clobbered — see SpyMessage.cs for the full explanation.
			var bmp = spyPortrait.Bitmap;
			bool[] used = new bool[256];
			for (int yy = 0; yy < bmp.Height; yy++)
			for (int xx = 0; xx < bmp.Width; xx++)
				used[bmp[xx, yy]] = true;
			for (int i = 144; i < 256; i++)
				if (used[i]) palette[i] = spyPortrait.Palette[i];
			this.SetPalette(palette);

			DialogBox.AddLayer(spyPortrait, 2, 2);

			_inciteCost = Diplomat.InciteCost(cityToIncite);
			_canIncite = Diplomat.CanIncite(cityToIncite, diplomat.Player.Gold);

			DialogBox.DrawText($"Spies Report", 0, 15, 45, 5);
			DialogBox.DrawText($"Dissidents in {_cityToIncite.Name}", 0, 15, 45, 5 + Resources.GetFontHeight(FONT_ID));
			DialogBox.DrawText($"will revolt for ${_inciteCost}", 0, 15, 45, 5 + (2 * Resources.GetFontHeight(FONT_ID)));
		}
	}
}
