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
	internal class Democracy : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"DEMOCRACY",
			"",
			"The wealthiest government.",
			"",
			"Roads, oceans, and rivers grant",
			"+1 trade as under the Republic.",
			"Jungle and mountain specials grant",
			"+2. CORRUPTION IS ZERO — distant",
			"cities pay no graft tax.",
			"",
			"We Love the King Day grows the",
			"city or gifts a Caravan when food",
			"income is positive.",
		};

		private static readonly string[] _page2 =
		{
			"Every military unit AWAY from its",
			"home city makes TWO citizens there",
			"unhappy — double the Republic's",
			"penalty. Diplomats, Caravans, and",
			"Settlers are exempt. Use H to",
			"set a unit's home city.",
			"",
			"Women's Suffrage cuts it to one.",
			"Shakespeare's Theatre shields its",
			"host city from all unhappiness.",
			"",
			"A democracy that falls into",
			"sustained disorder collapses into",
			"anarchy. Peace is not preferable —",
			"it is required.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Democracy() : base(5, "Democracy", "Democratic", new Advances.Democracy())
		{
			CorruptionMultiplier = 0;
			TradeBonus = 1;
			WarWeariness = 2;
			MartialLaw = false;
			CollapsesInDisorder = true;
			CelebrationGrowsCity = true;
			SpecialResourceTradeBonus = 2;
		}
	}
}