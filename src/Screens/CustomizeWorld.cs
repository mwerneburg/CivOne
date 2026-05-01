// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Drawing;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens
{
	[Expand, OwnPalette]
	internal class CustomizeWorld : BaseScreen
	{
		private static readonly (string Label, Size Size)[] MapSizes = new[]
		{
			("Tiny (40x25)",   new Size(40,  25)),
			("Small (60x40)",  new Size(60,  40)),
			("Normal (80x50)", new Size(80,  50)),
			("Large (120x75)", new Size(120, 75)),
			("Huge (160x100)", new Size(160, 100)),
			("Epic (320x200)", new Size(320, 200)),
		};

		private static readonly (string Question, string[] Options)[] Steps = new[]
		{
			("MAP SIZE",    new[] { "Tiny (40x25)", "Small (60x40)", "Normal (80x50)", "Large (120x75)", "Huge (160x100)", "Epic (320x200)" }),
			("LAND MASS",   new[] { "Small", "Normal", "Large" }),
			("TEMPERATURE", new[] { "Cool", "Temperate", "Warm" }),
			("CLIMATE",     new[] { "Arid", "Normal", "Wet" }),
			("AGE",         new[] { "3 billion years", "4 billion years", "5 billion years" }),
		};

		private int _step = 0;
		private int _cursor = 2;
		private readonly int[] _confirmed = new int[] { 2, 1, 1, 1, 1 };
		private bool _hasUpdate = true;
		private bool _closing = false;

		private Rectangle[] _optionRects;

		private int PanelW => 290;
		private int PanelH => 208;
		private int PanelX => (Width  - PanelW) / 2;
		private int PanelY => (Height - PanelH) / 2;

		protected override bool HasUpdate(uint gameTick)
		{
			if (_closing)
			{
				if (!HandleScreenFadeOut())
				{
					Destroy();
					Size sz = MapSizes[_confirmed[0]].Size;
					Map.Generate(_confirmed[1], _confirmed[2], _confirmed[3], _confirmed[4], sz.Width, sz.Height);
					if (!Runtime.Settings.ShowIntro)
						Common.AddScreen(new NewGame());
					else
						Common.AddScreen(new Intro());
				}
				return true;
			}

			if (!_hasUpdate) return false;
			_hasUpdate = false;

			Draw();
			return true;
		}

		private void Draw()
		{
			const int font = 0;
			int fh = Resources.GetFontHeight(font);
			int rowH = fh + 4;

			int pw = PanelW, ph = PanelH, px = PanelX, py = PanelY;

			this.FillRectangle(0, 0, Width, Height, CassetteTheme.BG0);
			this.DrawCassettePanel(px, py, pw, ph, "CUSTOMIZE WORLD");

			int cx = px + 12;
			int cw = pw - 24;
			int cy = py + 14;

			// Current step question header
			var (question, options) = Steps[_step];
			this.DrawText(question, font, CassetteTheme.PHOS, cx, cy);
			cy += fh + 5;

			// Option rows
			_optionRects = new Rectangle[options.Length];
			for (int i = 0; i < options.Length; i++)
			{
				bool selected = (i == _cursor);
				if (selected)
				{
					this.FillRectangle(cx - 3, cy - 1, cw + 6, rowH, CassetteTheme.PHOS_FAINT);
					this.DrawText("\x10 " + options[i], font, CassetteTheme.PHOS_GLOW, cx + 1, cy);
				}
				else
				{
					this.DrawText("  " + options[i], font, CassetteTheme.INK_HIGH, cx + 1, cy);
				}
				_optionRects[i] = new Rectangle(cx - 3, cy - 1, cw + 6, rowH);
				cy += rowH;
			}

			// Confirmed answers summary
			if (_step > 0)
			{
				cy += 4;
				this.DrawCassetteDivider(cx, cy, cw);
				cy += 5;

				for (int s = 0; s < _step; s++)
				{
					string lbl = Steps[s].Question;
					string val = Steps[s].Options[_confirmed[s]];
					this.DrawCassetteField(lbl, val, cx, cy, cw, font, CassetteTheme.PHOS_DIM);
					cy += fh + 4;
				}
			}

			// Footer
			int footerY = py + ph - fh - 6;
			this.DrawCassetteDivider(cx, footerY - 4, cw);
			this.DrawText("\x18\x19 Navigate   ENTER Select   ESC Back", font, CassetteTheme.INK_LOW,
				px + pw / 2, footerY, TextAlign.Center);
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			string[] options = Steps[_step].Options;
			switch (args.Key)
			{
				case Key.Up:
					_cursor = (_cursor - 1 + options.Length) % options.Length;
					_hasUpdate = true;
					return true;
				case Key.Down:
					_cursor = (_cursor + 1) % options.Length;
					_hasUpdate = true;
					return true;
				case Key.Enter:
					Confirm(_cursor);
					return true;
				case Key.Escape:
					GoBack();
					return true;
			}
			return false;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_optionRects == null) return false;
			for (int i = 0; i < _optionRects.Length; i++)
			{
				if (_optionRects[i].Contains(args.X, args.Y))
				{
					Confirm(i);
					return true;
				}
			}
			return false;
		}

		private void Confirm(int choice)
		{
			_confirmed[_step] = choice;
			_step++;
			if (_step >= Steps.Length)
			{
				_closing = true;
			}
			else
			{
				_cursor = _confirmed[_step];
			}
			_hasUpdate = true;
		}

		private void GoBack()
		{
			if (_step == 0)
			{
				Destroy();
				return;
			}
			_step--;
			_cursor = _confirmed[_step];
			_hasUpdate = true;
		}

		public CustomizeWorld() : base(MouseCursor.Pointer)
		{
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;
		}
	}
}
