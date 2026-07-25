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
	internal class Philosophy : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"PHILOSOPHY asks what is true and",
			"how one might know it.",
			"",
			"Allows the CIVIC MONUMENT.",
		};

		private static readonly string[] _page2 =
		{
			"More paths leave Philosophy than",
			"any other advance in the tree.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Philosophy() : base(8, 1, 0, Advance.Mysticism, Advance.Literacy)
		{
			Name = "Philosophy";
			Type = Advance.Philosophy;
		}
	}
}