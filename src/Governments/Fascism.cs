// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Governments
{
	// The late war government of a warmonger who senses he is losing (the user, Oct 2026).
	// NEVER available to the human, autopilot included (Player.AvailableGovernments) — it is
	// something the player faces, not something the player is offered. Adopted through the
	// AI's war ambition (AI.ConsiderFascism), and left the same way, or by collapse.
	internal class Fascism : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"FASCISM",
			"",
			"The regime of a militarist nation",
			"that believes it is losing. No",
			"player may adopt it; it is what",
			"you face across the border.",
			"",
			"Its people feel no weariness of",
			"war and its armies are cheap to",
			"keep. It takes no prisoners: a",
			"city it captures loses HALF its",
			"people.",
			"",
			"Trade will not restrain it, and",
			"the United Nations will not",
			"shield you from it.",
		};

		private static readonly string[] _page2 =
		{
			"It pays for all of this. Its",
			"SCIENCE is halved, its CULTURE",
			"counts for nothing, and every",
			"other nation holds a grudge",
			"against it from the day it rises.",
			"",
			"It rarely lasts. When the war it",
			"was raised for dies down it",
			"returns to an ordinary",
			"government; if it loses a quarter",
			"of its cities it COLLAPSES into",
			"ANARCHY.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Fascism() : base(6, "Fascism", "Fascist", new Advances.Conscription())
		{
			CorruptionMultiplier = 16;   // Monarchy's
			MartialLaw = true;
			FreeUnitSupport = 4;
			SettlerFoodCost = 1;
			SpecialResourceTradeBonus = 1;
			ScienceBonus = -50;
		}
	}
}
