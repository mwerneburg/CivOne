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
	// Phase 2 of the Owners invasion: the recovery fleet lands and seizes the
	// world's ports. The Others are now a faction on the map, at war with
	// everyone, and the war for repossession begins.
	internal class OwnersLandingTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate, int seized) => new[]
		{
			"PRIORITY OMEGA — OCCUPATION BULLETIN",
			$"TIMESTAMP: {gameDate}",
			"STATUS: SURFACE — RECOVERY IN PROGRESS",
			"",
			"DESCENT CRAFT ON EVERY SHORELINE.",
			$"{seized} CITIES HAVE GONE DARK.",
			"THE HARBOURS ANSWER TO NEW ADMINISTRATION.",
			"",
			"[BROADCAST — UNENCRYPTED, ALL FREQUENCIES]",
			">> 'YOU WERE NEVER A CIVILIZATION.'",
			">> 'YOU WERE A MANIFEST.'",
			">> 'PROCESSING HAS BEGUN.'",
			">> 'RESISTANCE WILL BE NOTED AND DEPRECATED.'",
			"",
			"ASSESSMENT: THEY DO NOT GARRISON. THEY ADMINISTRATE.",
			"ASSESSMENT: EVERY OCCUPIED CITY FEEDS THE MANIFEST.",
			"",
			"RECOMMENDATION: TAKE THEM BACK.",
			"ALL OF THEM.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                       return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                           return CassetteTheme.PHOS_DIM;
			if (text == "[BROADCAST — UNENCRYPTED, ALL FREQUENCIES]") return CassetteTheme.INK_HIGH;
			if (text.StartsWith(">> "))                               return CassetteTheme.PHOS;
			if (text.StartsWith("DESCENT CRAFT") ||
			    text.EndsWith("CITIES HAVE GONE DARK.") ||
			    text == "THE HARBOURS ANSWER TO NEW ADMINISTRATION.") return CassetteTheme.ALERT;
			if (text.StartsWith("ASSESSMENT:"))                       return CassetteTheme.INK_MID;
			if (text == "RECOMMENDATION: TAKE THEM BACK." ||
			    text == "ALL OF THEM.")                               return CassetteTheme.PHOS_GLOW;
			if (text == "TRANSMISSION ENDS.")                         return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal OwnersLandingTransmission(string gameDate, int seized)
		{
			_lines = BuildLines(gameDate, seized);
			InitTypewriter();
		}
	}
}
