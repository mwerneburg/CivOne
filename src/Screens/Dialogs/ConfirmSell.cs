// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Buildings;
using CivOne.Graphics;

namespace CivOne.Screens.Dialogs
{
	internal class ConfirmSell : BaseDialog
	{
		public IBuilding Building { get; private set; }

		public event EventHandler? Sell;

		private void MenuYes(object sender, EventArgs args)
		{
			if (Sell is not null)
				Sell(this, args);
			Cancel();
		}

		protected override void FirstUpdate()
		{
			Menu menu = new Menu(Palette)
			{
				X = 133,                       // dialog left (128) + text indent (5)
				Y = 80 + 5 + TextHeight + 3,   // dialog top + top margin + text + gap
				MenuWidth = TextWidth - 4,     // narrower than text — leaves clearance from the dialog right border
				ActiveColour = CassetteTheme.PHOS_FAINT,
				TextColour = CassetteTheme.INK_HIGH,
				FontId = 0
			};
			int i = 0;
			foreach (string choice in (string[])["No.", "Yes."])
			{
				menu.Items.Add(choice, i++);
			}
			menu.Items[0].Selected += Cancel;
			menu.Items[1].Selected += MenuYes;

			menu.MissClick += Cancel;
			menu.Cancel += Cancel;
			AddMenu(menu);
		}

		public ConfirmSell(IBuilding building) : base(128, 80, 18, 23, ["Do you want to sell", $"your {building.Name} for {building.SellPrice}$?"])
		{
			Building = building;
			
			for (int i = 0; i < TextLines.Length; i++)
			{
				DialogBox.AddLayer(TextLines[i], 5, (TextLines[i].Height * i) + 5);
			}
		}
	}
}
