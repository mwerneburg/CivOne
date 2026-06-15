#nullable enable
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
	internal class CommunicationsAdvisor : BaseReport
	{
		private struct RowHit
		{
			internal Rectangle Bounds;
			internal int       Index;   // index into _entries
		}

		private readonly List<TransmissionRecord> _entries;
		private readonly List<RowHit>             _rows = new();

		private static string DisplayTitle(string type) => type switch
		{
			"SETISignal"         => "SETI Signal Analysis",
			"SouthPoleIntel"     => "Classified Intelligence Report",
			"SouthPoleExpedition"=> "Expedition Log – South Pole Mission",
			"TauCetiApproach"    => "Tau Ceti — Approach Warning",
			"ProbeResult"        => "Tau Ceti — Probe Result",
			_                    => type
		};

		private void Replay(int index)
		{
			var entry = _entries[index];
			IScreen? screen = entry.Type switch
			{
				"SETISignal"          => new SETISignalTransmission(entry.Year),
				"SouthPoleIntel"      => new SouthPoleIntelReport(entry.Year),
				"SouthPoleExpedition" => new SouthPoleExpeditionLog(entry.Year),
				"TauCetiApproach"     => new TauCetiApproachWarning(entry.Year, Game.Instance.VisitorType),
				"ProbeResult"         => new ProbeResultTransmission(entry.Year, Game.Instance.VisitorType, Game.Instance.ProbeOutcomeTier),
				_                    => null
			};
			if (screen is not null)
				Common.AddScreen(screen);
		}

		private void HandleClick(object sender, ScreenEventArgs args)
		{
			foreach (var row in _rows)
			{
				if (!row.Bounds.Contains(args.X, args.Y)) continue;
				args.Handled = true;
				Replay(row.Index);
				return;
			}
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!base.HasUpdate(gameTick)) return false;

			_rows.Clear();

			int fh   = Resources.GetFontHeight(0);
			int rowH = fh + 4;
			int top  = 32;
			int bodyW = Width - 8;
			int x0   = OX + 4;

			// Divider below header
			this.DrawCassetteDivider(x0, top, bodyW);
			top += 4;

			if (_entries.Count == 0)
			{
				this.DrawText("No transmissions received.", 0, CassetteTheme.INK_MID,
				              Width / 2, top + 12, TextAlign.Center);
				this.DrawText("Transmissions appear here once five Observatories exist",
				              0, CassetteTheme.INK_LOW, Width / 2, top + 12 + fh + 4, TextAlign.Center);
				this.DrawText("world-wide, or the South Pole Expedition is built.",
				              0, CassetteTheme.INK_LOW, Width / 2, top + 12 + (fh + 4) * 2, TextAlign.Center);
				return true;
			}

			// Column headers
			int xYear  = x0;
			int xTitle = x0 + 68;
			int xPlay  = OX + bodyW - 24;

			this.DrawText("Year",         0, CassetteTheme.INK_LOW, xYear,  top);
			this.DrawText("Transmission", 0, CassetteTheme.INK_LOW, xTitle, top);
			this.DrawText("Replay",       0, CassetteTheme.INK_LOW, xPlay,  top, TextAlign.Center);
			top += rowH;
			this.DrawCassetteDivider(x0, top, bodyW);
			top += 3;

			// Rows (reverse chronological)
			int maxRows = (Height - top - 20) / rowH;
			int shown = _entries.Count < maxRows ? _entries.Count : maxRows;
			for (int i = 0; i < shown; i++)
			{
				var entry = _entries[i];
				var hit   = new Rectangle(x0, top - 1, bodyW, rowH);
				_rows.Add(new RowHit { Bounds = hit, Index = i });

				this.DrawText(entry.Year,              0, CassetteTheme.INK_MID,  xYear,  top);
				this.DrawText(DisplayTitle(entry.Type), 0, CassetteTheme.INK_HIGH, xTitle, top);
				this.DrawText("[>]",                   0, CassetteTheme.PHOS,     xPlay,  top, TextAlign.Center);
				top += rowH;
			}

			if (_entries.Count > shown)
				this.DrawText($"… and {_entries.Count - shown} more (scroll not yet supported)",
				              0, CassetteTheme.INK_LOW, Width / 2, top + 4, TextAlign.Center);

			// Footer hint
			this.DrawCassetteDivider(x0, Height - 18, bodyW);
			this.DrawText("Click a row to replay with typewriter animation  •  Any key to close",
			              0, CassetteTheme.INK_LOW, Width / 2, Height - 14, TextAlign.Center);

			return true;
		}

		internal CommunicationsAdvisor() : base("COMMUNICATIONS", CassetteTheme.BG0, MouseCursor.Pointer)
		{
			// Most recent first
			_entries = Game.Instance.Transmissions
				.AsEnumerable()
				.Reverse()
				.ToList();

			OnMouseDown += HandleClick;
		}
	}
}
