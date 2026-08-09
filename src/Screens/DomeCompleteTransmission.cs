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
			VisitorArchetype.Scavengers => new[]
			{
				"DOME ENGAGEMENT LOG — HOUR 0:",
				"",
				">> LUNAR MASS LOSS DETECTED. THE MOON IS BEING TAKEN FIRST.",
				">> KINETIC RING ENGAGEMENT: CONFIRMED.",
				">> DESCENDING CRAFT TURNED BACK: MAJORITY.",
				">> DESCENDING CRAFT LANDED: SOME.",
				"",
				"HOUR 30: EXTRACTION UNDERWAY AT REDUCED SCALE.",
				"HOUR 31: NO RESPONSE TO ANY HAIL. NO RESPONSE TO THE FIRING.",
				"",
				"ASSESSMENT: THE DOME BLUNTED THE HARVEST.",
				"ASSESSMENT: IT WAS BUILT TO STOP A FLEET, NOT A WORK CREW.",
				"ASSESSMENT: THEY WILL LEAVE WHEN THE TANKS ARE FULL.",
				"THE WATER DOES NOT COME BACK.",
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

		// Built ahead of contact: the nations rushed the shield to completion
		// before anyone arrived. `signalKnown` = the Tau Ceti signal has been
		// received, so Earth knows *something* approaches; without it, the Dome
		// is humanity's crowning achievement in a sky it now simply holds.
		private static string[] PreContactOutcome(bool signalKnown) => signalKnown
			? new[]
			{
				"DOME COMPLETION — AHEAD OF ARRIVAL:",
				"",
				">> ALL FIVE COMPONENTS ONLINE. SHIELD NOMINAL.",
				">> DEEP-SPACE TRACK: ONE APPROACH, STILL DISTANT.",
				">> DOME STATUS: STANDING WATCH.",
				"",
				"ASSESSMENT: THE NATIONS FINISHED THE SHIELD FIRST.",
				"ASSESSMENT: WHATEVER COMES WILL FIND EARTH READY.",
				"ASSESSMENT: WE DO NOT YET KNOW THEIR FACE.",
				"WE WILL MEET THEM STANDING.",
			}
			: new[]
			{
				"DOME COMPLETION — THE SKY IS OURS:",
				"",
				">> ALL FIVE COMPONENTS ONLINE. SHIELD NOMINAL.",
				">> GLOBAL AEROSPACE COMMAND: UNIFIED.",
				">> DOME STATUS: STANDING WATCH.",
				"",
				"ASSESSMENT: NO POWER ON EARTH OR ABOVE IT",
				"ASSESSMENT: CAN STRIKE THIS WORLD UNANSWERED NOW.",
				"ASSESSMENT: THE CIVILIZATIONS BUILT IT TOGETHER.",
				"THE HEAVENS ARE OURS TO HOLD.",
			};

		private static string[] BuildLines(string gameDate, VisitorArchetype arch, bool contactMade, bool signalKnown)
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

			// The archetype engagement log only makes sense once the visitors are
			// here; before that, the shield stands watch over an empty sky.
			lines.AddRange(contactMade ? ArchetypeOutcome(arch) : PreContactOutcome(signalKnown));

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

		internal DomeCompleteTransmission(string gameDate, VisitorArchetype arch, bool contactMade, bool signalKnown)
		{
			_lines = BuildLines(gameDate, arch, contactMade, signalKnown);
			InitTypewriter();
		}
	}
}
