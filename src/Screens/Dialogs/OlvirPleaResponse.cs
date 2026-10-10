// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Graphics;
using CivOne.UserInterface;

namespace CivOne.Screens.Dialogs
{
	// The Olvir never fight a war of their own choosing (Player.DeclareWar), so when an AI
	// civ attacks them they turn to the human. Answering is a war on the refugees' behalf,
	// and counts as honouring a pact rather than a war of our making
	// (Player.DeclareWarOnBehalfOf), so it does not break a peaceful victory streak.
	internal class OlvirPleaResponse : BaseDialog
	{
		private const int FONT_ID = 0;

		private readonly Player _aggressor;

		private void NoAction(object sender, EventArgs args) => Cancel();

		private void Defend(object sender, EventArgs args)
		{
			Human.DeclareWarOnBehalfOf(_aggressor);
			Cancel();
		}

		protected override void FirstUpdate()
		{
			Menu menu = new Menu(Palette)
			{
				X = 72,
				Y = 62 + 8 + (3 * Resources.GetFontHeight(FONT_ID)) + 6,
				MenuWidth = 184,
				ActiveColour = CassetteTheme.PHOS_FAINT,
				TextColour = CassetteTheme.INK_HIGH,
				DisabledColour = CassetteTheme.INK_LOW,
				FontId = FONT_ID
			};

			menu.Items.Add("Take no action").OnSelect(NoAction);
			menu.Items.Add($"Declare war on the {_aggressor.TribeNamePlural}").OnSelect(Defend);

			AddMenu(menu);
		}

		internal OlvirPleaResponse(Player aggressor) : base(64, 62, 200, 82)
		{
			_aggressor = aggressor ?? throw new ArgumentNullException(nameof(aggressor));

			using Palette palette = Common.DefaultPalette;
			using (Palette cass = CassetteTheme.CreatePalette())
				palette.MergePalette(cass, 1, 17);
			this.SetPalette(palette);

			int fh = Resources.GetFontHeight(FONT_ID);
			DialogBox.DrawText("The Olvir ask for our help!", FONT_ID, 15, 8, 6);
			DialogBox.DrawText($"The {_aggressor.TribeNamePlural} have", FONT_ID, 15, 8, 6 + fh);
			DialogBox.DrawText("attacked the refugees.", FONT_ID, 16, 8, 6 + (2 * fh));
		}
	}
}
