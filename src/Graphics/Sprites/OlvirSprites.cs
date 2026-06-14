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
using CivOne.IO;

namespace CivOne.Graphics.Sprites
{
	// 16×16 pixel-art overlays for Olvir land-use improvements.
	// Index 0 = transparent (terrain shows through).
	internal static class OlvirSprites
	{
		// Palette aliases (CassetteTheme indices)
		private const byte GW  = CassetteTheme.PHOS_GLOW;   // 13  #f8c060 bright amber
		private const byte PH  = CassetteTheme.PHOS;        // 12  #f0a030 amber
		private const byte PD  = CassetteTheme.PHOS_DIM;    // 11  #c07818 dim amber
		private const byte IM  = CassetteTheme.INK_MID;     // 7   #b39c72 warm tan
		private const byte IL  = CassetteTheme.INK_LOW;     // 6   #6a5a3c muted tan
		private const byte IH  = CassetteTheme.INK_HIGH;    // 8   #f4e6c8 off-white
		private const byte OK  = CassetteTheme.OK;          // 14  #5db536 green
		private const byte CY  = CassetteTheme.CYAN;        // 17  #3aaccc cyan/blue
		private const byte BG  = CassetteTheme.BG0;         // 1   near-black

		// ── SettlementCluster ───────────────────────────────────────────────
		// Three small amber domes: two on top, one centred below.
		private static Bytemap GetSettlementCluster()
		{
			var b = new Bytemap(16, 16);

			// Left dome (centred x=4)
			b[4,  2] = GW;
			b[3,  3] = PH; b[4, 3] = PH; b[5, 3] = PH;
			b[3,  4] = PD; b[4, 4] = PD; b[5, 4] = PD;

			// Right dome (centred x=11)
			b[11, 2] = GW;
			b[10, 3] = PH; b[11, 3] = PH; b[12, 3] = PH;
			b[10, 4] = PD; b[11, 4] = PD; b[12, 4] = PD;

			// Bottom centre dome (centred x=7-8)
			b[7,  8] = GW; b[8,  8] = GW;
			b[6,  9] = PH; b[7,  9] = PH; b[8,  9] = PH; b[9,  9] = PH;
			b[6, 10] = PD; b[7, 10] = PD; b[8, 10] = PD; b[9, 10] = PD;

			return b;
		}

		// ── Aquafarm ────────────────────────────────────────────────────────
		// CYAN water-channel grid: two vertical + two horizontal channels.
		private static Bytemap GetAquafarm()
		{
			var b = new Bytemap(16, 16);

			// Horizontal channels
			for (int x = 2; x <= 13; x++) { b[x, 5] = CY; b[x, 10] = CY; }
			// Vertical channels
			for (int y = 2; y <= 13; y++) { b[5, y] = CY; b[10, y] = CY; }
			// Bright intersections
			b[5, 5] = IH; b[10, 5] = IH; b[5, 10] = IH; b[10, 10] = IH;

			return b;
		}

		// ── CanopyArray ─────────────────────────────────────────────────────
		// Three horizontal green canopy slats with amber support pillars.
		private static Bytemap GetCanopyArray()
		{
			var b = new Bytemap(16, 16);

			int[] slats = { 3, 7, 11 };
			foreach (int y in slats)
				for (int x = 2; x <= 13; x++)
					b[x, y] = OK;

			// Support pillars (dim amber) between slats
			int[] pillars = { 3, 7, 12 };
			foreach (int x in pillars)
			{
				b[x, 4] = PD; b[x, 5] = PD; b[x, 6] = PD;
				b[x, 8] = PD; b[x, 9] = PD; b[x, 10] = PD;
			}

			return b;
		}

		// ── RepairBay ───────────────────────────────────────────────────────
		// Rectangular bay (INK_MID outline) with amber diagnostic beacons.
		private static Bytemap GetRepairBay()
		{
			var b = new Bytemap(16, 16);

			// Outer rectangle outline
			for (int x = 3; x <= 12; x++) { b[x, 3] = IM; b[x, 12] = IM; }
			for (int y = 3; y <= 12; y++) { b[3, y] = IM; b[12, y] = IM; }

			// Corner beacons (amber glow)
			b[4,  4] = GW; b[11,  4] = GW;
			b[4, 11] = GW; b[11, 11] = GW;

			// Central diagnostic panel (dim amber cross)
			b[7, 7] = PH; b[8, 7] = PH;
			b[7, 8] = PH; b[8, 8] = PH;
			b[6, 7] = PD; b[9, 7] = PD;
			b[7, 6] = PD; b[7, 9] = PD;
			b[8, 6] = PD; b[8, 9] = PD;
			b[6, 8] = PD; b[9, 8] = PD;

			return b;
		}

		// ── ExchangeNode ────────────────────────────────────────────────────
		// Central amber hub with four radiating connector lines.
		private static Bytemap GetExchangeNode()
		{
			var b = new Bytemap(16, 16);

			// Radiating spokes (dim amber)
			for (int i = 2; i <= 5; i++) b[7, i] = PD;       // north
			for (int i = 9; i <= 13; i++) b[7, i] = PD;      // south
			for (int i = 2; i <= 5; i++) b[i, 7] = PD;       // west
			for (int i = 9; i <= 13; i++) b[i, 7] = PD;      // east

			// Spoke tips (bright amber)
			b[7, 2] = GW; b[7, 13] = GW;
			b[2, 7] = GW; b[13, 7] = GW;

			// Central diamond hub (phosphor)
			b[7, 5] = PH; b[7, 9] = PH;
			b[5, 7] = PH; b[9, 7] = PH;
			b[6, 6] = PH; b[8, 6] = PH;
			b[6, 8] = PH; b[8, 8] = PH;
			b[7, 7] = GW; b[6, 7] = GW; b[8, 7] = GW;
			b[7, 6] = GW; b[7, 8] = GW;

			return b;
		}

		// ── BiofilterWall ───────────────────────────────────────────────────
		// Alternating horizontal filter layers (green bio-medium, amber frame).
		private static Bytemap GetBiofilterWall()
		{
			var b = new Bytemap(16, 16);

			// Frame (INK_MID)
			for (int x = 2; x <= 13; x++) { b[x, 2] = IM; b[x, 13] = IM; }
			for (int y = 2; y <= 13; y++) { b[2, y] = IM; b[13, y] = IM; }

			// Bio-filter layers (OK green)
			for (int x = 3; x <= 12; x++)
			{
				b[x, 4] = OK; b[x, 5] = OK;
				b[x, 8] = OK; b[x, 9] = OK;
			}

			// Amber separator bars
			for (int x = 3; x <= 12; x++)
				b[x, 6] = PD;
			for (int x = 3; x <= 12; x++)
				b[x, 10] = PD;

			// Filter status dots (PHOS_GLOW — active zones)
			b[5, 4] = GW; b[9, 4] = GW;
			b[5, 8] = GW; b[9, 8] = GW;

			return b;
		}

		// ── Public sprite table ─────────────────────────────────────────────

		private static readonly ISprite[] _sprites =
		{
			new CachedSprite(GetSettlementCluster),   // 0
			new CachedSprite(GetAquafarm),            // 1
			new CachedSprite(GetCanopyArray),         // 2
			new CachedSprite(GetRepairBay),           // 3
			new CachedSprite(GetExchangeNode),        // 4
			new CachedSprite(GetBiofilterWall),       // 5
		};

		internal static ISprite Get(OlvirImprovementType type) => _sprites[(int)type];
	}
}
