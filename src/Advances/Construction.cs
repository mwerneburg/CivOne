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
	internal class Construction : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"CONSTRUCTION raises works of",
			"stone and timber beyond the reach",
			"of a village.",
			"",
			"Allows the COLOSSEUM and the",
			"AQUEDUCT.",
		};

		private static readonly string[] _page2 =
		{
			"Without an aqueduct no city grows",
			"past size 6, and crowded cities",
			"without one suffer PLAGUE.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Construction() : base(4, 0, 1, Advance.Masonry, Advance.Currency)
		{
			Name = "Construction";
			Type = Advance.Construction;
		}
	}
}