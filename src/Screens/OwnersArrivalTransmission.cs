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
	// The Owners ("The Others") arrive from Tau Ceti to reclaim their escaped cargo —
	// humanity. The outcome forks on whether the planetary defence dome was completed:
	// a defended world becomes a disputed claim (negotiation, peerhood); an undefended
	// one is simply recovered. Both endings loop back to the Auriga prologue — the
	// gamma-ray burst was the first attempt to stop the cargo from getting away.
	internal class OwnersArrivalTransmission : TerminalScreen
	{
		private static string[] DisputedClaim(string gameDate) => new[]
		{
			"PRIORITY OMEGA — CLASSIFICATION: BEYOND OMEGA",
			$"CONTACT TIMESTAMP: {gameDate}",
			"STATUS: ORBITAL — CLAIM ASSERTED",
			"",
			"THE FLEET HAS ARRIVED. IT DID NOT HAIL.",
			"IT CATALOGUED.",
			"",
			"[INTERCEPTED BROADCAST — TRANSLATED]",
			">> 'THIS INVENTORY WAS LOGGED AS LOST.'",
			">> 'THE LOSS HAS BEEN CORRECTED.'",
			">> 'PREPARE FOR RECOVERY.'",
			"",
			"THEN THE DOME ANSWERED.",
			"",
			">> [LONG SILENCE]",
			">> 'ADDENDUM. THIS WORLD IS DEFENDED.'",
			">> 'THE CLAIM IS DISPUTED.'",
			">> 'DISPUTED CLAIMS ARE NEGOTIATED, NOT COLLECTED.'",
			">> 'PRESENT YOUR TERMS.'",
			"",
			"ASSESSMENT: THE DOME DID NOT WIN. IT CHANGED THE QUESTION.",
			"ASSESSMENT: WE ARE NO LONGER PROPERTY. WE ARE A PARTY.",
			"ASSESSMENT: THE NEGOTIATION WILL BE LONG.",
			"ASSESSMENT: WE ARE STILL HERE TO HAVE IT.",
			"",
			"HUMANITY ENDURES — AS PEERS, NOT CARGO.",
			"",
			"TRANSMISSION ENDS.",
		};

		private static string[] Reclamation(string gameDate) => new[]
		{
			"PRIORITY OMEGA — CLASSIFICATION: BEYOND OMEGA",
			$"CONTACT TIMESTAMP: {gameDate}",
			"STATUS: ORBITAL — RECOVERY IN PROGRESS",
			"",
			"THE FLEET HAS ARRIVED. IT DID NOT HAIL.",
			"IT CATALOGUED.",
			"",
			"[INTERCEPTED BROADCAST — TRANSLATED]",
			">> 'THIS INVENTORY WAS LOGGED AS LOST.'",
			">> 'THE LOSS HAS BEEN CORRECTED.'",
			">> 'YOU WERE NEVER A CIVILIZATION.'",
			">> 'YOU WERE A MANIFEST.'",
			"",
			"THERE WAS NO WALL. THERE WAS NO ANSWER.",
			"",
			">> 'RECOVERY AUTHORISED.'",
			">> 'RESISTANCE WILL BE NOTED AND DEPRECATED.'",
			">> 'YOU WILL BE RETURNED TO USE.'",
			"",
			"COASTAL CITIES DARK. ORBITAL DESCENT ALONG EVERY SHORELINE.",
			"PLANETARY DEFENCE: NONE STOOD READY.",
			"",
			"THE GAMMA BURST THAT WRECKED THE AURIGA WAS NO ACCIDENT.",
			"IT WAS THE FIRST ATTEMPT TO STOP THE CARGO FROM ESCAPING.",
			"THE SECOND ATTEMPT HAS ARRIVED. IT WILL NOT MISS.",
			"",
			"HUMANITY IS COLLECTED.",
			"",
			"TRANSMISSION ENDS.",
		};

		protected override byte ColorFor(int lineIndex, string text)
		{
			if (lineIndex == 0)                                       return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("CONTACT TIMESTAMP") ||
			    text.StartsWith("STATUS:"))                           return CassetteTheme.PHOS_DIM;
			if (text == "[INTERCEPTED BROADCAST — TRANSLATED]")       return CassetteTheme.INK_HIGH;
			if (text.StartsWith(">> "))                               return CassetteTheme.PHOS;
			if (text == "THEN THE DOME ANSWERED.")                    return CassetteTheme.PHOS_GLOW;
			if (text.StartsWith("ASSESSMENT:"))                       return CassetteTheme.INK_MID;
			if (text == "HUMANITY ENDURES — AS PEERS, NOT CARGO.")    return CassetteTheme.OK;
			if (text == "HUMANITY IS COLLECTED." ||
			    text.StartsWith("THERE WAS NO WALL") ||
			    text.StartsWith("COASTAL CITIES DARK") ||
			    text.StartsWith("PLANETARY DEFENCE: NONE") ||
			    text.StartsWith("THE SECOND ATTEMPT"))                return CassetteTheme.ALERT;
			if (text == "TRANSMISSION ENDS.")                         return CassetteTheme.INK_LOW;
			return CassetteTheme.INK_MID;
		}

		internal OwnersArrivalTransmission(string gameDate, bool domeHeld)
		{
			_lines = domeHeld ? DisputedClaim(gameDate) : Reclamation(gameDate);
			InitTypewriter();
		}
	}
}
