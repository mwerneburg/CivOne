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
	internal class HydroPlant : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A HYDRO PLANT lets a FACTORY add",
			"100% to shield production instead",
			"of 50%.",
			"",
			"It also HALVES the pollution that",
			"industry produces.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ELECTRONICS.",
			"",
			"Clean and safe, but it may only be",
			"built by a city beside a river or",
			"mountains.",
			"",
			"The HOOVER DAM acts as a hydro",
			"plant in every city on its",
			"continent.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public HydroPlant() : base(24, 4)
		{
			Name = "Hydro Plant";
			RequiredTech = new Electronics();
			SetIcon(4, 2, false);
			SetSmallIcon(3, 4);
			// TODO: Fix icon in patch, should be: SetSmallIcon(3, 3);
			Type = Building.HydroPlant;
		}
	}
}