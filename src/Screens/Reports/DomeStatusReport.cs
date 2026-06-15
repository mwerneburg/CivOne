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
using CivOne.Wonders;

namespace CivOne.Screens.Reports
{
	internal class DomeStatusReport : BaseReport
	{
		private const int INFUSE_COST  = 200; // gold spent
		private const int INFUSE_YIELD = 50;  // shields added

		private struct ComponentRow
		{
			internal Rectangle Bounds;
			internal Enums.Wonder Component;
			internal byte?        AssignedOwner; // null if unassigned
		}

		private readonly List<ComponentRow> _rows = new();

		private static readonly Enums.Wonder[] _components =
		{
			Enums.Wonder.DomeEmitterArray,
			Enums.Wonder.DomeSensorNet,
			Enums.Wonder.DomePowerCore,
			Enums.Wonder.DomeCommandHub,
			Enums.Wonder.DomeKineticRing,
		};

		private string ComponentName(Enums.Wonder w) => w switch
		{
			Enums.Wonder.DomeEmitterArray => "Dome Emitter Array",
			Enums.Wonder.DomeSensorNet    => "Dome Sensor Net",
			Enums.Wonder.DomePowerCore    => "Dome Power Core",
			Enums.Wonder.DomeCommandHub   => "Dome Command Hub",
			Enums.Wonder.DomeKineticRing  => "Dome Kinetic Ring",
			_                             => w.ToString(),
		};

		// Returns (owner, city) for the city currently building or having built this component.
		private (Player? owner, City? city, bool built) FindComponent(Enums.Wonder w)
		{
			byte wonderId = (byte)w;
			foreach (City c in Game.Instance.GetCities())
			{
				// Already built
				if (c.Wonders.Any(wr => wr.Id == wonderId))
					return (Game.GetPlayer(c.Owner), c, true);
				// Under construction
				if (c.CurrentProduction is IWonder cw && cw.Id == wonderId)
					return (Game.GetPlayer(c.Owner), c, false);
			}
			return (null, null, false);
		}

		private void TryInfuse(Enums.Wonder component)
		{
			if (Human.Gold < INFUSE_COST) return;

			var (_, city, built) = FindComponent(component);
			if (city is null || built) return;
			if (Game.GetPlayer(city.Owner) == Human) return; // can't self-infuse

			Human.Gold -= INFUSE_COST;
			city.Shields = (short)(city.Shields + INFUSE_YIELD);
			SetUpdate();
		}

		private void HandleClick(object sender, ScreenEventArgs args)
		{
			foreach (var row in _rows)
			{
				if (!row.Bounds.Contains(args.X, args.Y)) continue;
				args.Handled = true;
				TryInfuse(row.Component);
				return;
			}
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!base.HasUpdate(gameTick)) return false;

			_rows.Clear();

			int fh   = Resources.GetFontHeight(0);
			int rowH = fh + 6;
			int top  = 32;
			int x0   = OX + 4;
			int bodyW = Width - 8;

			this.DrawCassetteDivider(x0, top, bodyW);
			top += 4;

			if (Game.Instance.DomeAssignments.Count == 0)
			{
				this.DrawText("The Tau Ceti approach warning has not been received yet.",
				              0, CassetteTheme.INK_MID, Width / 2, top + 12, TextAlign.Center);
				this.DrawText("Dome component assignments are made when the warning fires.",
				              0, CassetteTheme.INK_LOW, Width / 2, top + 12 + rowH, TextAlign.Center);
				return true;
			}

			// Column headers
			this.DrawText("Component",   0, CassetteTheme.INK_LOW, x0,          top);
			this.DrawText("Assigned To", 0, CassetteTheme.INK_LOW, x0 + 130,    top);
			this.DrawText("Progress",    0, CassetteTheme.INK_LOW, x0 + 230,    top);
			this.DrawText("Infuse $200", 0, CassetteTheme.INK_LOW, OX + bodyW - 60, top, TextAlign.Center);
			top += rowH - 2;
			this.DrawCassetteDivider(x0, top, bodyW);
			top += 4;

			int builtCount = 0;
			foreach (Enums.Wonder w in _components)
			{
				var (owner, city, built) = FindComponent(w);
				byte? assignedOwner = null;
				string assignedName = "—";

				foreach (var kv in Game.Instance.DomeAssignments)
				{
					if (kv.Value.Contains(w))
					{
						assignedOwner = kv.Key;
						assignedName = Game.GetPlayer(kv.Key)?.Civilization?.Name ?? "Unknown";
						break;
					}
				}

				byte nameColour = built ? CassetteTheme.OK : CassetteTheme.INK_HIGH;
				this.DrawText(ComponentName(w), 0, nameColour, x0, top);
				this.DrawText(assignedName,     0, CassetteTheme.INK_MID, x0 + 130, top);

				// Progress
				string progress;
				byte progressColour;
				if (built)
				{
					progress = "Complete";
					progressColour = CassetteTheme.OK;
					builtCount++;
				}
				else if (city is not null)
				{
					int pct = city.Shields * 100 / (30 * 10); // price*10 = total shields
					progress = $"{pct}%";
					progressColour = CassetteTheme.PHOS;
				}
				else
				{
					progress = "Not started";
					progressColour = CassetteTheme.INK_LOW;
				}
				this.DrawText(progress, 0, progressColour, x0 + 230, top);

				// Infuse button (only for incomplete AI components)
				bool canInfuse = !built && city is not null &&
				                 Game.GetPlayer(city.Owner) != Human &&
				                 Human.Gold >= INFUSE_COST;
				string infuseLabel = canInfuse ? "[+50]" : (built ? "—" : "");
				byte infuseColour  = canInfuse ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_LOW;
				this.DrawText(infuseLabel, 0, infuseColour, OX + bodyW - 60, top, TextAlign.Center);

				_rows.Add(new ComponentRow
				{
					Bounds        = new Rectangle(x0, top - 1, bodyW, rowH),
					Component     = w,
					AssignedOwner = assignedOwner,
				});
				top += rowH;
			}

			this.DrawCassetteDivider(x0, top + 2, bodyW);
			top += 8;

			string summary = builtCount == 5
				? "ALL COMPONENTS ONLINE — DOME OPERATIONAL"
				: $"{builtCount} of 5 components complete";
			byte summaryColour = builtCount == 5 ? CassetteTheme.OK : CassetteTheme.INK_MID;
			this.DrawText(summary, 0, summaryColour, Width / 2, top, TextAlign.Center);

			this.DrawCassetteDivider(x0, Height - 18, bodyW);
			this.DrawText("Click [+50] to spend $200 gold adding 50 shields to an AI dome project  •  Any key closes",
			              0, CassetteTheme.INK_LOW, Width / 2, Height - 14, TextAlign.Center);

			return true;
		}

		internal DomeStatusReport() : base("DOME STATUS", CassetteTheme.BG0, MouseCursor.Pointer)
		{
			OnMouseDown += HandleClick;
		}
	}
}
