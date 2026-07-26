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
using CivOne.IO;

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

		// Ministry badge on the left, with the title and text indented past it. Null
		// when advisor_badges.txt is absent, in which case the dialog keeps its
		// original text-only layout rather than reserving empty space.
		private static Bytemap? Badge(Advisor advisor)
		{
			string[] names = ["defense", "domestic", "foreign", "science"];
			return Free.Instance.AdvisorBadge(names[(int)advisor]);
		}

		private const int BadgeMargin = 4;
		private static int TextLeft(Advisor advisor) =>
			Badge(advisor) is null ? 8 : BadgeMargin + Free.BadgeSize + BadgeMargin;

		private static int DialogWidth(string[] message, Advisor advisor)
		{
			int maxWidth = TextBitmaps(message).Max(b => b.Width) + 8 + TextLeft(advisor);
			return maxWidth < 140 ? 140 : maxWidth;
		}

		private static int DialogHeight(string[] message, Advisor advisor)
		{
			int textHeight = 4 + 9 + 4 + TextBitmaps(message).Sum(b => b.Height) + 6;
			if (Badge(advisor) is null) return textHeight;
			// Never shorter than the badge it has to hold.
			int badgeHeight = BadgeMargin + Free.BadgeSize + BadgeMargin;
			return textHeight < badgeHeight ? badgeHeight : textHeight;
		}

		public AdvisorMessage(Advisor advisor, string[] message, bool leftAlign) : base((leftAlign ? 38 : 58), 72, DialogWidth(message, advisor), DialogHeight(message, advisor))
		{
			string[] advisorNames = ["Defense Minister", "Domestic Advisor", "Foreign Minister", "Science Advisor"];

			Bytemap? badge = Badge(advisor);
			if (badge is not null)
				DialogBox.AddLayer(badge, BadgeMargin, BadgeMargin);

			int x = TextLeft(advisor);
			_textLines = TextBitmaps(message);
			DialogBox.DrawText($"{advisorNames[(int)advisor]}:", 0, 15, x, 4);
			DialogBox.FillRectangle(x, 11, Resources.GetText($"{advisorNames[(int)advisor]}:", 0, 15).Width + 1, 1, 11);
			for (int i = 0; i < _textLines.Length; i++)
				DialogBox.AddLayer(_textLines[i], x, (_textLines[i].Height * i) + 13);
		}
	}
}
