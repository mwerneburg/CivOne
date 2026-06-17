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
	internal class SouthPoleIntelReport : TerminalScreen
	{
		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0) return CassetteTheme.ALERT;
			if (lineIndex == 1) return CassetteTheme.PHOS_DIM;
			return CassetteTheme.INK_MID;
		}

		public SouthPoleIntelReport(string gameYear)
		{
			string[] intelLines = SouthPoleExpeditionLog.LoadIntelLines()
				?? new[]
				{
					"Satellite imagery has revealed an anomalous formation at the South Pole.",
					"Norwegian scientists confirm the structure is of non-terrestrial origin.",
					"A classified expedition has been dispatched. Details: EYES ONLY."
				};

			var lines = new List<string>();
			lines.Add("CLASSIFIED INTELLIGENCE REPORT");
			lines.Add($"TRANSMISSION TIMESTAMP: {gameYear}");
			lines.Add("");
			foreach (string line in intelLines)
				lines.Add(line.Replace("{game year}", gameYear));
			_lines = lines.ToArray();
			InitTypewriter();
		}
	}
}
