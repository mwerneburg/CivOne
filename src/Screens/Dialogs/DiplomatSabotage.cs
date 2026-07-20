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
using System.Collections.Generic;

namespace CivOne.Screens.Dialogs
{
	internal class DiplomatSabotage : BaseDialog
	{
		private const int FONT_ID = 0;

		private readonly City _enemyCity;
		private readonly Diplomat _diplomat;

		internal DiplomatSabotage(City enemyCity, Diplomat diplomat) : base(60, 80, 220, 56)
		{
			_enemyCity = enemyCity ?? throw new ArgumentNullException(nameof(enemyCity));
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

			DialogBox.DrawText($"Spies Report", 0, 15, 45, 5);
			DialogBox.DrawText(_diplomat.Sabotage(_enemyCity), 0, 15, 45, 5 + Resources.GetFontHeight(FONT_ID));
			DialogBox.DrawText($"in {_enemyCity.Name}", 0, 15, 45, 5 + (2 * Resources.GetFontHeight(FONT_ID)));
		}
	}
}
