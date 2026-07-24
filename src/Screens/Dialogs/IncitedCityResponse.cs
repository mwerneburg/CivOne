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
using CivOne.Graphics;
using CivOne.Tasks;
using CivOne.UserInterface;

namespace CivOne.Screens.Dialogs
{
	// Shown when a foreign diplomat incites a revolt in one of the HUMAN's cities while
	// the human is governed by an elected government (Republic/Democracy). The Senate,
	// affronted, recommends war — but the decision is the player's: do nothing, declare
	// war, or (with an embassy) convene a discussion and strong-arm the culprit into
	// returning the city. Under authoritarian rule there is no Senate to convene, so the
	// incite path just shows the plain "Spies report" message instead.
	internal class IncitedCityResponse : BaseDialog
	{
		private const int FONT_ID = 0;

		private readonly City _city;
		private readonly Player _inciter;

		private void NoAction(object sender, EventArgs args) => Cancel();

		private void DeclareWar(object sender, EventArgs args)
		{
			// Clear any standing peace treaty so the declaration always takes effect —
			// inciting the player's city is provocation enough to tear one up.
			Human.SetPeaceTreaty(_inciter, 0);
			_inciter.SetPeaceTreaty(Human, 0);
			Human.DeclareWar(_inciter);
			Cancel();
		}

		private void ConveneDiscussion(object sender, EventArgs args)
		{
			// The culprit weighs the demand against the human's relative might. A civ that
			// the hegemon dwarfs (twice its score or more) folds and restores the city to
			// avoid a war it cannot win; a peer denies everything and keeps it.
			bool concede = Human.Score >= _inciter.Score * 2;

			if (concede && Game.GetCities().Length > 0 && System.Array.IndexOf(Game.GetCities(), _city) >= 0)
			{
				_city.Owner = (byte)Game.PlayerNumber(Human);
				Game.UpdateResources(_city.Tile);
				Human.MakePeace(_inciter);
				Human.SetPeaceTreaty(_inciter, 50);
				_inciter.SetPeaceTreaty(Human, 50);
				GameTask.Insert(Message.Advisor(Advisor.Foreign, false,
					$"The {_inciter.TribeNamePlural} bow to your", $"strength: {_city.Name} is restored,",
					"and 50 turns of peace pledged."));
			}
			else
			{
				GameTask.Insert(Message.Advisor(Advisor.Foreign, true,
					$"The {_inciter.TribeNamePlural} deny all", $"involvement and keep {_city.Name}."));
			}
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
			menu.Items.Add($"Declare war on the {_inciter.TribeNamePlural}").OnSelect(DeclareWar);
			menu.Items.Add("Convene discussion").OnSelect(ConveneDiscussion).SetEnabled(Human.HasEmbassy(_inciter));

			AddMenu(menu);
		}

		internal IncitedCityResponse(City city, Player inciter) : base(64, 62, 200, 92)
		{
			_city = city ?? throw new ArgumentNullException(nameof(city));
			_inciter = inciter ?? throw new ArgumentNullException(nameof(inciter));

			using Palette palette = Common.DefaultPalette;
			using (Palette cass = CassetteTheme.CreatePalette())
				palette.MergePalette(cass, 1, 17);
			this.SetPalette(palette);

			int fh = Resources.GetFontHeight(FONT_ID);
			DialogBox.DrawText($"{_inciter.TribeName} influence suspected", FONT_ID, 15, 8, 6);
			DialogBox.DrawText($"in {_city.Name}!", FONT_ID, 15, 8, 6 + fh);
			DialogBox.DrawText("The Senate recommends WAR.", FONT_ID, 16, 8, 6 + (2 * fh));
		}
	}
}
