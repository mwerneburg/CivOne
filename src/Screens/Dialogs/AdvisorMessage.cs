// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Screens.Dialogs
{
	internal class AdvisorMessage : BaseDialog
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
			int maxWidth = TextBitmaps(message).Max(b => b.Width) + 16;
			return maxWidth < 140 ? 140 : maxWidth;
		}

		private static int DialogHeight(string[] message)
		{
			return 4 + 9 + 4 + TextBitmaps(message).Sum(b => b.Height) + 6;
		}

		public AdvisorMessage(Advisor advisor, string[] message, bool leftAlign) : base((leftAlign ? 38 : 58), 72, DialogWidth(message), DialogHeight(message))
		{
			string[] advisorNames = ["Defense Minister", "Domestic Advisor", "Foreign Minister", "Science Advisor"];

			_textLines = TextBitmaps(message);
			DialogBox.DrawText($"{advisorNames[(int)advisor]}:", 0, 15, 8, 4);
			DialogBox.FillRectangle(8, 11, Resources.GetText($"{advisorNames[(int)advisor]}:", 0, 15).Width + 1, 1, 11);
			for (int i = 0; i < _textLines.Length; i++)
				DialogBox.AddLayer(_textLines[i], 8, (_textLines[i].Height * i) + 13);
		}
	}
}
