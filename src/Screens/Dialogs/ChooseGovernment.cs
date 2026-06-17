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
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Governments;
using CivOne.UserInterface;

namespace CivOne.Screens.Dialogs
{
	internal class ChooseGovernment : BaseDialog
	{
		private readonly IGovernment[] _availableGovernments;

		public IGovernment Result { get; private set; } = null!;

		private void GovernmentChoice(object sender, MenuItemEventArgs<IGovernment> args)
		{
			Result = args.Value;
			CloseMenus();
			Cancel();
		}

		public override bool KeyDown(KeyboardEventArgs args) => true;
		public override bool MouseDown(ScreenEventArgs args) => true;

		protected override void FirstUpdate()
		{
			Menu<IGovernment> menu = new Menu<IGovernment>("ChooseGovernment", Palette)
			{
				X = 103,
				Y = 84,
				MenuWidth = 82,
				ActiveColour = CassetteTheme.PHOS_FAINT,
				TextColour = CassetteTheme.INK_HIGH,
				FontId = 0
			};
			foreach (IGovernment government in _availableGovernments)
			{
				menu.Items.Add($"{government.NameAdjective}", government).OnSelect(GovernmentChoice);
			}
			menu.MissClick += (s, a) => { };
			AddMenu(menu);
		}

		private static int DialogHeight
		{
			get
			{
				return (Game.HumanPlayer.AvailableGovernments.Count() * Resources.GetFontHeight(0)) + 23;
			}
		}

		public ChooseGovernment() : base(100, 64, 86, DialogHeight)
		{
			_availableGovernments = Game.HumanPlayer.AvailableGovernments.ToArray();

			DialogBox.DrawText("Select type of", 0, 15, 5, 5);
			DialogBox.DrawText("Government...", 0, 15, 5, 13);
		}
	}
}