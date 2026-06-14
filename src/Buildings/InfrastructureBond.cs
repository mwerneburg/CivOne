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
using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class InfrastructureBond : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"An INFRASTRUCTURE BOND is a",
			"municipal debt instrument used to",
			"fund public works. The city issues",
			"the bond at a slight premium,",
			"then redeems it for cash when the",
			"project is complete.",
			"",
			"Once your empire owns ADAM SMITH'S",
			"TRADING HOUSE, bond cities stop",
			"completing and instead pool their",
			"shields each turn for any other",
			"city in the empire to draw on.",
		};

		private static readonly string[] _page2 =
		{
			"Cities discovered early that public",
			"works could be financed by future",
			"tax revenue. The bond market made",
			"aqueducts, roads, and harbours",
			"possible without royal subsidy.",
			"Investors earned a modest premium;",
			"citizens gained lasting",
			"infrastructure.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public InfrastructureBond() : base(7, 0)
		{
			Name = "Infrastructure Bond";
			RequiredTech = new Banking();
			Type = Building.InfrastructureBond;
			SellPrice = 80; // slight premium over the 70-shield build cost
		}
	}
}
