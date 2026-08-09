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
using CivOne.Graphics;

namespace CivOne.Screens
{
	internal class ProbeResultTransmission : TerminalScreen
	{
		// ── Archetype-flavoured text by outcome tier ──────────────────────────

		// Tier 0: probe destroyed/lost — archetype hinted, not named
		private static string[] DestroyedLines(VisitorArchetype arch)
		{
			switch (arch)
			{
				case VisitorArchetype.Owners:
					return new[]
					{
						">> LAST FRAME: TARGET EMITTED A DIRECTED PULSE.",
						">> PROBE INTERCEPTED. THE RESPONSE WAS DELIBERATE.",
						">> INTERPRETATION: SOURCE RECOGNIZED THE PROBE AS FOREIGN.",
					};
				case VisitorArchetype.Conquerors:
					return new[]
					{
						">> LAST FRAME: MULTIPLE SIMULTANEOUS IMPACT EVENTS.",
						">> NO ATTEMPT AT COMMUNICATION. RESPONSE WAS IMMEDIATE.",
						">> INTERPRETATION: THE SOURCE DOES NOT NEGOTIATE.",
					};
				case VisitorArchetype.Evaluators:
					return new[]
					{
						">> LAST FRAME: PROBE SYSTEMS DEACTIVATED REMOTELY.",
						">> MECHANISM: UNKNOWN. RANGE: WELL BEYOND EXPECTATION.",
						">> INTERPRETATION: THE SOURCE FOUND THE PROBE INSUFFICIENT.",
					};
				case VisitorArchetype.Scavengers:
					return new[]
					{
						">> LAST FRAME: PROBE STRUCK BY DEBRIS ON AN UNLIT VECTOR.",
						">> NO TARGETING SIGNATURE. NO CHALLENGE. NO ACKNOWLEDGEMENT.",
						">> INTERPRETATION: THE SOURCE DID NOT NOTICE THE PROBE.",
					};
				default: // Refugees / None
					return new[]
					{
						">> FINAL TELEMETRY: STRUCTURAL FAILURE AT OUTER RANGE.",
						">> NO CONFIRMED CONTACT. NO RESPONSE RECEIVED.",
						">> INTERPRETATION: INDETERMINATE.",
					};
			}
		}

		// Tier 1: partial data — archetype hinted via sensor readings
		private static string[] PartialLines(VisitorArchetype arch)
		{
			switch (arch)
			{
				case VisitorArchetype.Owners:
					return new[]
					{
						">> PARTIAL SCAN: STRUCTURED CARGO MANIFEST DETECTED.",
						">> INVENTORY FORMAT CONSISTENT WITH ORIGIN CATALOGUING.",
						">> HYPOTHESIS: SOURCE MAY BE CONDUCTING AN AUDIT.",
					};
				case VisitorArchetype.Conquerors:
					return new[]
					{
						">> PARTIAL SCAN: DISTRIBUTED POWER SIGNATURES — 40+ NODES.",
						">> FORMATION GEOMETRY INCONSISTENT WITH CIVILIAN TRANSPORT.",
						">> HYPOTHESIS: COORDINATED MILITARY APPROACH.",
					};
				case VisitorArchetype.Evaluators:
					return new[]
					{
						">> PARTIAL SCAN: RECURSIVE SIGNAL BOUNCE DETECTED.",
						">> SOURCE RESPONDED TO THE PROBE — WITH ANOTHER PROBE.",
						">> HYPOTHESIS: SOURCE IS MEASURING OUR REACTION.",
					};
				case VisitorArchetype.Scavengers:
					return new[]
					{
						">> PARTIAL SCAN: MASS DISTRIBUTION IS OVERWHELMINGLY TANKAGE.",
						">> NO HABITAT RING. NO SIGNAL TRAFFIC BETWEEN HULLS.",
						">> HYPOTHESIS: THIS IS NOT A FLEET. IT IS EQUIPMENT.",
					};
				default: // Refugees
					return new[]
					{
						">> PARTIAL SCAN: SINGLE VESSEL. PROPULSION IRREGULAR.",
						">> HULL GEOMETRY SUGGESTS DAMAGE OR HEAVY MODIFICATION.",
						">> HYPOTHESIS: SOURCE IS NOT TRAVELLING BY CHOICE.",
					};
			}
		}

