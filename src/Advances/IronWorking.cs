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
	internal class IronWorking : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"IRON WORKING gives a metal common",
			"enough to arm a whole people.",
			"",
			"Allows the LEGION.",
		};

		private static readonly string[] _page2 =
		{
			"Legions are the first units cheap",
			"enough to lose in numbers, which",
			"is how ancient wars are won.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public IronWorking() : base(5, 1, 1, Advance.BronzeWorking)
		{
			Name = "Iron Working";
			Type = Advance.IronWorking;
		}
	}
}