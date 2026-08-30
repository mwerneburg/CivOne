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
	internal class ExchangeCenter : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"An EXCHANGE CENTER hosts ongoing",
			"cultural dialogue between citizens",
			"and alien representatives. What",
			"passes through it does not stay",
			"in one city: the exchange adds 3",
			"to your CULTURE each turn.",
		};

		private static readonly string[] _page2 =
		{
			"Memetic Protocols revealed that",
			"many human conflicts arose from",
			"failures of translation. Alien",
			"observers had no concept of",
			"ideological war. Their bafflement",
			"proved instructive: an Exchange",
			"Center teaches citizens to see",
			"their disputes from the outside.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public ExchangeCenter() : base(8, 1)
		{
			Name = "Exchange Center";
			RequiredTech = new MemeticProtocols();
			Type = Building.ExchangeCenter;
		}
	}
}
