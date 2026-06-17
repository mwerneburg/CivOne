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
	internal class SurplusDepot : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A SURPLUS DEPOT converts excess",
			"food production into taxable",
			"trade. Each turn, half of the",
			"city's positive food surplus is",
			"added to the treasury as gold.",
		};

		private static readonly string[] _page2 =
		{
			"Surplus grain has always had value",
			"beyond the stomach. Ancient cities",
			"built wealth by trading stored",
			"harvests across long distances.",
			"A city with a large surplus and",
			"no room to grow wastes its most",
			"basic resource — the Surplus",
			"Depot puts that waste to work.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public SurplusDepot() : base(6, 1)
		{
			Name = "Surplus Depot";
			RequiredTech = new Trade();
			Type = Building.SurplusDepot;
		}
	}
}
