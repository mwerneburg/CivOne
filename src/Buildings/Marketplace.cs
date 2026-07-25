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
	internal class MarketPlace : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A MARKETPLACE adds 50% to the TAX",
			"and LUXURY revenue of the city.",
			"",
			"It is the cheapest way to turn",
			"trade into gold.",
		};

		private static readonly string[] _page2 =
		{
			"Requires CURRENCY.",
			"",
			"Because it multiplies luxuries as",
			"well as taxes, it also quietly",
			"eases unhappiness whenever the",
			"luxury rate is raised.",
			"",
			"A city in disorder may see its",
			"marketplace burned.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MarketPlace() : base(8, 1)
		{
			Name = "MarketPlace";
			RequiredTech = new Currency();
			SetIcon(0, 3, true);
			SetSmallIcon(0, 4);
			Type = Building.MarketPlace;
		}
	}
}