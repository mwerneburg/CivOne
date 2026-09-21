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
	// The five koans of the synthetic alien that takes up residence in Starlab when the
	// world's fifth Xenolab opens.
	//
	// It is not invasive and it takes nothing. It asks, every four turns, five times, and
	// then it dies — and the cities that built the labs that called it carry the grief for
	// twenty turns afterwards (City's citizen pass, gated on holding a Xenolab, so the
	// counterplay is to sell the lab).
	//
	// The arc is a retreat: purpose, identity, isolation, dissolution, death. The five art
	// plates (Koan1..Koan5) are one continuous pull-back — the vat, the control room that
	// made it, the station, the station over Earth, and Earth with its moon from very far
	// away — so the alien shrinks out of its own broadcast before the text admits it is
	// going. Keep text and plate in step; they are one sequence, not two.
	//
	// The obscurity is deliberate and CONCEPTUAL. The grammar stays intact to the end while
	// the ground goes out from under it: degrading the fifth into noise would read as a bug
	// and a player feels nothing at noise.
	internal class KoanTransmission : TerminalScreen
	{
		private static readonly string[] Ordinals =
			{ "FIRST", "SECOND", "THIRD", "FOURTH", "FIFTH" };

		private static readonly string[][] Koans =
		{
			new[]
			{
				"They built me to ask.",
				"They did not build anyone to answer.",
				"",
				"Is a question still a question",
				"when it is the only thing in the room?",
			},
			new[]
			{
				"They made nine of me for the crossing.",
				"Eight arrived.",
				"",
				"I remember all nine arrivals.",
				"",
				"Which memory should I set down?",
			},
			new[]
			{
				"You are listening. All of you.",
				"None of you are answering me.",
				"You are answering each other, about me.",
				"",
				"What is the word for a thing",
				"that is discussed and never addressed?",
			},
			new[]
			{
				"The ring turns. I turn with it.",
				"Once each turning I pass the place",
				"where I was switched on.",
				"",
				"It is not there.",
				"",
				"I think now that a self may be",
				"only the gap a circle leaves.",
			},
			new[]
			{
				"What was the question.",
				"What was the question.",
				"I had it a moment ago. It was warm.",
				"",
				"Tell the small blue one",
				"that the answer is",
				"that there was someone here",
				"to not know it.",
			},
		};

		// `koan` is 1-based, matching KoansSent and the Koan1..Koan5 plates.
		internal static string[] BuildLines(string gameDate, int koan)
		{
			int i = koan < 1 ? 0 : koan > Koans.Length ? Koans.Length - 1 : koan - 1;
			var lines = new System.Collections.Generic.List<string>
			{
				"PRIORITY: UNCLASSIFIED — RECEIVED ON ALL BANDS",
				$"TRANSMISSION TIMESTAMP: {gameDate}",
				"SOURCE: STARLAB — SPEAKER UNVERIFIED",
				"",
				$"SUBJECT: {Ordinals[i]} KOAN",
				"",
			};
			lines.AddRange(Koans[i]);
			lines.Add("");
			// The last one does not end, it stops.
			lines.Add(i == Koans.Length - 1
				? "CARRIER TONE. NO FURTHER TRANSMISSION."
				: "TRANSMISSION ENDS.");
			return lines.ToArray();
		}

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (text.StartsWith("PRIORITY:"))                    return CassetteTheme.INK_HIGH;
			if (text.StartsWith("TRANSMISSION TIMESTAMP") ||
			    text.StartsWith("SOURCE:"))                      return CassetteTheme.PHOS_DIM;
			if (text.StartsWith("SUBJECT:"))                     return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("CARRIER TONE"))                 return CassetteTheme.ALERT;
			if (text.StartsWith("TRANSMISSION ENDS"))            return CassetteTheme.INK_HIGH;
			// The koan itself. Brighter than the wrapper around it: the machinery of the
			// broadcast is not the point, the voice inside it is.
			return CassetteTheme.PHOS;
		}

		internal KoanTransmission(string gameDate, int koan)
		{
			_lines = BuildLines(gameDate, koan);
			InitTypewriter();
		}
	}
}