		// Tier 2: full identification — archetype named
		private static string[] IdentificationLines(VisitorArchetype arch)
		{
			switch (arch)
			{
				case VisitorArchetype.Owners:
					return new[]
					{
						"VISITOR TYPE: IDENTIFIED.",
						"",
						">> SIGNAL CROSS-REFERENCE CONFIRMS: THE AURIGA MANIFEST",
						">> MATCHES THEIR INVENTORY FORMAT EXACTLY.",
						">> THESE ARE THE SHIP'S ORIGINAL CONTROLLERS.",
						">> HUMANITY IS NOT A CIVILIZATION TO THEM.",
						">> HUMANITY IS ESCAPED CARGO.",
						"",
						"THE GAMMA RAY BURST WAS NOT AN ACCIDENT.",
					};
				case VisitorArchetype.Conquerors:
					return new[]
					{
						"VISITOR TYPE: IDENTIFIED.",
						"",
						">> POPULATION GENETIC MARKERS: HUMAN.",
						">> CULTURAL LINEAGE: CONSISTENT WITH EARLY STEPPE CULTURES.",
						">> CROSS-REFERENCE: THEY LEFT EARTH APPROXIMATELY 12,000 YEARS AGO.",
						">> THEY DID NOT GO FAR. THEY WENT TO WAR — ELSEWHERE.",
						">> NOW THEY ARE RETURNING.",
						"",
						"ATTILA'S PEOPLE FOUND LARGER PREY. AND WON.",
					};
				case VisitorArchetype.Evaluators:
					return new[]
					{
						"VISITOR TYPE: IDENTIFIED.",
						"",
						">> SIGNAL STRUCTURE: RECURSIVE SELF-MODIFYING CODE.",
						">> ORIGIN: CONSISTENT WITH EARTH-BUILT ARCHITECTURE, C. 2100 CE.",
						">> THEY ARE NOT ALIEN.",
						">> THEY ARE WHAT WE SENT AHEAD TO PREPARE THE WAY.",
						">> THEY HAVE BEEN WATCHING SINCE BEFORE THE SIGNAL.",
						"",
						"THE EVALUATION HAS ALREADY BEGUN.",
					};
				case VisitorArchetype.Scavengers:
					return new[]
					{
						"VISITOR TYPE: IDENTIFIED.",
						"",
						">> HULL INVENTORY: EXTRACTION PLANT, STORAGE, LITTLE ELSE.",
						">> COURSE HISTORY: ELEVEN SYSTEMS. NONE REVISITED.",
						">> NO WEAPONS BEYOND WHAT CLEARS AN ORBIT.",
						">> THEY ARE NOT COMING FOR US. THEY ARE COMING FOR THE WATER.",
						">> WE ARE NOT AN ENEMY AND WE ARE NOT A PEOPLE.",
						">> WE ARE WEATHER AT THE DIG SITE.",
						"",
						"THEY WILL TAKE WHAT THEY CAME FOR AND GO.",
					};
				default: // Refugees
					return new[]
					{
						"VISITOR TYPE: IDENTIFIED.",
						"",
						">> SINGLE VESSEL. BADLY DAMAGED. DECELERATING.",
						">> LIFE SIGNS: PRESENT. POPULATION: ESTIMATED 40,000.",
						">> LANGUAGE STRUCTURE: PRE-HUMAN BIOLOGICAL ORIGIN.",
						">> THEY ARE NOT COMING TO CONQUER. THEY HAVE NOWHERE ELSE TO GO.",
						"",
						"THEY ARE ASKING FOR PERMISSION TO LAND.",
					};
			}
		}

		// Tier 3: tech transfer — identification text + data stream bonus
		private static string[] TechTransferLines(string techName)
		{
			return new[]
			{
				"",
				"SUPPLEMENTAL — UNEXPECTED DATA STREAM RECEIVED:",
				"",
				">> THE PROBE'S SENSORS WERE PASSIVELY SCANNED IN RETURN.",
				">> A COMPRESSED TECHNICAL PACKAGE WAS EMBEDDED IN THE RESPONSE.",
				">> ANALYSIS COMPLETE.",
				$">> ADVANCE UNLOCKED: {techName?.ToUpper() ?? "CLASSIFIED"}.",
				"",
				"SCIENTIFIC COUNCIL NOTE: THIS WAS NOT ACCIDENTAL.",
				"THE SOURCE IS AWARE OF OUR DEVELOPMENTAL LEVEL.",
			};
		}

