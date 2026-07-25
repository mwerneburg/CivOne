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
	internal class Factory : BaseBuilding
	{
		private static readonly string[] _page1 =
		{
			"A FACTORY adds 50% to the SHIELD",
			"production of the city.",
			"",
			"With a POWER PLANT, HYDRO PLANT or",
			"NUCLEAR PLANT it adds 100%",
			"instead.",
		};

		private static readonly string[] _page2 =
		{
			"Requires INDUSTRIALIZATION.",
			"",
			"Industry breeds POLLUTION. Expect",
			"smoke on your tiles, and plan for",
			"a MASS TRANSIT or RECYCLING",
			"CENTER.",
			"",
			"The HOOVER DAM powers every",
			"factory on its continent.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Factory() : base(20, 4)
		{
			Name = "Factory";
			RequiredTech = new Industrialization();
			SetIcon(3, 1, true);
			SetSmallIcon(2, 4);
			Type = Building.Factory;
		}
	}
}