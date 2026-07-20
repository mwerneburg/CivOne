// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.IO;

namespace CivOne.Screens.Dialogs
{
	internal class SpyMessage : BaseDialog
	{
		private readonly Picture[] _textLines;

		private static Picture[] TextBitmaps(string[] message)
		{
			Picture[] output = new Picture[message.Length];
			for (int i = 0; i < message.Length; i++)
				output[i] = Resources.GetText(message[i], 0, 15);
			return output;
		}

		private static int DialogWidth(string[] message)
		{
			return TextBitmaps(message).Max(b => b.Width) + 50;
		}

		public SpyMessage(string[] message) : base(38, 72, DialogWidth(message), 57)
		{
			IBitmap spyPortrait = Icons.Spy;

			using Palette palette = Common.DefaultPalette;
			using (Palette cass = CassetteTheme.CreatePalette())
				palette.MergePalette(cass, 1, 17);
			// Bring in ONLY the palette entries the spy portrait's pixels actually use,
			// not a blanket 144-255 copy. The wholesale copy clobbered the map's colours
			// in that range — which now include custom high-index terrain entries (the
			// desert ramp at 233/236/238/250/251, etc.) — blowing out the map behind the
			// popup. Copying just the used indices leaves the rest of the palette intact.
			Bytemap bmp = spyPortrait.Bitmap;
			bool[] used = new bool[256];
			for (int yy = 0; yy < bmp.Height; yy++)
			for (int xx = 0; xx < bmp.Width; xx++)
				used[bmp[xx, yy]] = true;
			for (int i = 144; i < 256; i++)
				if (used[i]) palette[i] = spyPortrait.Palette[i];
			this.SetPalette(palette);
			
			_textLines = TextBitmaps(message);
			DialogBox.AddLayer(spyPortrait, 2, 2);
			for (int i = 0; i < _textLines.Length; i++)
				DialogBox.AddLayer(_textLines[i], 47, (_textLines[i].Height * (i -1)) + 13);
		}
	}
}
