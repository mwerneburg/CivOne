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
	// Phase 1 of the Owners invasion: the reclamation fleet opens with orbital
	// strikes on every civilization's capital. A Fusion Core holder's capital is
	// saved by its space-based interceptors — the one bright moment in the
	// sequence. Unlike the old Reclamation ending, the game continues from here:
	// the war is playable.
	internal class OwnersStrikeTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate, string? coreHolder, int struck)
		{
			var lines = new List<string>
			{
				"PRIORITY OMEGA — PLANETARY ALERT",
				$"CONTACT TIMESTAMP: {gameDate}",
				"STATUS: ORBITAL — BOMBARDMENT IN PROGRESS",
				"",
				"THE FLEET HAS ARRIVED. IT DID NOT HAIL.",
				"IT CATALOGUED. THEN IT FIRED.",
				"",
				$"TRACKING: {struck} OBJECTS DEORBITING.",
				"TRAJECTORY ANALYSIS: NOT RANDOM.",
				"TARGETS: SEATS OF GOVERNMENT. ALL OF THEM.",
				"",
				"[INTERCEPTED BROADCAST — TRANSLATED]",
				">> 'NOTICE OF REPOSSESSION.'",
				">> 'THE ASSETS DEGRADED IN STORAGE.'",
				">> 'ADMINISTRATIVE CENTRES ARE NOT REQUIRED'",
				">> 'FOR INVENTORY PROCESSING.'",
				"",
			};
			if (coreHolder is not null)
			{
				lines.Add($"{coreHolder.ToUpper()} INTERCEPTORS RESPONDING.");
				lines.Add("ONE CAPITAL WILL SEE MORNING.");
			}
			else
			{
				lines.Add("PLANETARY DEFENCE: NONE STOOD READY.");
			}
			lines.AddRange(new[]
			{
				"",
				"CAPITALS BURN. THE MANIFEST IS BEING READ.",
				"THE RECOVERY FLEET IS DESCENDING.",
				"",
				"RECOMMENDATION: NONE AVAILABLE.",
				"SURVIVE.",
				"",
				"TRANSMISSION ENDS.",
			});
			return lines.ToArray();
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                     return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("CONTACT TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                         return CassetteTheme.PHOS_DIM;
			if (text == "[INTERCEPTED BROADCAST — TRANSLATED]")     return CassetteTheme.INK_HIGH;
			if (text.StartsWith(">> "))                             return CassetteTheme.PHOS;
			if (text.EndsWith("INTERCEPTORS RESPONDING.") ||
			    text == "ONE CAPITAL WILL SEE MORNING.")            return CassetteTheme.OK;
			if (text.StartsWith("TRACKING:") ||
			    text.StartsWith("TARGETS:") ||
			    text.StartsWith("PLANETARY DEFENCE: NONE") ||
			    text.StartsWith("CAPITALS BURN") ||
			    text == "THE RECOVERY FLEET IS DESCENDING.")        return CassetteTheme.ALERT;
			if (text == "SURVIVE.")                                 return CassetteTheme.PHOS_GLOW;
			if (text == "TRANSMISSION ENDS.")                       return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal OwnersStrikeTransmission(string gameDate, string? coreHolder, int struck)
		{
			_lines = BuildLines(gameDate, coreHolder, struck);
			InitTypewriter();
		}
	}
}
