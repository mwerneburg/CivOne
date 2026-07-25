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
	internal class MfgPlant : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A MANUFACTURING PLANT adds a",
			"further 100% to the SHIELD",
			"production of the city.",
			"",
			"Its effect stacks with the",
			"FACTORY.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ROBOTICS.",
			"",
			"The most powerful production",
			"building in the game, and the most",
			"expensive to build and maintain.",
			"",
			"Reserve it for a handful of great",
			"workshop cities. It pollutes",
			"heavily.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MfgPlant() : base(32, 6)
		{
			Name = "Mfg. Plant";
			RequiredTech = new Robotics();
			SetIcon(3, 2, true);
			SetSmallIcon(3, 0);
			Type = Building.MfgPlant;
		}
	}
}