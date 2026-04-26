// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	[Modal]
	internal class CivilizationScore : BaseReport
	{
		private bool _update = true;

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			var players = Game.Players
				.Where(p => !(p.Civilization is Barbarian))
				.OrderByDescending(p => p.Score)
				.ThenBy(p => p == Human ? 0 : 1)
				.ToArray();

			for (int i = 0; i < players.Length; i++)
			{
				Player p = players[i];
				int yy = 28 + i * 20;
				bool isHuman = (p == Human);
				byte fg = isHuman ? CassetteTheme.PHOS_GLOW : Common.ColourLight[(byte)p];

				if (isHuman)
					this.FillRectangle(8, yy - 2, 304, 18, Common.ColourDark[(byte)p]);

				string name = $"{i + 1}. {p.TribeNamePlural} ({p.LeaderName})";
				this.DrawText(name, 0, fg, 12, yy)
				    .DrawText(p.Score.ToString(), 0, fg, 308, yy, TextAlign.Right);
			}

			return true;
		}

		public CivilizationScore() : base("CIVILIZATION SCORE", 3)
		{
		}
	}
}
