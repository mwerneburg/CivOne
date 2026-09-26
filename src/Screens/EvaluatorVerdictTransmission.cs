// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using CivOne.Graphics;

namespace CivOne.Screens
{
	// The Evaluators' determination. Written as a form, because it is one: an automated
	// process that has graded a species and is filing the result (the user's brief — "not the
	// Vulcans welcoming grubby humanity to the big leagues"). It does not congratulate and it
	// does not threaten. It notifies.
	internal class EvaluatorVerdictTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate, bool pass, bool viaDome, double cohesion,
			bool intentClean, string? offender)
		{
			int pct = (int)System.Math.Round(cohesion * 100);
			var lines = new List<string>
			{
				"LEAGUE INSTRUMENT — NOTICE OF DETERMINATION",
				$"TIMESTAMP: {gameDate}",
				"STATUS: FINAL",
				"",
				"SUBJECT SPECIES: HUMAN (SOL III).",
				"OBSERVATION PERIOD: CLOSED.",
				"",
			};
			if (viaDome)
				lines.Add("* UNISON: PLANETARY SHIELD RAISED IN CONCERT. CRITERION SATISFIED.");
			else
				lines.Add($"* COHESION: {pct}% OF POLITIES AT PEACE AND IN CONTACT. "
				          + (cohesion >= Game.CohesionToPass ? "THRESHOLD MET." : "BELOW THRESHOLD."));
			lines.Add(intentClean || viaDome
				? "* INTENT: NO PROSCRIBED ACTS RECORDED IN PERIOD."
				: "* INTENT: PROSCRIBED ACTS RECORDED IN PERIOD.");
			lines.Add("");

			if (pass)
			{
				lines.AddRange(new[]
				{
					"DETERMINATION: ADMIT.",
					"",
					"* ACCESS TO LEAGUE TRANSIT PROTOCOLS: GRANTED.",
					"* DRIVE SPECIFICATION: RELEASED TO ALL POLITIES.",
					"* SOL III ENTERED IN THE REGISTER OF RECEIVING WORLDS.",
				});
			}
			else
			{
				lines.AddRange(new[]
				{
					"DETERMINATION: RESET.",
					"",
					"* SOL III: QUARANTINE. NO VESSEL WILL LEAVE THIS SYSTEM.",
				});
				if (offender is not null)
					lines.AddRange(new[]
					{
						$"* CORRECTIVE MEASURE APPLIED: THE {offender.ToUpper()}.",
						"  BASIS: CUMULATIVE RECORD. OBSERVATION PRECEDED CONTACT.",
					});
				lines.Add("* RE-EVALUATION: NOT SCHEDULED.");
			}

			lines.AddRange(new[]
			{
				"",
				"THIS NOTICE REQUIRES NO REPLY. NO REPLY WILL BE READ.",
				"",
				"TRANSMISSION ENDS.",
			});
			return lines.ToArray();
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                      return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TIMESTAMP") || text.StartsWith("STATUS:")) return CassetteTheme.PHOS_DIM;
			if (text == "DETERMINATION: ADMIT.")                     return CassetteTheme.OK;
			if (text == "DETERMINATION: RESET.")                     return CassetteTheme.ALERT;
			if (text.StartsWith("* CORRECTIVE") ||
			    text.StartsWith("* SOL III: QUARANTINE"))            return CassetteTheme.ALERT;
			if (text.StartsWith("*"))                                return CassetteTheme.PHOS;
			if (text.StartsWith("THIS NOTICE"))                      return CassetteTheme.INK_LOW;
			if (text == "TRANSMISSION ENDS.")                        return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal EvaluatorVerdictTransmission(string gameDate, bool pass, bool viaDome, double cohesion,
			bool intentClean, string? offender)
		{
			_lines = BuildLines(gameDate, pass, viaDome, cohesion, intentClean, offender);
			InitTypewriter();
		}
	}
}
