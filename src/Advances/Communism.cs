// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class Communism : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"COMMUNISM holds that the state",
			"should own what the people use.",
			"",
			"Allows the COMMUNISM government",
			"and the POLICE STATION.",
		};

		private static readonly string[] _page2 =
		{
			"Under Communism, corruption no",
			"longer grows with distance from",
			"the capital — the government of",
			"a very large empire.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		// Same prerequisites as The Republic. Communism used to sit behind Philosophy AND
		// Industrialization, which put it so late that every civ had already adopted a
		// republic — and since the AI only changes government for a STRICTLY better score,
		// none of them could legally move to it afterwards. It is a political idea, not an
		// industrial one: China industrialised while communist rather than the other way
		// round. The United Nations moved to Globalism so it does not arrive early with it.
		public Communism() : base(4, 0, 2, Advance.CodeOfLaws, Advance.Literacy)
		{
			Name = "Communism";
			Type = Advance.Communism;
		}
	}
}