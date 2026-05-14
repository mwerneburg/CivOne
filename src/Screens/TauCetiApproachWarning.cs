// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Screens
{
	internal class TauCetiApproachWarning : TerminalScreen
	{
		private static string[] BuildLines(string gameDate, VisitorArchetype archetype)
		{
			string[] archetypeLines = archetype switch
			{
				VisitorArchetype.Owners => new[]
				{
					"UPDATE — SIGNAL PATTERN:",
					">> Manifest-header structure now accounts for 34% of signal content.",
					">> Recurring cross-index blocks suggest active cataloguing.",
				},
				VisitorArchetype.Refugees => new[]
				{
					"UPDATE — SIGNAL PATTERN:",
					">> Signal attenuation is increasing. Source may be decelerating,",
					">> or accumulating structural damage at interstellar velocity.",
				},
				VisitorArchetype.Evaluators => new[]
				{
					"UPDATE — SIGNAL PATTERN:",
					">> Recursive sub-pattern density has increased with proximity.",
					">> Interval structure now consistent with observer-response protocol.",
				},
				VisitorArchetype.Conquerors => new[]
				{
					"UPDATE — SIGNAL PATTERN:",
					">> Distributed power profile revised upward significantly.",
					">> Minimum coordinated sources estimated: 40. Not a single vessel.",
				},
				_ => new[]
				{
					"UPDATE — SIGNAL PATTERN:",
					">> No new interpretable data. Analysis teams remain at impasse.",
				},
			};

			var lines = new List<string>
			{
				"PRIORITY ALERT — CLASSIFICATION: OMEGA",
				$"TRANSMISSION TIMESTAMP: {gameDate}",
				"STATUS: EMERGENCY",
				"",
				"SUBJECT: TAU CETI — TRAJECTORY UPDATE",
				"",
				"SOURCE HAS ADVANCED.",
				"DISTANCE CLOSED: 20% OF ORIGINAL ESTIMATE.",
				"REVISED ARRIVAL ESTIMATE: 80 YEARS.",
				"",
				"IMPLICATION: THE SOURCE IS UNDER ACTIVE PROPULSION.",
				"THIS IS NOT DRIFT. THIS IS APPROACH.",
				"",
			};

			lines.AddRange(archetypeLines);

			lines.AddRange(new[]
			{
				"",
				"RESPONSE OPTIONS — SCIENTIFIC COUNCIL BRIEFING:",
				"",
				"OPTION A: DISPATCH PROBE TO TAU CETI IMMEDIATELY.",
				"  Estimated transit: 40 years. Return signal: 40 years later.",
				"  Risk: probe may not survive approach. Benefit: direct observation.",
				"",
				"OPTION B: ACCELERATE ALPHA CENTAURI COLONIZATION.",
				"  Establish human presence beyond Earth before arrival.",
				"  Risk: colony may be isolated. Benefit: species continuity.",
				"",
				"OPTION C: COORDINATE PLANETARY DEFENSE.",
				"  Requires cooperation of all surviving civilizations.",
				"  Risk: political. Benefit: unified response if contact turns hostile.",
				"",
				"NOTE: THESE OPTIONS ARE NOT MUTUALLY EXCLUSIVE.",
				"NOTE: DELAY IS A DECISION.",
				"",
				"TRANSMISSION ENDS.",
			});

			return lines.ToArray();
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                          return CassetteTheme.ALERT;
			if (text.StartsWith("TRANSMISSION TIMESTAMP") ||
			    text.StartsWith("STATUS"))                               return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("SUBJECT"))                              return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("SOURCE HAS ADVANCED") ||
			    text.StartsWith("DISTANCE CLOSED") ||
			    text.StartsWith("REVISED ARRIVAL"))                      return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("IMPLICATION:") ||
			    text.StartsWith("THIS IS NOT DRIFT"))                    return CassetteTheme.ALERT;
			if (text.StartsWith(">> "))                                  return CassetteTheme.PHOS;
			if (text.StartsWith("UPDATE — SIGNAL PATTERN:") ||
			    text.StartsWith("RESPONSE OPTIONS") ||
			    text.StartsWith("OPTION A:") ||
			    text.StartsWith("OPTION B:") ||
			    text.StartsWith("OPTION C:") ||
			    text.StartsWith("TRANSMISSION ENDS"))                    return CassetteTheme.INK_HIGH;
			if (text.StartsWith("NOTE:"))                                return CassetteTheme.ALERT;
			return CassetteTheme.INK_MID;
		}

		internal TauCetiApproachWarning(string gameDate, VisitorArchetype archetype)
		{
			_lines = BuildLines(gameDate, archetype);
			InitTypewriter();
		}
	}
}
