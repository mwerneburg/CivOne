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
	internal class OlvirArrivalTransmission : TerminalScreen
	{
		private static string[] ArrivalDetails(VisitorArchetype archetype) => archetype switch
		{
			VisitorArchetype.Refugees => new[]
			{
				"VESSEL CLASS: SINGLE — ASYMMETRIC HULL, HEAVILY MODIFIED.",
				"POPULATION: ESTIMATED 200–800 INDIVIDUALS.",
				"COMMUNICATION: PARTIAL. THEY HAVE BEEN LEARNING OUR LANGUAGES.",
				"",
				"FIRST TRANSMISSION (TRANSLATED):",
				">> 'WE LEFT BECAUSE IT WAS ENDING.'",
				">> 'WE FOUND YOU BECAUSE WE WERE LOOKING.'",
				">> 'WE ASK FOR WATER. WE ASK FOR GROUND.'",
				">> 'WE HAVE KNOWLEDGE WE WILL SHARE.'",
				"",
				"ASSESSMENT: THEY ARE NOT INVADERS. THEY ARE SURVIVORS.",
				"ASSESSMENT: THEY SURVIVED SOMETHING THEIR HOME WORLD DID NOT.",
				"ASSESSMENT: WHATEVER ENDED THERE — THEY KNOW HOW IT ENDS.",
			},
			VisitorArchetype.Evaluators => new[]
			{
				"VESSEL CLASS: SINGLE — PRECISE GEOMETRIC HULL. NO VISIBLE MODIFICATIONS.",
				"POPULATION: UNKNOWN. BIOLOGICAL SIGNALS AMBIGUOUS.",
				"COMMUNICATION: FLUENT. THEY HAVE BEEN STUDYING US.",
				"",
				"FIRST TRANSMISSION (TRANSLATED):",
				">> 'YOUR SPECIES HAS REACHED THE THRESHOLD.'",
				">> 'WE OBSERVE. WE DO NOT YET PARTICIPATE.'",
				">> 'DEMONSTRATE COHESION. DEMONSTRATE INTENT.'",
				">> 'YOU HAVE TIME. USE IT CAREFULLY.'",
				"",
				"ASSESSMENT: THEY ARE WATCHING.",
				"ASSESSMENT: THIS IS A TEST WE DID NOT KNOW WE WERE TAKING.",
				"ASSESSMENT: THE CRITERIA HAVE NOT BEEN DISCLOSED.",
			},
			VisitorArchetype.Owners => new[]
			{
				"VESSEL CLASS: MULTIPLE — SURVEY FORMATION, STANDARD GRID PATTERN.",
				"POPULATION: UNKNOWN. EFFICIENT BROADCAST SUGGESTS COORDINATION.",
				"COMMUNICATION: DIRECT. THEY EXPECTED TO FIND NO ONE.",
				"",
				"FIRST TRANSMISSION (TRANSLATED):",
				">> 'THIS SECTOR WAS CATALOGUED AS UNINHABITED.'",
				">> 'RECLASSIFICATION IS IN PROCESS.'",
				">> 'YOU HAVE DEVELOPED BEYOND PROJECTED PARAMETERS.'",
				">> 'STATE YOUR DISPOSITION.'",
				"",
				"ASSESSMENT: THEY HELD PRIOR CLAIM TO THIS SYSTEM.",
				"ASSESSMENT: THEY DID NOT KNOW WE EXISTED.",
				"ASSESSMENT: OUR EXISTENCE HAS COMPLICATED THEIR PLANS.",
			},
			_ => new[] // Conquerors
			{
				"VESSEL CLASS: MULTIPLE — COMBAT FORMATION, DISTRIBUTED APPROACH.",
				"POPULATION: UNKNOWN. SIGNAL COMPLEXITY SUGGESTS THOUSANDS.",
				"COMMUNICATION: DEMANDS. THEY EXPECT COMPLIANCE.",
				"",
				"FIRST TRANSMISSION (TRANSLATED):",
				">> 'THIS WORLD IS DESIGNATED FOR ACQUISITION.'",
				">> 'RESISTANCE IS CATEGORISED AS INEFFICIENT.'",
				">> 'TRANSFER OF SURFACE ACCESS IS REQUIRED.'",
				">> 'COMPLIANCE TIMELINE: 60 ORBITAL CYCLES.'",
				"",
				"ASSESSMENT: THEY ARE NOT ASKING.",
				"ASSESSMENT: THEY HAVE DONE THIS BEFORE.",
				"ASSESSMENT: THE DOME IS NOW THE ONLY RELEVANT VARIABLE.",
			},
		};

		private static string[] BuildLines(string gameDate, VisitorArchetype archetype)
		{
			var lines = new List<string>
			{
				"FIRST CONTACT — CLASSIFICATION: BEYOND OMEGA",
				$"CONTACT TIMESTAMP: {gameDate}",
				"STATUS: ACTIVE",
				"",
				"TAU CETI VESSEL HAS ACHIEVED EARTH ORBIT.",
				"",
			};

			lines.AddRange(ArrivalDetails(archetype));

			lines.AddRange(new[]
			{
				"",
				"WORLD GOVERNMENTS CONVENING.",
				"FURTHER TRANSMISSIONS TO FOLLOW.",
				"",
				"TRANSMISSION ENDS.",
			});

			return lines.ToArray();
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                              return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("CONTACT TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                                  return CassetteTheme.PHOS_DIM;
			if (text == "TAU CETI VESSEL HAS ACHIEVED EARTH ORBIT.")         return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("VESSEL CLASS:") ||
			    text.StartsWith("POPULATION:") ||
			    text.StartsWith("COMMUNICATION:"))                           return CassetteTheme.INK_MID;
			if (text == "FIRST TRANSMISSION (TRANSLATED):")                  return CassetteTheme.INK_HIGH;
			if (text.StartsWith(">> "))                                      return CassetteTheme.PHOS;
			if (text.StartsWith("ASSESSMENT:"))                              return CassetteTheme.INK_HIGH;
			if (text.StartsWith("WORLD GOVERNMENTS") ||
			    text.StartsWith("FURTHER TRANSMISSIONS"))                    return CassetteTheme.PHOS_DIM;
			if (text == "TRANSMISSION ENDS.")                                return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal OlvirArrivalTransmission(string gameDate, VisitorArchetype archetype)
		{
			_lines = BuildLines(gameDate, archetype);
			InitTypewriter();
		}
	}
}
