// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class BronzeWorking : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"BRONZE WORKING gives smiths a",
			"metal hard enough for armour and",
			"spear points.",
			"",
			"Allows the PHALANX and THE",
			"COLOSSUS.",
		};

		private static readonly string[] _page2 =
		{
			"The Phalanx is the first unit",
			"that can hold a city against a",
			"determined neighbour.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public BronzeWorking() : base(5, 2, 0)
		{
			Name = "Bronze Working";
			Type = Advance.BronzeWorking;
		}
	}
}