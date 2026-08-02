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
	internal class Despotism : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"DESPOTISM",
			"",
			"The government every civilization",
			"begins with. One ruler, absolute",
			"authority, and a countryside that",
			"never quite prospers.",
			"",
			"THE TILE PENALTY is what defines",
			"it: any tile yielding three or",
			"more food, shields or trade gives",
			"up one of them.",
			"",
			"That is why irrigating good",
			"grassland under a despot buys you",
			"nothing. Leave the tile, or leave",
			"the government.",
		};

		private static readonly string[] _page2 =
		{
			"Corruption is high, though far",
			"short of anarchy.",
			"",
			"No unit is supported free — each",
			"costs its home city a shield —",
			"and a settler eats one food a",
			"turn.",
			"",
			"Martial law applies: troops in a",
			"city make its citizens content,",
			"so a garrison is cheaper than a",
			"Temple for a while.",
			"",
			"Monarchy lifts the tile penalty.",
			"Take it as soon as it is offered.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Despotism() : base(1, "Despotism")
		{
			CorruptionMultiplier = 8;
			TilePenalty = true;
			FreeUnitSupport = -1;
			SettlerFoodCost = 1;
			MartialLaw = true;
		}
	}
}