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
	// The cursed outcome of the South Pole Expedition: the anomaly was not
	// propulsion hardware. Ground zero has gone dark, the five-turn clock is
	// running, and the only recommendation on file is fire.
	internal class ThingOutbreakTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate, string city) => new[]
		{
			"PRIORITY OMEGA — EXPEDITION QUARANTINE",
			$"TIMESTAMP: {gameDate}",
			"STATUS: CONTAINMENT FAILED",
			"",
			"THE EXPEDITION BROUGHT SOMETHING BACK.",
			$"{city.ToUpper()} HAS STOPPED ANSWERING.",
			"LAST CONTACT: A REQUEST FOR MEDICAL SUPPLIES.",
			"THEN NOTHING. THEN THE WRONG VOICES.",
			"",
			"[RECOVERED — NORWEGIAN FIELD JOURNAL, PARTIAL]",
			">> 'IT IS NOT DEAD.'",
			">> 'IT IS NOT ONE.'",
			">> 'BURN IT. BURN ALL OF IT.'",
			"",
			$"ASSESSMENT: WHATEVER WALKS IN {city.ToUpper()} IS NOT ITS PEOPLE.",
			"ASSESSMENT: IF IT STANDS IN FIVE YEARS, TWO MORE CITIES FOLLOW.",
			"ASSESSMENT: THE OCEAN IS THE ONLY BORDER IT RESPECTS.",
			"",
			"RECOMMENDATION: DESTROY THE CITY.",
			"DO NOT WAIT. DO NOT WATCH.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                        return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                            return CassetteTheme.PHOS_DIM;
			if (text == "[RECOVERED — NORWEGIAN FIELD JOURNAL, PARTIAL]") return CassetteTheme.INK_HIGH;
			if (text.StartsWith(">> "))                                return CassetteTheme.PHOS;
			if (text == "THE EXPEDITION BROUGHT SOMETHING BACK." ||
			    text.EndsWith("HAS STOPPED ANSWERING.") ||
			    text == "THEN NOTHING. THEN THE WRONG VOICES.")        return CassetteTheme.ALERT;
			if (text.StartsWith("ASSESSMENT:"))                        return CassetteTheme.INK_MID;
			if (text == "RECOMMENDATION: DESTROY THE CITY." ||
			    text == "DO NOT WAIT. DO NOT WATCH.")                  return CassetteTheme.PHOS_GLOW;
			if (text == "TRANSMISSION ENDS.")                          return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal ThingOutbreakTransmission(string gameDate, string city)
		{
			_lines = BuildLines(gameDate, city);
			InitTypewriter();
		}
	}
}
