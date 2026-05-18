// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.UserInterface;

namespace CivOne.Screens
{
	[Expand]
	internal class ChooseTech : BaseScreen
	{
		private readonly IAdvance[] _availableAdvances;
		private bool _menuCreated = false;

		private void AdvanceChoice(object sender, MenuItemEventArgs<IAdvance> args)
		{
			Human.CurrentResearch = args.Value;
			Destroy();
		}

		private void AdvanceContext(object sender, MenuItemEventArgs<IAdvance> args)
		{
			Common.AddScreen(new Civilopedia(args.Value));
		}

		protected override bool HasUpdate(uint gameTick)
		{
			int fh  = Resources.GetFontHeight(0);
			int lh  = fh + 1;
			const string hint = "RIGHT-CLICK ANY ITEM FOR INFO";

			// Measure panel width
			int innerW = Resources.GetTextSize(0, hint).Width;
			foreach (IAdvance adv in _availableAdvances)
				innerW = System.Math.Max(innerW, Resources.GetTextSize(0, adv.Name).Width);
			innerW = System.Math.Max(innerW, 160);

			int pw = innerW + 24;
			int ph = 2 + 6                               // borders + top pad
			       + _availableAdvances.Length * lh       // menu rows
			       + 3 + 1 + 3                            // gap + divider + gap
			       + fh + 6;                              // hint + bottom pad

			int ox = (Width  - pw) / 2;
			int oy = (Height - ph) / 2;

			// Draw panel every frame (background for menu overlay)
			this.Clear(CassetteTheme.BG0);
			this.DrawCassettePanel(ox, oy, pw, ph, "CHOOSE RESEARCH");
			this.AddScanlines(ox + 1, oy + 1, pw - 2, ph - 2);

			// Draw hint at bottom (after scanlines so text is fully visible)
			int hintY = oy + ph - fh - 6;
			int hintX = ox + 12;
			this.DrawCassetteDivider(hintX, hintY - 4, pw - 24);
			this.DrawText(hint, 0, CassetteTheme.INK_LOW, ox + pw / 2, hintY, TextAlign.Center);

			// Create the menu once
			if (!_menuCreated)
			{
				_menuCreated = true;

				int menuX = ox + 12;
				int menuY = oy + 6;

				var menu = new Menu<IAdvance>("ChooseTech", Palette)
				{
					X         = menuX,
					Y         = menuY,
					MenuWidth = pw - 24,
					ActiveColour  = CassetteTheme.PHOS_FAINT,
					TextColour    = CassetteTheme.INK_HIGH,
					DisabledColour = CassetteTheme.INK_LOW,
					FontId    = 0
				};

				foreach (IAdvance advance in _availableAdvances)
				{
					menu.Items.Add(advance.Name, advance)
						.OnSelect(AdvanceChoice)
						.OnContext(AdvanceContext)
						.OnHelp(AdvanceContext);
				}

				AddMenu(menu);
			}

			return true;
		}

		public ChooseTech() : base(MouseCursor.Pointer)
		{
			_availableAdvances = Human.AvailableResearch.Take(8).ToArray();

			using Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
