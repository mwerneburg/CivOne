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
	// Convened when a foreign civ's diplomats have committed Game.ProvocationThreshold
	// hostile acts against the human — sabotage or incited revolt — under an elected
	// government.
	//
	// The sibling of IncitedCityResponse, which handles a single incite. A single
	// sabotage is an incident and gets only a spy report; a pattern of them is a
	// campaign, and a Democracy that cannot answer a campaign is the gap this fills.
	// Crossing the threshold ALSO lifts the Senate's veto on attacking that civ
	// (Game.IsProvocateur, read in BaseUnit.Confront) whatever the player chooses
	// here — the Senate will not start your war, but it stops shielding an aggressor.
	internal class SenateGrievanceResponse : BaseDialog
	{
		private const int FONT_ID = 0;

		private readonly Player _culprit;

		private void NoAction(object sender, EventArgs args) => Cancel();

		private void DeclareWar(object sender, EventArgs args)
		{
			// Tear up any standing treaty: three acts of sabotage is provocation enough.
			Human.SetPeaceTreaty(_culprit, 0);
			_culprit.SetPeaceTreaty(Human, 0);
			Human.DeclareWar(_culprit);
			Cancel();
		}

		private void DemandReparations(object sender, EventArgs args)
		{
			// Same test IncitedCityResponse uses for its city demand: a civ the human
			// dwarfs pays up rather than face a war it cannot win; a peer denies it all.
			bool concede = Human.Score >= _culprit.Score * 2;

			if (concede)
			{
				short paid = (short)Math.Min((int)_culprit.Gold, 200);
				_culprit.Gold -= paid;
				Human.Gold += paid;
				GameTask.Insert(Message.Advisor(Advisor.Foreign, false,
					$"The {_culprit.TribeNamePlural} disavow", "their agents and pay",
					$"${paid} in reparations."));
			}
			else
			{
				GameTask.Insert(Message.Advisor(Advisor.Foreign, true,
					$"The {_culprit.TribeNamePlural} deny", "everything and offer",
					"nothing."));
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
			menu.Items.Add($"Declare war on the {_culprit.TribeNamePlural}").OnSelect(DeclareWar);
			menu.Items.Add("Demand reparations").OnSelect(DemandReparations)
				.SetEnabled(Human.HasEmbassy(_culprit));

			AddMenu(menu);
		}

		internal SenateGrievanceResponse(Player culprit) : base(64, 62, 200, 92)
		{
			_culprit = culprit ?? throw new ArgumentNullException(nameof(culprit));

			using Palette palette = Common.DefaultPalette;
			using (Palette cass = CassetteTheme.CreatePalette())
				palette.MergePalette(cass, 1, 17);
			this.SetPalette(palette);

			int fh = Resources.GetFontHeight(FONT_ID);
			DialogBox.DrawText($"{_culprit.TribeName} agents have struck", FONT_ID, 15, 8, 6);
			DialogBox.DrawText("our cities once too often!", FONT_ID, 15, 8, 6 + fh);
			DialogBox.DrawText("The Senate recommends WAR.", FONT_ID, 16, 8, 6 + (2 * fh));
		}
	}
}
