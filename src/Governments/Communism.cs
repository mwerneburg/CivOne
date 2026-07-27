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
	internal class Communism : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"COMMUNISM",
			"",
			"A government of the scientific",
			"state. Every city produces HALF",
			"AGAIN as much SCIENCE.",
			"",
			"It earns no trade bonus from",
			"roads, rivers or ocean — the",
			"republics out-earn it — but it",
			"turns what commerce it has into",
			"research far more efficiently.",
			"",
			"Distance from the capital NO",
			"LONGER matters: corruption is",
			"charged as though every city sat",
			"ten tiles out, so the furthest",
			"province is as governable as the",
			"nearest. A PALACE halves it again.",
		};

		private static readonly string[] _page2 =
		{
			"MARTIAL LAW: each military unit",
			"garrisoned in a city keeps one",
			"citizen content, up to three.",
			"",
			"Its people feel no unhappiness",
			"over armies abroad, and sustained",
			"disorder will not topple the",
			"state — where a republic or a",
			"democracy would collapse into",
			"ANARCHY.",
			"",
			"The government of a very large",
			"empire that intends to out-think",
			"its rivals rather than out-trade",
			"them.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Communism() : base(3, "Communism", "Communist", new Advances.Communism())
		{
			CorruptionMultiplier = 20;
			MartialLaw = true;
			FreeUnitSupport = 3;
			ScienceBonus = 50;
			SpecialResourceTradeBonus = 1;
			FixedCorruptionDistance = 10;
			PalaceHalvesCorruption = true;
		}
	}
}