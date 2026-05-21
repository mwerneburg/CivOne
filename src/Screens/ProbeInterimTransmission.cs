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
	// Fired in three phases during the 35-turn probe transit to Tau Ceti.
	// Phase 1 (+8 turns):  heliopause cleared, software ack
	// Phase 2 (+18 turns): midpoint deceleration, spectroscopic survey
	// Phase 3 (+28 turns): visual contact, anomalous emissions detected
	internal class ProbeInterimTransmission : TerminalScreen
	{
		private static string[] Phase1Lines(string gameDate) => new[]
		{
			"PRIORITY: ROUTINE",
			$"TRANSMISSION TIMESTAMP: {gameDate}",
			"SOURCE: PROBE ALPHA — OUTBOUND",
			"",
			"SUBJECT: HELIOPAUSE CROSSING — NOMINAL",
			"",
			"PROBE STATUS REPORT — YEAR 8 OF TRANSIT",
			"",
			"Oort Cloud cleared. No collision events logged.",
			"Drive performance: 101.3% of rated thrust. Anomaly: none.",
			"Fuel margin: 94.2% reserve. Trajectory: confirmed on-course.",
			"",
			"SOFTWARE UPDATE ACKNOWLEDGED:",
			"Navigation patch v3.1 received, installed, and verified.",
			"Star-lock recalibrated: Tau Ceti bearing nominal.",
			"",
			"NOTE: Signal delay at this range is approximately 2.7 years.",
			"This report was transmitted 2.7 years prior to receipt.",
			"",
			"NEXT SCHEDULED REPORT: MIDPOINT DECELERATION.",
			"",
			"TRANSMISSION ENDS.",
		};

		private static string[] Phase2Lines(string gameDate) => new[]
		{
			"PRIORITY: ROUTINE",
			$"TRANSMISSION TIMESTAMP: {gameDate}",
			"SOURCE: PROBE ALPHA — OUTBOUND",
			"",
			"SUBJECT: MIDPOINT DECELERATION BURN — COMPLETE",
			"",
			"PROBE STATUS REPORT — YEAR 18 OF TRANSIT",
			"",
			"Position: 6.1 light-years from Sol. 5.8 light-years from Tau Ceti.",
			"Deceleration burn: complete. Velocity now 34% c and reducing.",
			"Drive performance: 99.1% of rated. Fuel margin: 61.8% reserve.",
			"",
			"TAU CETI SYSTEM — SPECTROSCOPIC SURVEY:",
			"",
			"* Planetary system confirmed. Minimum five bodies detected.",
			"* Three rocky candidates reside within habitable zone.",
			"* Atmospheric signatures: analysis ongoing. Inconclusive at range.",
			"* No response to outbound transmission at this time.",
			"* Source continues to emit on original signal frequencies.",
			"",
			"NOTE: Signal delay at this range is approximately 6.1 years.",
			"This report was transmitted 6.1 years prior to receipt.",
			"",
			"NEXT SCHEDULED REPORT: SYSTEM APPROACH.",
			"",
			"TRANSMISSION ENDS.",
		};

		private static string[] Phase3Lines(string gameDate) => new[]
		{
			"PRIORITY: URGENT",
			$"TRANSMISSION TIMESTAMP: {gameDate}",
			"SOURCE: PROBE ALPHA — INBOUND",
			"",
			"SUBJECT: TAU CETI SYSTEM — VISUAL CONTACT ESTABLISHED",
			"",
			"PROBE STATUS REPORT — YEAR 28 OF TRANSIT",
			"",
			"Position: 9.5 light-years from Sol. 2.4 light-years from Tau Ceti.",
			"Approach velocity nominal. Drive status: deceleration standby.",
			"",
			"OBSERVATION — TAU CETI SYSTEM:",
			"",
			"* Tau Ceti now visually resolved. Companion bodies confirmed.",
			"* Three rocky planets in habitable zone — surface albedo analysis underway.",
			"* Tau Ceti II: strong thermal asymmetry detected. Origin: unclear.",
			"",
			"ANOMALY DETECTED:",
			"",
			"* Narrowband electromagnetic emissions from Tau Ceti II.",
			"* Pattern: structured, non-thermal, non-random.",
			"* Assessment: artificial origin probable.",
			"* Further analysis pending flyby.",
			"",
			"NOTE: Signal delay at this range is approximately 9.5 years.",
			"This report was transmitted 9.5 years prior to receipt.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                              return CassetteTheme.INK_HIGH;
			if (text.StartsWith("TRANSMISSION TIMESTAMP") ||
			    text.StartsWith("SOURCE"))                                   return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("SUBJECT"))                                  return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("PROBE STATUS REPORT") ||
			    text.StartsWith("TAU CETI SYSTEM") ||
			    text.StartsWith("OBSERVATION") ||
			    text.StartsWith("SOFTWARE UPDATE ACKNOWLEDGED") ||
			    text.StartsWith("TRANSMISSION ENDS"))                        return CassetteTheme.INK_HIGH;
			if (text.StartsWith("ANOMALY DETECTED"))                         return CassetteTheme.ALERT;
			if (text.StartsWith("* Assessment:") ||
			    text.StartsWith("* Tau Ceti II:") ||
			    text.StartsWith("* Narrowband") ||
			    text.StartsWith("* Pattern:"))                               return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("*"))                                        return CassetteTheme.PHOS;
			if (text.StartsWith("NOTE:"))                                    return CassetteTheme.PHOS_DIM;
			return CassetteTheme.INK_MID;
		}

		internal ProbeInterimTransmission(string gameDate, int phase)
		{
			_lines = phase switch
			{
				1 => Phase1Lines(gameDate),
				2 => Phase2Lines(gameDate),
				_ => Phase3Lines(gameDate),
			};
			InitTypewriter();
		}
	}
}
