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
	internal class MassTransit : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"MASS TRANSIT eliminates the",
			"POLLUTION caused by the city's",
			"POPULATION entirely.",
			"",
			"It also improves the flow of goods",
			"and labour, adding 20% to food and",
			"shields.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MASS PRODUCTION.",
			"",
			"Population pollution grows with",
			"size and with advanced technology,",
			"so the largest modern cities need",
			"this most.",
			"",
			"Industrial smoke is untouched by",
			"it; that needs a RECYCLING CENTER.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MassTransit() : base(24, 4)
		{
			Name = "Mass Transit";
			RequiredTech = new MassProduction();
			SetIcon(2, 3, false);
			SetSmallIcon(2, 2);
			Type = Building.MassTransit;
		}
	}
}