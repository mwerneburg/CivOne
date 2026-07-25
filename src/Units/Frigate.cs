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

namespace CivOne.Units
{
	internal class Frigate : BaseUnitSea, IBoardable
	{
		public int Cargo
		{
			get
			{
				return 4;
			}
		}

		private static readonly string[] _page1 =
		{
			"The FRIGATE carries 4 land units",
			"and can fight, unlike the merchant",
			"hulls before it.",
			"",
			"A warship and a troop ship in one.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MAGNETISM.",
			"",
			"It remains useful long after the",
			"IRONCLAD appears, because the",
			"ironclad carries nothing.",
			"",
			"Escort invasions with it, and",
			"raid coastal cities where the",
			"defenders are weak.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Frigate() : base(4, 2, 2, 3)
		{
			Type = UnitType.Frigate;
			Name = "Frigate";
			RequiredTech = new Magnetism();
			ObsoleteTech = null;
			SetIcon('B', 1, 0);
		}
	}
}