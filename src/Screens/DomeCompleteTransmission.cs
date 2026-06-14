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
using CivOne.Enums;
using CivOne.Graphics;

namespace CivOne.Screens
{
	internal class DomeCompleteTransmission : TerminalScreen
	{
		private static string[] ArchetypeOutcome(VisitorArchetype arch) => arch switch
		{
			VisitorArchetype.Conquerors => new[]
			{
				"DOME ENGAGEMENT LOG — HOUR 0:",
				"",
				">> APPROACH VECTOR ENTERED DOME INTERCEPT RANGE.",
				">> KINETIC RING ENGAGEMENT: CONFIRMED.",
				">> EMITTER ARRAY: SUSTAINED FIRE — 14 HOURS.",
				">> SENSOR NET: TRACKING 40+ DISCRETE TARGETS.",
				"",
				"HOUR 17: FORMATION DISPERSED. APPROACH HALTED.",
				"HOUR 22: NO FURTHER SIGNAL DETECTED FROM TAU CETI.",
				"",
				"ASSESSMENT: THE DOME HELD.",
				"ASSESSMENT: THEY DID NOT EXPECT RESISTANCE.",
				"ASSESSMENT: THEY MAY RETURN.",
			},
			VisitorArchetype.Owners => new[]
			{
				"DOME ACTIVATION — INITIAL CONTACT:",
				"",
				">> DOME POWER CORE: ONLINE.",
				">> INCOMING TRANSMISSION INTERCEPTED AT DOME PERIMETER.",
				">> TRANSLATION IN PROGRESS...",
				"",
				"MESSAGE: 'THIS CLAIM IS DISPUTED.'",
				"MESSAGE: 'PRESENT YOUR TERMS.'",
				"",
				"ASSESSMENT: THE DOME CHANGED THE NEGOTIATION.",
				"ASSESSMENT: THEY SEE US AS PEERS NOW, NOT PROPERTY.",
				"ASSESSMENT: AN AGREEMENT IS POSSIBLE.",
				"THE TERMS WILL NOT BE EASY.",
			},
			VisitorArchetype.Evaluators => new[]
			{
				"DOME COMPLETION — EVALUATOR RESPONSE:",
				"",
				">> COMMAND HUB: SIGNAL RECEIVED AT ACTIVATION.",
				">> CONTENT: A SINGLE SYMBOL. CROSS-REFERENCED.",
				">> TRANSLATION: 'ACKNOWLEDGED.'",
				"",
				"ASSESSMENT: THE DOME WAS THE FINAL TEST.",
				"ASSESSMENT: A SPECIES THAT CANNOT UNIFY CANNOT BE TRUSTED.",
				"ASSESSMENT: YOU UNIFIED.",
				"ASSESSMENT: EVALUATION — COMPLETE. RESULT: PASS.",
			},
			_ => new[] // Refugees
			{
				"DOME ACTIVATION — REFUGEE CONTACT:",
				"",
				">> DOME SENSOR NET CONFIRMED: SINGLE VESSEL, DECELERATING.",
				">> DOME COMMAND HUB TRANSMITTED LANDING COORDINATES.",
				">> THEIR RESPONSE: IMMEDIATE COMPLIANCE.",
				"",
				"ASSESSMENT: THEY WERE AFRAID OF THE DOME.",
				"ASSESSMENT: THEY HAVE BEEN AFRAID FOR A LONG TIME.",
				"ASSESSMENT: THEY LANDED PEACEFULLY.",
				"ASSESSMENT: THEY BROUGHT WHAT THEY KNEW.",
				"THE OCEANS HAVE ROOM FOR THEM.",
			},
		};

		private static string[] BuildLines(string gameDate, VisitorArchetype arch)
		{
			var lines = new List<string>
			{
				"PLANETARY DEFENCE DOME — OPERATIONAL",
				$"COMPLETION TIMESTAMP: {gameDate}",
				"STATUS: ALL FIVE COMPONENTS ONLINE",
				"",
				"CIVILIZATIONS PARTICIPATING: ALL SURVIVING",
				"",
			};

			lines.AddRange(ArchetypeOutcome(arch));

			lines.AddRange(new[]
			{
				"",
				"EARTH STANDS.",
				"",
				"TRANSMISSION ENDS.",
			});

			return lines.ToArray();
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                               return CassetteTheme.OK;
			if (text.StartsWith("COMPLETION TIMESTAMP") ||
			    text.StartsWith("STATUS:") ||
			    text.StartsWith("CIVILIZATIONS"))             return CassetteTheme.PHOS_DIM;
			if (text.StartsWith(">> "))                      return CassetteTheme.PHOS;
			if (text.StartsWith("MESSAGE:") ||
			    text.StartsWith("ASSESSMENT:"))              return CassetteTheme.INK_HIGH;
			if (text == "EARTH STANDS.")                     return CassetteTheme.OK;
			if (text == "TRANSMISSION ENDS.")                return CassetteTheme.INK_LOW;
			if (text.StartsWith("THE "))                     return CassetteTheme.PHOS_GLOW;
			return CassetteTheme.INK_MID;
		}

		internal DomeCompleteTransmission(string gameDate, VisitorArchetype arch)
		{
			_lines = BuildLines(gameDate, arch);
			InitTypewriter();
		}
	}
}
