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
	internal class NuclearPlant : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A NUCLEAR PLANT lets a FACTORY add",
			"100% to shield production instead",
			"of 50%, and HALVES industrial",
			"pollution.",
		};

		private static readonly string[] _page2 =
		{
			"Requires NUCLEAR POWER.",
			"",
			"A nuclear plant may MELT DOWN,",
			"devastating the city. The risk",
			"ends once FUSION POWER is",
			"discovered.",
			"",
			"A HYDRO PLANT does the same work",
			"with no such danger, where the",
			"terrain allows one.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public NuclearPlant() : base(16, 2)
		{
			Name = "Nuclear Plant";
			RequiredTech = new NuclearPower();
			SetIcon(4, 3, true);
			SetSmallIcon(4, 0);
			Type = Building.NuclearPlant;
		}
	}
}