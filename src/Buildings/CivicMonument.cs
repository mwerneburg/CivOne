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

namespace CivOne.Buildings
{
	internal class CivicMonument : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A CIVIC MONUMENT is a permanent",
			"public work — a statue, fountain,",
			"or triumphal arch — that lifts",
			"the city's morale. Citizens are",
			"made more content by civic pride.",
		};

		private static readonly string[] _page2 =
		{
			"Great cities have always invested",
			"in symbols of collective identity.",
			"The Roman triumphal arch, the",
			"Athenian stoa, the medieval",
			"market cross — each told citizens",
			"that their city was worth living",
			"in, and worth defending.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public CivicMonument() : base(4, 0)
		{
			Name = "Civic Monument";
			RequiredTech = new Philosophy();
			Type = Building.CivicMonument;
		}
	}
}
