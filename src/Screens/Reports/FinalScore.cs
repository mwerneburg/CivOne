#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	internal class FinalScore : BaseReport
	{
		private readonly string _victoryType;
		private readonly int    _score;
		private readonly string _year;
		private bool _drawn;

		private static string RatingTitle(int score)
		{
			if (score >= 300) return "DEITY";
			if (score >= 200) return "EMPEROR";
			if (score >= 150) return "KING";
			if (score >= 100) return "PRINCE";
			if (score >=  50) return "WARLORD";
			return "CHIEFTAIN";
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (_drawn) return false;
			_drawn = true;

			int cx = Width / 2;
			int top = 36;

			// Victory / defeat banner
			bool won = _victoryType != "Defeated";
			byte bannerCol = won ? CassetteTheme.OK : CassetteTheme.ALERT;
			this.DrawText(won ? "VICTORY!" : "DEFEATED", 1, bannerCol, cx, top, TextAlign.Center);
			top += Resources.GetFontHeight(1) + 8;

			// Score (large)
			this.DrawText(_score.ToString(), 1, CassetteTheme.PHOS_GLOW, cx, top, TextAlign.Center);
			top += Resources.GetFontHeight(1) + 4;

			this.DrawText("POINTS", 0, CassetteTheme.INK_MID, cx, top, TextAlign.Center);
			top += Resources.GetFontHeight(0) + 10;

			// Rating
			string rating = RatingTitle(_score);
			this.DrawText("Rating:", 0, CassetteTheme.INK_LOW, cx - 2, top, TextAlign.Right);
			this.DrawText(rating, 0, CassetteTheme.PHOS, cx + 2, top);
			top += Resources.GetFontHeight(0) + 4;

			// Victory type
			this.DrawText("Outcome:", 0, CassetteTheme.INK_LOW, cx - 2, top, TextAlign.Right);
			this.DrawText(_victoryType, 0, CassetteTheme.INK_HIGH, cx + 2, top);
			top += Resources.GetFontHeight(0) + 4;

			// Year
			this.DrawText("Year:", 0, CassetteTheme.INK_LOW, cx - 2, top, TextAlign.Right);
			this.DrawText(_year, 0, CassetteTheme.INK_HIGH, cx + 2, top);
			top += Resources.GetFontHeight(0) + 14;

			// Leader / tribe
			this.DrawText($"{Human.LeaderName} of the {Human.TribeNamePlural}", 0, CassetteTheme.INK_MID, cx, top, TextAlign.Center);

			// Footer hint
			this.DrawCassetteDivider(4, Height - 18, Width - 8);
			this.DrawText("Press any key to continue", 0, CassetteTheme.INK_LOW, cx, Height - 14, TextAlign.Center);

			return true;
		}

		internal FinalScore(string victoryType) : base("FINAL SCORE", CassetteTheme.BG0, MouseCursor.Pointer)
		{
			_victoryType = victoryType;
			_score = Human.Score;
			_year  = Common.YearString(Game.Instance.GameTurn);
		}
	}
}
