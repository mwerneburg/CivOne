#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;

namespace CivOne.Governments
{
	internal class Republic : BaseGovernment
	{
		private static readonly string[] _page1 =
		{
			"REPUBLIC",
			"",
			"A trade-driven government.",
			"",
			"Roads on grassland, plains, and",
			"desert yield +1 trade. Ocean and",
			"river tiles also gain +1. Jungle",
			"and mountain specials gain +2.",
			"",
			"Corruption is heavy: cities far",
			"from the capital lose much of",
			"their tax and science to graft.",
			"",
			"When We Love the King Day fires,",
			"a republic city grows or gifts",
			"a Caravan rather than easing",
			"taxation.",
		};

		private static readonly string[] _page2 =
		{
			"Every military unit AWAY from its",
			"home city makes ONE citizen there",
			"unhappy. Diplomats, Caravans, and",
			"Settlers do not count. Use the",
			"HOME CITY command (H) to base a",
			"unit and reset the penalty.",
			"",
			"Women's Suffrage cancels it.",
			"Shakespeare's Theatre shields its",
			"host city from all unhappiness.",
			"",
			"Sustained civil disorder will",
			"topple the Republic and plunge",
			"the nation into anarchy.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Republic() : base(4, "Republic", new TheRepublic())
		{
			CorruptionMultiplier = 24;
		}
	}
}