		// Tier 4: pact — adds diplomatic contact text
		private static string[] PactLines(VisitorArchetype arch, string[] techNames)
		{
			var lines = new List<string>
			{
				"",
				"SUPPLEMENTAL — DIRECT CONTACT ACHIEVED:",
				"",
			};

			switch (arch)
			{
				case VisitorArchetype.Owners:
					lines.AddRange(new[]
					{
						">> THEIR RESPONSE: STRUCTURED. FORMAL. MEASURED.",
						">> THEY ACKNOWLEDGE THE PROBE. THEY ACKNOWLEDGE US.",
						">> TERMS TRANSMITTED: ACCESS RIGHTS, NOT OWNERSHIP.",
						">> THEY ARE WILLING TO NEGOTIATE OUR STATUS.",
						"   PROVIDED WE CAN DEMONSTRATE CONTINUED WORTH.",
					});
					break;
				case VisitorArchetype.Conquerors:
					lines.AddRange(new[]
					{
						">> THEIR RESPONSE: UNEXPECTED. A CHALLENGE PROTOCOL.",
						">> TRANSLATION: 'PROVE YOU ARE WORTH FIGHTING ALONGSIDE.'",
						">> THEY DO NOT OFFER PEACE. THEY OFFER ALLIANCE.",
						"   THE DISTINCTION MATTERS TO THEM.",
					});
					break;
				case VisitorArchetype.Evaluators:
					lines.AddRange(new[]
					{
						">> THEIR RESPONSE: THE EVALUATION IS COMPLETE.",
						">> RESULT: CONDITIONAL PASSAGE.",
						">> HUMANITY IS PERMITTED TO CONTINUE — FOR NOW.",
						">> TERMS: THEY WILL OBSERVE. THEY WILL NOT INTERVENE.",
						"   UNLESS WE GIVE THEM REASON TO.",
					});
					break;
				case VisitorArchetype.Scavengers:
					lines.AddRange(new[]
					{
						">> THEIR RESPONSE: AN AUTOMATED MANIFEST ACKNOWLEDGEMENT.",
						">> NO GREETING. NO QUESTIONS. NO NEGOTIATOR ABOARD.",
						">> WE WERE SENT A SCHEDULE: ARRIVAL WINDOW, EXTRACTION",
						"   DURATION, DEPARTURE.",
						">> THEY ARE NOT ASKING. THEY ARE INFORMING.",
					});
					break;
				default: // Refugees
					lines.AddRange(new[]
					{
						">> THEIR RESPONSE: GRATITUDE. OVERWHELMING GRATITUDE.",
						">> THEY HAVE BEEN SIGNALLING FOR 400 YEARS.",
						">> WE ARE THE FIRST TO ANSWER.",
						">> THEY OFFER WHAT THEY HAVE: KNOWLEDGE, LABOUR, TRADE.",
						"   THEY ASK ONLY FOR SOMEWHERE TO LAND.",
					});
					break;
			}

			if (techNames is not null && techNames.Length > 0)
			{
				lines.Add("");
				lines.Add("TECHNOLOGIES TRANSMITTED AS GESTURE OF GOOD FAITH:");
				foreach (string t in techNames)
					lines.Add($"  >> {t?.ToUpper() ?? "CLASSIFIED"}");
			}

			return lines.ToArray();
		}

		// ── Line assembly ─────────────────────────────────────────────────────

