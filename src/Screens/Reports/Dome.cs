// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Wonders;

namespace CivOne.Screens.Reports
{
	[Expand, OwnPalette]
	internal class Dome : BaseScreen
	{
		private bool _update = true;

		private static string ComponentName(Wonder id) => id switch
		{
			Wonder.DomeEmitterArray => "EMITTER ARRAY",
			Wonder.DomeSensorNet    => "SENSOR NET",
			Wonder.DomePowerCore    => "POWER CORE",
			Wonder.DomeCommandHub   => "COMMAND HUB",
			Wonder.DomeKineticRing  => "KINETIC RING",
			_                       => id.ToString().ToUpper()
		};

		private static City FindBuildingCity(Player player, Wonder wonderId)
			=> player.Cities.FirstOrDefault(c =>
				c.CurrentProduction is IDomeComponent d &&
				((IWonder)d).Id == (byte)wonderId);

		private void DrawScreen()
		{
			int W = Width, H = Height;
			const int headerH = 20;
			const int footerH = 12;
			int contentY = headerH + 6;

			// Scanline background
			this.FillRectangle(0, 0, W, H, CassetteTheme.BG0);
			for (int y = 0; y < H; y += 2)
				this.FillRectangle(0, y, W, 1, CassetteTheme.BG1);

			// Header
			this.FillRectangle(0, 0, W, headerH, CassetteTheme.BG3)
			    .FillRectangle(0, headerH - 1, W, 1, CassetteTheme.BORDER);
			this.DrawText("PLANETARY DOME", 0, CassetteTheme.PHOS_GLOW, 4, 3);
			this.DrawText($"· {Game.Instance.GameYear} ·", 0, CassetteTheme.INK_MID, W / 2, 3, TextAlign.Center);
			this.DrawText("ESC · CLOSE", 0, CassetteTheme.INK_LOW, W - 4, 3, TextAlign.Right);

			// Sub-header divider
			this.DrawText("COMPONENT", 0, CassetteTheme.INK_LOW, 6, contentY);
			this.DrawText("ASSIGNED TO", 0, CassetteTheme.INK_LOW, W / 2, contentY, TextAlign.Center);
			this.DrawText("STATUS", 0, CassetteTheme.INK_LOW, W - 6, contentY, TextAlign.Right);
			contentY += 8;
			this.FillRectangle(4, contentY, W - 8, 1, CassetteTheme.BORDER);
			contentY += 4;

			// Build reverse lookup: component wonder id → player id
			var componentOwner = new Dictionary<Wonder, byte>();
			foreach (var kv in Game.Instance.DomeAssignments)
				foreach (var w in kv.Value)
					componentOwner[w] = kv.Key;

			int builtCount = 0;
			int total      = Game.DomeFiveComponents.Length;
			int fh         = 8;

			foreach (var comp in Game.DomeFiveComponents)
			{
				Wonder wonderId   = (Wonder)comp.Id;
				bool built        = Game.Instance.WonderBuilt(comp);
				if (built) builtCount++;

				bool hasOwner     = componentOwner.TryGetValue(wonderId, out byte pid);
				Player? player     = hasOwner ? Game.GetPlayer(pid) : null;
				bool isHuman      = hasOwner && player == Human;
				City? building     = (built || !hasOwner) ? null : FindBuildingCity(player!, wonderId);

				byte nameColor = isHuman ? CassetteTheme.PHOS : CassetteTheme.INK_HIGH;
				byte statColor = built        ? CassetteTheme.OK
				               : building != null ? CassetteTheme.PHOS
				               : CassetteTheme.INK_LOW;

				string compLabel = ComponentName(wonderId);
				string civLabel  = hasOwner ? player!.TribeNamePlural.ToUpper() : "UNASSIGNED";
				string statusStr = built          ? "COMPLETE"
				                 : building != null ? building.Name.ToUpper()
				                 : "NOT STARTED";

				if (isHuman)
					this.FillRectangle(2, contentY - 1, W - 4, fh + 1, CassetteTheme.BG2);

				this.DrawText(compLabel, 0, nameColor, 6, contentY);
				this.DrawText(civLabel, 0, CassetteTheme.INK_MID, W / 2, contentY, TextAlign.Center);
				this.DrawText(statusStr, 0, statColor, W - 6, contentY, TextAlign.Right);

				contentY += fh + 4;
				this.FillRectangle(4, contentY - 3, W - 8, 1, CassetteTheme.BG2);
			}

			// Overall completion bar
			int barY = H - footerH - 12;
			float pct = (float)builtCount / total;
			int barW = W - 16;
			int filledW = (int)(barW * pct);
			this.FillRectangle(8, barY, barW, 6, CassetteTheme.BG2)
			    .FillRectangle(8, barY, filledW, 6, pct >= 1f ? CassetteTheme.OK : CassetteTheme.PHOS);
			string pctLabel = $"DOME COMPLETION  {builtCount}/{total}  ({(int)(pct * 100)}%)";
			this.DrawText(pctLabel, 0, pct >= 1f ? CassetteTheme.OK : CassetteTheme.INK_MID, W / 2, barY + 8, TextAlign.Center);

			// Footer
			int fy = H - footerH;
			this.FillRectangle(0, fy, W, 1, CassetteTheme.BORDER)
			    .FillRectangle(0, fy + 1, W, footerH - 1, CassetteTheme.BG3);
			string hint = pct >= 1f ? "ALL COMPONENTS COMPLETE" : "Send caravans to assist allied dome cities";
			this.DrawText(hint, 0, pct >= 1f ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_LOW, W / 2, fy + 2, TextAlign.Center);
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;
			DrawScreen();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		public Dome() : base(MouseCursor.Pointer)
		{
			using Palette pal = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				pal.MergePalette(cassette, 1, 17);
			Palette = pal;
			this.Clear(0);
		}
	}
}
