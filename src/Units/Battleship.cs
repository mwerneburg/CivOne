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
	internal class Battleship : BaseUnitSea
	{
		private static readonly string[] _page1 =
		{
			"The BATTLESHIP is the most",
			"powerful vessel afloat, with guns",
			"and armour beyond any other ship.",
		};

		private static readonly string[] _page2 =
		{
			"Requires STEEL.",
			"Needs OIL: +50% shields without.",
			"",
			"Enormously expensive. One is a",
			"fleet in itself; two are a burden",
			"on the treasury.",
			"",
			"It has no answer to aircraft",
			"except the ships around it, so",
			"sail it with a CARRIER or within",
			"reach of your own FIGHTERS.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Battleship() : base(16, 18, 12, 4, 2)
		{
			Type = UnitType.Battleship;
			Name = "Battleship";
			RequiredTech = new Steel();
			ObsoleteTech = null;
			SetIcon('A', 1, 0);
		}
	}
}