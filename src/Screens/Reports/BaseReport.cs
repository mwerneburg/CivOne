// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	[Modal, OwnPalette]
	internal abstract class BaseReport : BaseScreen
	{
		private bool _update = true;

		protected readonly IBitmap[] Portrait = new Picture[4];

		protected event ScreenEventHandler OnMouseDown;
		
		protected byte BackgroundColour { get; }
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;
			return true;
		}

		protected void SetUpdate()
		{
			_update = true;
		}
		
		public override bool KeyDown(KeyboardEventArgs args)
		{
			Destroy();
			return true;
		}
		
		public override bool MouseDown(ScreenEventArgs args)
		{
			OnMouseDown?.Invoke(this, args);
			if (args.Handled) return true;

			Destroy();
			return true;
		}
		
		public BaseReport(string title, byte backgroundColour, MouseCursor cursor = MouseCursor.None) : base(cursor)
		{
			BackgroundColour = backgroundColour;

			bool modernGovernment = Human.HasAdvance<Invention>();
			for (int i = 0; i < 4; i++)
			{
				Portrait[i] = Icons.GovernmentPortrait(Human.Government, (Advisor)Enum.Parse(typeof(Advisor), $"{i}"), modernGovernment);
			}
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			p.MergePalette(Portrait[0].Palette, 144);
			Palette = p;

			this.Clear(CassetteTheme.BG0)
				.FillRectangle(0, 0, 320, 27, CassetteTheme.BG3)
				.FillRectangle(0, 27, 320, 1, CassetteTheme.BORDER)
				.DrawText(title, 0, CassetteTheme.PHOS_GLOW, 160, 2, TextAlign.Center)
				.DrawText(string.Format("{0} of the {1}", "Empire", Human.TribeNamePlural), 0, CassetteTheme.INK_MID, 160, 10, TextAlign.Center)
				.DrawText(string.Format("{0} {1}: {2}", "Emperor", Human.LeaderName, Game.GameYear), 0, CassetteTheme.INK_LOW, 160, 18, TextAlign.Center);
		}
	}
}