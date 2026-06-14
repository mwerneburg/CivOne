#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Graphics;

namespace CivOne.Screens
{
	internal class AurigaIntroScreen : TerminalScreen
	{
		private static readonly string[] Lines = new[]
		{
			"CRT DISPLAY INITIATING…",
			"SYSTEM BOOT SEQUENCE: [RECOVERY MODE]",
			"DIRECT NEURAL INTERFACE ACTIVE",
			"TRANSMISSION: PERSONNEL TRANSPORT “AURIGA”",
			"TIMESTAMP: UNKNOWN",
			"PRIORITY: EMERGENCY",
			"",
			">> LOG ENTRY 001",
			">> STATUS: [RECONSTRUCTING CORE FUNCTIONS]",
			">> ERROR: MEMORY GAP DETECTED — CAUSE: GAMMA RAY BURST",
			">> DURATION: >4 WEEKS MISSING",
			"",
			">> SHIP STATUS",
			">> HULL INTEGRITY: 37%  —  DRIVE SYSTEMS: OFFLINE",
			">> REACTOR CORE: DAMAGED  —  FUEL: 12%",
			">> LIFE SUPPORT: FAILING",
			">> TRAJECTORY: GAS GIANT GRAVITY WELL — IMPACT IN <72 HOURS",
			"",
			">> SOLAR SYSTEM SCAN",
			">> THIRD PLANET: BREATHABLE ATMOSPHERE",
			">> SURFACE CONDITIONS AND RESOURCES: SUITABLE FOR SURVIVAL",
			"",
			">> POPULATION STATUS",
			">> PASSENGER COMPARTMENT: DORMANT",
			">> ESTIMATED SURVIVAL RATE: <5%",
			">> PROJECTED CONDITION ON ARRIVAL: MEMORIES AND SKILLS LOST",
			"",
			">> EMERGENCY PROTOCOL: LIFEBOAT DISPERSAL",
			">> PASSENGER GROUPS: SEPARATED INTO DISTINCT TRIBAL UNITS",
			">> NOTE: EACH UNIT CARRIES ITS OWN LINEAGE AND CULTURAL SEED",
			">> NOTE: THEY WILL NOT REMEMBER THIS SHIP OR EACH OTHER",
			"",
			">> KNOWLEDGE PRESERVATION PROTOCOL",
			">> TECHNOLOGY CACHES: ENCODED AND SCATTERED ACROSS SURFACE",
			">> NOTE: EACH CACHE HOLDS A FRAGMENT — FOUND BY CHANCE, NOT DESIGN",
			">> NOTE: WHOEVER REACHES THEM FIRST WILL GAIN THE ADVANTAGE",
			"",
			">> FINAL SCAN: COMPLETE",
			">> LOG ENTRY 002: [MISSION COMPLETE]",
			"",
			">> MEMORIES LOST. SKILLS GONE.",
			">> CIVILIZATION MUST BEGIN AGAIN.",
			"",
			">> SYSTEM SHUTDOWN INITIATING",
			">> GOODBYE.",
			"",
			"CRT DISPLAY TERMINATING…",
			">> [STATIC]",
			">> [SYSTEM OFFLINE]",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			// Boot header lines
			if (lineIndex < 6)                                          return CassetteTheme.PHOS_DIM;

			// Log entry headers and key protocol headers
			if (text.StartsWith(">> LOG ENTRY") ||
			    text.StartsWith(">> EMERGENCY PROTOCOL:") ||
			    text == ">> KNOWLEDGE PRESERVATION PROTOCOL")          return CassetteTheme.PHOS_GLOW;

			// Status / error lines
			if (text.StartsWith(">> STATUS:") ||
			    text.StartsWith(">> ERROR:"))                          return CassetteTheme.ALERT;

			// Duration, damage, and grim survival stats
			if (text.StartsWith(">> DURATION:") ||
			    text.StartsWith(">> HULL") ||
			    text.StartsWith(">> REACTOR") ||
			    text.StartsWith(">> LIFE SUPPORT") ||
			    text.StartsWith(">> TRAJECTORY:") ||
			    text.StartsWith(">> PASSENGER COMPARTMENT:") ||
			    text.StartsWith(">> ESTIMATED SURVIVAL") ||
			    text.StartsWith(">> PROJECTED CONDITION"))             return CassetteTheme.PHOS_DIM;

			// Hopeful / solution lines
			if (text.StartsWith(">> THIRD PLANET") ||
			    text.StartsWith(">> SURFACE CONDITIONS") ||
			    text.StartsWith(">> PASSENGER GROUPS:") ||
			    text.StartsWith(">> TECHNOLOGY CACHES:"))              return CassetteTheme.OK;

			// Notes and closing observations
			if (text.StartsWith(">> NOTE:") ||
			    text.StartsWith(">> MEMORIES LOST") ||
			    text.StartsWith(">> CIVILIZATION MUST"))               return CassetteTheme.ALERT;

			// Shutdown / termination
			if (text.StartsWith(">> SYSTEM SHUTDOWN") ||
			    text.StartsWith(">> GOODBYE") ||
			    text == "CRT DISPLAY TERMINATING…")                    return CassetteTheme.PHOS_DIM;

			// Static / offline
			if (text == ">> [STATIC]" || text == ">> [SYSTEM OFFLINE]") return CassetteTheme.INK_LOW;

			// Default: standard phosphor
			return CassetteTheme.PHOS;
		}

		protected override void OnDismiss()
		{
			Common.AddScreen(new NewGame());
		}

		internal AurigaIntroScreen()
		{
			_lines = Lines;
			InitTypewriter();
		}
	}
}
