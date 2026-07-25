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
	internal class Medicine : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MEDICINE studies the body and its",
			"ailments in an orderly way.",
			"",
			"Allows SHAKESPEARE'S THEATRE.",
		};

		private static readonly string[] _page2 =
		{
			"Medicine ends the PLAGUE that",
			"strikes crowded cities lacking an",
			"AQUEDUCT.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Medicine() : base(3, 1, 2, Advance.Philosophy, Advance.Trade)
		{
			Name = "Medicine";
			Type = Advance.Medicine;
		}
	}
}