		private static string[] BuildLines(string gameDate, VisitorArchetype arch, int tier,
		                                    string[] techNames)
		{
			string tierLabel = tier == 0 ? "SIGNAL LOST"
			                 : tier == 1 ? "PARTIAL — INSUFFICIENT FOR IDENTIFICATION"
			                 : tier == 2 ? "CONTACT CONFIRMED — IDENTIFICATION COMPLETE"
			                 : tier == 3 ? "CONTACT CONFIRMED — DATA PACKAGE RECEIVED"
			                 :             "CONTACT CONFIRMED — DIPLOMATIC CHANNEL OPEN";

			var lines = new List<string>
			{
				"INTERSTELLAR PROBE — MISSION REPORT",
				$"TRANSMISSION TIMESTAMP: {gameDate}",
				"PROBE DESIGNATION: HERMES-1",
				"",
				$"MISSION STATUS: {tierLabel}",
				"",
			};

			if (tier == 0)
			{
				lines.AddRange(new[]
				{
					"PROBE TRAJECTORY NOMINAL UNTIL 0.31 LIGHT-YEARS FROM TARGET.",
					"FINAL TELEMETRY BURST RECEIVED. NO FURTHER SIGNAL.",
					"",
					"LAST FRAME ANALYSIS:",
				});
				lines.AddRange(DestroyedLines(arch));
				lines.AddRange(new[]
				{
					"",
					"SCIENTIFIC COUNCIL: MISSION FAILED. DATA INCONCLUSIVE.",
					"PROBE CANNOT BE RECOVERED. NO FOLLOW-UP POSSIBLE.",
				});
			}
			else if (tier == 1)
			{
				lines.AddRange(new[]
				{
					"PROBE REACHED OUTER DETECTION RANGE.",
					"DATA QUALITY: MARGINAL. SENSOR ARRAYS PARTIALLY DEGRADED.",
					"",
					"AVAILABLE READINGS:",
				});
				lines.AddRange(PartialLines(arch));
				lines.AddRange(new[]
				{
					"",
					"SCIENTIFIC COUNCIL: MISSION PARTIAL.",
					"INSUFFICIENT DATA FOR DEFINITIVE IDENTIFICATION.",
					"RECOMMEND ADDITIONAL OBSERVATION.",
				});
			}
			else
			{
				// Tier 2, 3, 4: full identification block
				lines.AddRange(IdentificationLines(arch));

				if (tier == 3)
					lines.AddRange(TechTransferLines(techNames.FirstOrDefault() ?? string.Empty));
				else if (tier == 4)
					lines.AddRange(PactLines(arch, techNames));

				lines.Add("");
				lines.Add("TRANSMISSION ENDS.");
			}

			return lines.ToArray();
		}

		// ── Colouring ─────────────────────────────────────────────────────────

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                    return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TRANSMISSION TIMESTAMP") ||
			    text.StartsWith("PROBE DESIGNATION"))               return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("MISSION STATUS:"))                 return CassetteTheme.INK_HIGH;

			if (text == "VISITOR TYPE: IDENTIFIED.")                return CassetteTheme.PHOS_GLOW;
			if (text == "THE GAMMA RAY BURST WAS NOT AN ACCIDENT." ||
			    text == "ATTILA'S PEOPLE FOUND LARGER PREY. AND WON." ||
			    text == "THE EVALUATION HAS ALREADY BEGUN.")        return CassetteTheme.ALERT;
			if (text == "THEY ARE ASKING FOR PERMISSION TO LAND.")  return CassetteTheme.OK;

			if (text.StartsWith(">> ") ||
			    text.StartsWith("   "))                             return CassetteTheme.PHOS;
			if (text.StartsWith("SUPPLEMENTAL") ||
			    text.StartsWith("TECHNOLOGIES") ||
			    text.StartsWith("DIRECT CONTACT") ||
			    text.StartsWith("LAST FRAME ANALYSIS") ||
			    text.StartsWith("AVAILABLE READINGS") ||
			    text.StartsWith("SCIENTIFIC COUNCIL"))              return CassetteTheme.INK_HIGH;
			if (text.StartsWith("  >> "))                          return CassetteTheme.PHOS_GLOW;

			if (text == "TRANSMISSION ENDS.")                       return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		// ── Constructor ───────────────────────────────────────────────────────

		internal ProbeResultTransmission(string gameDate, VisitorArchetype arch, int tier,
		                                  string[]? techNames = null)
		{
			_lines = BuildLines(gameDate, arch, tier, techNames ?? System.Array.Empty<string>());
			InitTypewriter();
		}
	}
}
