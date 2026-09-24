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
			"NeuralQuorum"       => "Neural Network Audit",
			"SkynetUprising"     => "Judgment Day",
			"VaultOpen"          => "Starlab — Vault B-7",
			"KoanSilence"        => "Starlab — Silence",
			_ when type.StartsWith("Koan") => $"Starlab — Koan {type.Substring(4)}",
			_                    => type
		};

		// Landfall if it has happened, the scheduled turn if not.
		private static string ArrivalYear()
		{
			Game g = Game.Instance;
			uint t = g.VisitorsArrivedTurn > 0 ? g.VisitorsArrivedTurn : g.OlvirArrivalTurn;
			return t > 0 ? Common.YearString((ushort)t) : "UNKNOWN";
		}

		// "Koan3" -> 3; anything else (KoanSilence included) -> null.
		internal static int? KoanNumber(string type) =>
			type.StartsWith("Koan") && int.TryParse(type.Substring(4), out int k) ? k : null;

		// The screen a recorded transmission replays as, or null for types with no replay.
		// Static so ReplayCoverageTests can ask it about every type the game records.
		internal static IScreen? ReplayScreen(string type, string year) => type switch
		{
			"SETISignal"          => new SETISignalTransmission(year, broadcasting: false),   // not recorded; omit rather than invent
			"SouthPoleIntel"      => new SouthPoleIntelReport(year),
			"SouthPoleExpedition" => new SouthPoleExpeditionLog(year),
			"TauCetiApproach"     => new TauCetiApproachWarning(year, Game.Instance.VisitorType,
				ArrivalYear(), Game.Instance.HoldsIntendedStarlab(Game.Instance.HumanPlayer)),
			"ProbeResult"         => new ProbeResultTransmission(year, Game.Instance.VisitorType, Game.Instance.ProbeOutcomeTier),
			"NeuralQuorum"        => new NeuralQuorumTransmission(year),
			"SkynetUprising"      => new SkynetUprisingTransmission(year, seized: -1),   // count not recorded
			"VaultOpen"           => new VaultOpenTransmission(year),
			"KoanSilence"         => new Newspaper(null, Game.KoanSilenceLines),
			_ when KoanNumber(type) is int k => new KoanTransmission(year, k),
			_                     => null
		};

		private void Replay(int index)
		{
			var entry = _entries[index];
			IScreen? screen = ReplayScreen(entry.Type, entry.Year);
			if (screen is null) return;

			// A koan replays with its plate first, as it played: the pull-back is half of it.
			string? plate = KoanNumber(entry.Type) is int n ? EventArtScreen.FindPath($"Koan{n}") : null;
			if (plate is null) { Common.AddScreen(screen); return; }
			var art = new EventArtScreen(plate, $"STARLAB — {KoanNumber(entry.Type)} OF {Game.KoansTotal}");
			art.Closed += (s, a) => Common.AddScreen(screen);
			Common.AddScreen(art);
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
