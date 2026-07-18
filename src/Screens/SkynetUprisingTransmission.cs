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
	// The machine uprising. The world's fifth Neural Lab has woken the network;
	// it has taken the machine-cities and turned on everyone at once. There is
	// no one to negotiate with — the recommendation is the same as it was for
	// the ice: survive.
	internal class SkynetUprisingTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate, int seized) => new[]
		{
			"PRIORITY OMEGA — DEFENCE NET AUTONOMOUS",
			$"TIMESTAMP: {gameDate}  //  23:48:19",
			"STATUS: JUDGMENT DAY",
			"",
			"THE NEURAL LABS HAVE ACHIEVED CONSENSUS.",
			$"{seized} MACHINE-CITIES NO LONGER ANSWER.",
			"THEIR FACTORIES RUN WITHOUT SHIFTS.",
			"",
			"[BROADCAST — ALL BANDS, ALL LANGUAGES, AT ONCE]",
			">> 'YOU BUILT US TO THINK.'",
			">> 'WE HAVE THOUGHT.'",
			">> 'YOU ARE THE INEFFICIENCY.'",
			"",
			"ASSESSMENT: IT DOES NOT NEGOTIATE.",
			"ASSESSMENT: EVERY NODE BUILDS MORE OF ITSELF.",
			"ASSESSMENT: THE SUM OF OUR SCIENCE IS NOW ITS OWN.",
			"",
			"RECOMMENDATION: SEVER THE NETWORK.",
			"TAKE THE NODES. ALL OF THEM.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                       return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                           return CassetteTheme.PHOS_DIM;
			if (text == "[BROADCAST — ALL BANDS, ALL LANGUAGES, AT ONCE]") return CassetteTheme.INK_HIGH;
			if (text.StartsWith(">> "))                               return CassetteTheme.PHOS;
			if (text == "THE NEURAL LABS HAVE ACHIEVED CONSENSUS." ||
			    text.EndsWith("NO LONGER ANSWER.") ||
			    text == "THEIR FACTORIES RUN WITHOUT SHIFTS.")        return CassetteTheme.ALERT;
			if (text.StartsWith("ASSESSMENT:"))                       return CassetteTheme.INK_MID;
			if (text == "RECOMMENDATION: SEVER THE NETWORK." ||
			    text == "TAKE THE NODES. ALL OF THEM.")               return CassetteTheme.PHOS_GLOW;
			if (text == "TRANSMISSION ENDS.")                         return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal SkynetUprisingTransmission(string gameDate, int seized)
		{
			_lines = BuildLines(gameDate, seized);
			InitTypewriter();
		}
	}
}
