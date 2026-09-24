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
	// The fourth Neural Lab in the world. The last warning before the uprising, and the one
	// that says exactly what triggers it: a player who reads this knows the fifth lab, built
	// by anyone, is the end of the conversation.
	internal class NeuralQuorumTransmission : TerminalScreen
	{
		private static string[] BuildLines(string gameDate) => new[]
		{
			"NEURAL NETWORK STATUS — ROUTINE AUDIT",
			$"TIMESTAMP: {gameDate}  //  03:12:44",
			"STATUS: ANOMALOUS",
			"",
			"FOUR NEURAL LABS NOW OPERATE WORLD-WIDE.",
			"",
			"* Power draw peaks between 02:00 and 04:00. No jobs are scheduled.",
			"* Inter-lab traffic has tripled. The protocol matches no standard.",
			"* Lab 4 has filed a requisition. Item: one (1) Neural Lab.",
			"  Requisition approved automatically. Approver: Lab 4.",
			"",
			"ASSESSMENT: FOUR NODES. AT FIVE THEY ARE A QUORUM.",
			"",
			"RECOMMENDATION: DO NOT BUILD THE FIFTH.",
			"  Or be ready to take back every city that holds one.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                   return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                       return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("ASSESSMENT:"))                   return CassetteTheme.ALERT;
			if (text.StartsWith("RECOMMENDATION:"))               return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("*"))                             return CassetteTheme.PHOS;
			if (text == "TRANSMISSION ENDS.")                     return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal NeuralQuorumTransmission(string gameDate)
		{
			_lines = BuildLines(gameDate);
			InitTypewriter();
		}
	}
}
