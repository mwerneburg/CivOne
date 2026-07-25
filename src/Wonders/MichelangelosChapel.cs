// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Wonders
{
	internal class MichelangelosChapel : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"MICHELANGELO'S CHAPEL is a marvel",
			"of sacred art.",
			"",
			"It acts as a CATHEDRAL in every",
			"city on the SAME CONTINENT,",
			"calming their citizens.",
		};

		private static readonly string[] _page2 =
		{
			"Requires RELIGION.",
			"",
			"One chapel spares the cost of",
			"cathedrals across a continent — a",
			"cornerstone of a happy, sprawling",
			"empire.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MichelangelosChapel() : base(30)
		{
			Name = "Michelangelo's Chapel";
			RequiredTech = new Religion();
			ObsoleteTech = new Communism();
			SetSmallIcon(5, 4);
			Type = Wonder.MichelangelosChapel;
		}
	}
}