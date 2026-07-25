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
			"Allows the COMMUNISM government,",
			"the POLICE STATION and THE UNITED",
			"NATIONS.",
		};

		private static readonly string[] _page2 =
		{
			"Under Communism, corruption no",
			"longer grows with distance from",
			"the capital — the government of",
			"a very large empire.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Communism() : base(4, 0, 2, Advance.Philosophy, Advance.Industrialization)
		{
			Name = "Communism";
			Type = Advance.Communism;
		}
	}
}