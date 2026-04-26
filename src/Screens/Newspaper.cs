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
using CivOne.IO;
using CivOne.Graphics;
using CivOne.Graphics.Sprites;

namespace CivOne.Screens
{
	[Expand]
	internal class Newspaper : BaseScreen
	{
		private const int PaperW = 320;
		private const int PaperH = 200;

		private bool _update = true;

		// All content stored so Resize() can re-render from scratch.
		private readonly string[] _message;
		private readonly bool _showGovernment;
		private readonly bool _modernGovernment;
		private readonly string _newsflash;
		private readonly string _shout;
		private readonly string _date;
		private readonly string _paperName;

		// ─── rendering ──────────────────────────────────────────────────────────

		private void PaperOffset(out int ox, out int oy)
		{
			ox = (Width  > PaperW) ? (Width  - PaperW) / 2 : 0;
			oy = (Height > PaperH) ? (Height - PaperH) / 2 : 0;
		}

		private void Render()
		{
			PaperOffset(out int ox, out int oy);

			Palette palette = Common.DefaultPalette;

			IBitmap[] portraits = new IBitmap[4];
			if (_showGovernment)
			{
				for (int i = 0; i < 4; i++)
					portraits[i] = Icons.GovernmentPortrait(Human.Government,
						(Advisor)Enum.Parse(typeof(Advisor), i.ToString()), _modernGovernment);
				for (int i = 144; i < 256; i++)
					palette[i] = portraits[0].Palette[i];
			}

			Palette = palette;

			// Clear canvas then draw the newspaper centred on it
			this.FillRectangle(0, 0, Width, Height, 0);

			this.FillRectangle(ox, oy, PaperW, 100, 15)
				.DrawText(_shout, 2, 5, ox + 6,   oy + 3)
				.DrawText(_shout, 2, 5, ox + 272,  oy + 3)
				.DrawText(_newsflash, 1, 5, ox + 158, oy + 3, TextAlign.Center)
				.DrawText(_newsflash, 1, 5, ox + 158, oy + 3, TextAlign.Center)
				.DrawText(",-.", 4, 5, ox + 8,   oy + 11)
				.DrawText(",-.", 4, 5, ox + 268,  oy + 11)
				.DrawText(_paperName, 4, 5, ox + 160, oy + 11, TextAlign.Center)
				.DrawText(_date,     0, 5, ox + 8,   oy + 28)
				.DrawText("10 cents", 0, 5, ox + 272, oy + 28)
				.FillRectangle(ox + 1,   oy + 1,  318, 1, 5)
				.FillRectangle(ox + 1,   oy + 2,  1, 33, 5)
				.FillRectangle(ox + 318, oy + 2,  1, 33, 5)
				.FillRectangle(ox,       oy + 35, PaperW, 1, 5)
				.FillRectangle(ox,       oy + 97, PaperW, 1, 5);

			for (int i = 0; i < _message.Length; i++)
				this.DrawText(_message[i], 3, 5, ox + 16, oy + 40 + (i * 17));

			if (_showGovernment)
			{
				string[] advisorNames = { "Defense Minister", "Domestic Advisor", "Foreign Minister", "Science Advisor" };
				this.FillRectangle(ox, oy + 100, PaperW, 100, 15)
					.DrawText("New Cabinet:", 5, 5, ox + 106, oy + 102);
				for (int i = 0; i < 4; i++)
					this.AddLayer(portraits[i], ox + 20 + (80 * i), oy + 118)
						.DrawText(advisorNames[i], 1, 5, ox + 40 + (80 * i),
							oy + ((i % 2) == 0 ? 180 : 186), TextAlign.Center);
			}
			else
			{
				for (int xx = ox; xx < ox + PaperW; xx += Icons.Newspaper.Width())
					this.AddLayer(Icons.Newspaper, xx, oy + 100);

				using (IBitmap dialog = new Picture(151, 15)
					.Tile(Pattern.PanelGrey)
					.DrawRectangle3D()
					.DrawText("Press any key to continue.", 0, 15, 4, 4))
				{
					this.FillRectangle(ox + 80, oy + 128, 153, 17, 5)
						.AddLayer(dialog, ox + 81, oy + 129);
				}
			}
		}

		private void OnResizeHandler(object sender, Events.ResizeEventArgs args)
		{
			Render();
			_update = true;
		}

		// ─── update / input ─────────────────────────────────────────────────────

		protected override bool HasUpdate(uint gameTick)
		{
			if (_update)
			{
				_update = false;
				return true;
			}
			return false;
		}

		public void Close() => Destroy();

		public override bool KeyDown(KeyboardEventArgs args) { Close(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Close(); return true; }

		// ─── constructor ────────────────────────────────────────────────────────

		public Newspaper(City city, string[] message, bool showGovernment = false)
		{
			_message        = message;
			_showGovernment = showGovernment;
			_modernGovernment = Human.HasAdvance<Invention>();

			_newsflash = TextFile.Instance.GetGameText($"KING/NEWS{(char)Common.Random.Next((int)'A', (int)'O')}")[0];
			_shout     = (Common.Random.Next(0, 2) == 0) ? "FLASH" : "EXTRA!";
			_date      = $"January 1, {Common.YearString(Game.GameTurn)}";

			string cityName = city?.Name ?? (Human.Cities.Length > 0 ? Human.Cities[0].Name : "NONE");
			_paperName = Common.Random.Next(0, 3) switch
			{
				0 => $"The {cityName} Times",
				1 => $"The {cityName} Tribune",
				_ => $"{cityName} Weekly"
			};

			Palette = Common.DefaultPalette;
			Render();
			OnResize += OnResizeHandler;
		}
	}
}
