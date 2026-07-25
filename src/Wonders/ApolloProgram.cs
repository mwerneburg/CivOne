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
	internal class ApolloProgram : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The APOLLO PROGRAM carries your",
			"people to the Moon.",
			"",
			"The whole world map is revealed,",
			"and every civilization may begin",
			"building a SPACESHIP.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SPACE FLIGHT.",
			"",
			"It opens the SPACE RACE for all —",
			"so build it only when your own",
			"launch pads are ready to answer.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public ApolloProgram() : base(60)
		{
			Name = "Apollo Program";
			RequiredTech = new SpaceFlight();
			ObsoleteTech = null;
			SetSmallIcon(7, 4);
			Type = Wonder.ApolloProgram;
		}
	}
}