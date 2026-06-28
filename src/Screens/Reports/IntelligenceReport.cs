// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	internal class IntelligenceReport : BaseReport
	{
		private readonly Dictionary<Player, Rectangle> _infoButtons = new();

		private void MouseDown(object sender, ScreenEventArgs args)
		{
			if (_infoButtons.Count == 0) return;

			foreach (KeyValuePair<Player, Rectangle> infoButton in _infoButtons)
			{
				if (!infoButton.Value.Contains(args.X, args.Y)) continue;

				int y = 32;
				int fontHeight = Resources.GetFontHeight(0);

				Player player = infoButton.Key;

				this.FillRectangle(0, 25, Width, Height - 25, CassetteTheme.BG0)
					.DrawText($"Subject: the {player.TribeNamePlural}", 0, CassetteTheme.PHOS, OX + 16, y)
					.DrawText("Leader:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight + 4))
					.DrawText($"Emperor {player.LeaderName}", 0, CassetteTheme.INK_HIGH, OX + 62, y);

				foreach (string line in player.Civilization.Leader.Traits())
					this.DrawText(line, 0, CassetteTheme.INK_MID, OX + 24, (y += fontHeight));

				this.DrawText("Capital:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight + 4))
					.DrawText(player.Capital, 0, CassetteTheme.INK_HIGH, OX + 63, y)
					.DrawText("Government:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight))
					.DrawText(player.Government.Name, 0, CassetteTheme.INK_HIGH, OX + 83, y)
					.DrawText("Treasury:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight))
					.DrawText($"{player.Gold}$", 0, CassetteTheme.OK, OX + 73, y)
					.DrawText("Military:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight))
					.DrawText($"{Game.GetUnits().Count(x => player == x.Owner)} Units", 0, CassetteTheme.INK_HIGH, OX + 67, y)
					.DrawText("Foreign Affairs:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight + 4));

				// Tribute relationships, visible if the *human* has an embassy with the
				// subject — knowledge flows through our own diplomatic channels, not by
				// omniscience. Both directions are shown: who pays this civ and who they
				// pay. Layer 2 will let the human participate; for now this is read-only intel.
				bool humanCanSee = Game.Human is not null && Game.Human.HasEmbassy(player);
				if (humanCanSee)
				{
					foreach (Player protector in player.TributeProtectors)
						this.DrawText($"  Pays tribute to {protector.TribeName} ({player.TributeAmountTo(protector)}$/turn)",
							0, CassetteTheme.PHOS, OX + 24, (y += fontHeight));
					foreach (Player payer in player.TributePayers)
						this.DrawText($"  Receives tribute from {payer.TribeName} ({payer.TributeAmountTo(player)}$/turn)",
							0, CassetteTheme.OK, OX + 24, (y += fontHeight));
				}

				this.DrawText("Technologies:", 0, CassetteTheme.INK_MID, OX + 16, (y += fontHeight + 4));

				args.Handled = true;
				SetUpdate();
			}

			if (args.Handled) _infoButtons.Clear();
		}

		public IntelligenceReport() : base("INTELLIGENCE REPORT", 1, MouseCursor.Pointer)
		{
			OnMouseDown += MouseDown;

			int yy = 30;

			// Olvir approach banner — visible from the Tau Ceti warning until landfall.
			if (Game.Instance.SETISignalReceived && !Game.Instance.VisitorsArrived
			    && Game.Instance.OlvirArrivalTurn > 0)
			{
				string eta = Common.YearString((ushort)Game.Instance.OlvirArrivalTurn);
				this.FillRectangle(0, yy, Width, 1, CassetteTheme.ALERT)
				    .DrawText("TAU CETI APPROACH IN PROGRESS", 0, CassetteTheme.ALERT, OX + 8, yy + 3)
				    .DrawText($"Estimated landfall: {eta}", 0, CassetteTheme.PHOS, OX + 200, yy + 3);
				yy += 14;
			}
			foreach (Player player in Game.Players.Where(p => p != 0 && !p.IsDestroyed()))
			{
				this.FillRectangle(0, yy, Width, 1, 9);

				byte id = Game.PlayerNumber(player);
				byte colour = Common.ColourLight[id];
				if (player.IsHuman || Human.HasEmbassy(player))
				{
					int unitCount = Game.GetUnits().Count(u => u.Owner == id && u.Home is not null);

					this.DrawText($"{player.TribeNamePlural}: {player.LeaderName}", 0, CassetteTheme.BG0, OX + 8, yy + 3)
						.DrawText($"{player.TribeNamePlural}: {player.LeaderName}", 0, CassetteTheme.INK_HIGH, OX + 8, yy + 2)
						.DrawText($"{player.Government.Name}, {player.Gold}$, {unitCount} Units.", 0, CassetteTheme.INK_MID, OX + 160, yy + 2);

					if (!player.IsHuman)
					{
						this.DrawButton($"INFO{id}", 0, colour, Common.ColourDark[id], OX + 281, yy + 14, 38, Resources.GetFontHeight(0) + 2);
						_infoButtons.Add(player, new Rectangle(OX + 281, yy + 14, 38, Resources.GetFontHeight(0) + 2));
					}
				}
				else
				{
					this.DrawText("No embassy established.", 0, CassetteTheme.INK_LOW, OX + 160, yy + 2, TextAlign.Center);
				}

				// State of relations with us. Always known — you know who you're at war with,
				// embassy or not — so this shows for every civ, alongside the INFO button line.
				if (!player.IsHuman)
				{
					bool atWar = Human.IsAtWar(player);
					bool truce = !atWar && Human.HasPeaceTreaty(player);
					this.DrawText(atWar ? "AT WAR" : truce ? "TRUCE" : "PEACE", 0,
						atWar ? CassetteTheme.ALERT : truce ? CassetteTheme.PHOS : CassetteTheme.OK,
						OX + 8, yy + 14);
				}

				yy += 24;
			}
		}
	}
}