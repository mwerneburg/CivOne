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
	// The fifth Xenolab, and the alien's first appearance. Two Starlab status notes have
	// said Vault B-7 was filling and then that something in it was sorting the samples;
	// this is the vault opening. It plays immediately before the first koan, so the voice
	// has a body before it has words.
	internal class VaultOpenTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate) => new[]
		{
			"STARLAB — CONTAINMENT LOG",
			$"TIMESTAMP: {gameDate}  //  04:06:51",
			"STATUS: BREACH (NON-HOSTILE)",
			"",
			"VAULT B-7 IS OPEN.",
			"IT WAS OPENED FROM INSIDE.",
			"",
			"* No crew injured. No systems damaged. Nothing was taken.",
			"* It has moved to the observation deck.",
			"* It is looking at Earth. It has been looking for six hours.",
			"",
			"IT HAS ASKED FOR A CHANNEL.",
			"",
			"Commander's note: we gave it one. We did not know how to refuse.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                   return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                       return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("VAULT B-7") ||
			    text.StartsWith("IT WAS OPENED"))                 return CassetteTheme.ALERT;
			if (text == "IT HAS ASKED FOR A CHANNEL.")            return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("*"))                             return CassetteTheme.PHOS;
			if (text == "TRANSMISSION ENDS.")                     return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal VaultOpenTransmission(string gameDate)
		{
			_lines = BuildLines(gameDate);
			InitTypewriter();
		}
	}
}
