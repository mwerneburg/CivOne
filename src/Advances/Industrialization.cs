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
	internal class Industrialization : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"INDUSTRIALIZATION gathers work",
			"into factories and multiplies what",
			"one pair of hands can make.",
			"",
			"Allows the FACTORY, the TRANSPORT",
			"and WOMEN'S SUFFRAGE.",
		};

		private static readonly string[] _page2 =
		{
			"Factories add half again to a",
			"city's production — and begin the",
			"POLLUTION that will trouble the",
			"rest of the game.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Industrialization() : base(2, 0, 2, Advance.RailRoad, Advance.Banking)
		{
			Name = "Industrialization";
			Type = Advance.Industrialization;
		}
	}
}