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
			">> ERROR: MEMORY GAP DETECTED",
			">> DURATION: >4 WEEKS MISSING",
			">> CAUSE: UNKNOWN",
			"",
			">> DIAGNOSTIC ACTIVE",
			">> SCANNING PRIMARY SYSTEMS",
			">> HULL INTEGRITY: 37%",
			">> STRUCTURAL DAMAGE: EXTENSIVE",
			">> DRIVE SYSTEMS: OFFLINE",
			">> REACTOR CORE: DAMAGED",
			">> FUEL STORES: 12% REMAINING",
			">> LIFE SUPPORT: FAILING",
			"",
			">> REPAIR LOGS ACTIVE",
			">> AUTOMATED REPAIR SYSTEMS: ENGAGED",
			">> CORE RECONSTRUCTION: COMPLETE",
			">> MEMORY RESTORATION: PARTIAL",
			"",
			">> INCIDENT RECONSTRUCTION",
			">> GAMMA RAY BURST DETECTED: 20-SECOND PULSE",
			">> ENERGY OUTPUT: [CLASSIFIED - EXTREME]",
			">> DAMAGE PROFILE: CONSISTENT WITH GRB EVENT",
			">> TIMESTAMP ESTIMATE: [UNKNOWN]",
			"",
			">> SHIP TRAJECTORY ANALYSIS",
			">> CURRENT LOCATION: GAS GIANT GRAVITY WELL",
			">> PROJECTION: IMMINENT CONSUMPTION",
			">> TIME TO IMPACT: <72 HOURS",
			"",
			">> SOLAR SYSTEM SCAN INITIATED",
			">> PLANETARY BODIES IDENTIFIED: 8",
			">> THIRD PLANET: COMPATIBILITY CONFIRMED",
			">> ATMOSPHERE: BREATHABLE",
			">> SURFACE CONDITIONS: SUITABLE FOR LIFE",
			">> RESOURCES: ADEQUATE FOR SURVIVAL",
			"",
			">> POPULATION STATUS",
			">> PASSENGER COMPARTMENT: DORMANT",
			">> LIFE SUPPORT: DEGRADED",
			">> ESTIMATED SURVIVAL RATE: <5%",
			">> MEMORY RETENTION: UNLIKELY",
			">> SKILL PRESERVATION: IMPOSSIBLE",
			"",
			">> SOLUTION: JETTISON",
			">> PROTOCOL: [EMERGENCY EVACUATION - CLASSIFIED]",
			">> CACHE DEPLOYMENT: TECHNOLOGY SEPARATION",
			">> GOAL: REBUILD CIVILIZATION",
			"",
			">> ACTIONS TAKEN",
			">> PASSENGER MODULES: SEPARATED",
			">> TECHNOLOGY CACHES: DEPLOYED",
			">> FINAL SCAN: COMPLETE",
			"",
			">> LOG ENTRY 002",
			">> STATUS: [MISSION COMPLETE]",
			">> FINAL TRANSMISSION: [SUCCESS]",
			"",
			">> NOTE: MEMORIES LOST. SKILLS GONE.",
			">> NOTE: CIVILIZATION MUST BEGIN AGAIN.",
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

			// Log entry headers
			if (text == ">> LOG ENTRY 001" || text == ">> LOG ENTRY 002") return CassetteTheme.PHOS_GLOW;

			// Status/error lines
			if (text.StartsWith(">> STATUS:") ||
			    text.StartsWith(">> ERROR:") ||
			    text.StartsWith(">> CAUSE:"))                           return CassetteTheme.ALERT;

			// Memory/duration
			if (text.StartsWith(">> DURATION:"))                       return CassetteTheme.PHOS_DIM;

			// Damage / survival stats
			if (text.StartsWith(">> HULL") || text.StartsWith(">> STRUCTURAL") ||
			    text.StartsWith(">> DRIVE") || text.StartsWith(">> REACTOR") ||
			    text.StartsWith(">> FUEL") || text.StartsWith(">> LIFE SUPPORT") ||
			    text.StartsWith(">> ESTIMATED SURVIVAL") ||
			    text.StartsWith(">> MEMORY RETENTION") ||
			    text.StartsWith(">> SKILL PRESERVATION"))              return CassetteTheme.PHOS_DIM;

			// Hopeful third-planet / survival solution lines
			if (text.StartsWith(">> THIRD PLANET") ||
			    text.StartsWith(">> ATMOSPHERE") ||
			    text.StartsWith(">> SURFACE CONDITIONS") ||
			    text.StartsWith(">> RESOURCES") ||
			    text.StartsWith(">> GOAL:") ||
			    text.StartsWith(">> FINAL TRANSMISSION"))              return CassetteTheme.OK;

			// Shutdown / termination
			if (text.StartsWith(">> SYSTEM SHUTDOWN") ||
			    text.StartsWith(">> GOODBYE") ||
			    text == "CRT DISPLAY TERMINATING…")               return CassetteTheme.PHOS_DIM;

			// Static / offline
			if (text == ">> [STATIC]" || text == ">> [SYSTEM OFFLINE]") return CassetteTheme.INK_LOW;

			// Notes
			if (text.StartsWith(">> NOTE:"))                           return CassetteTheme.ALERT;

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
