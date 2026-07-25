// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Concepts
{
	internal class Fortify : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"FORTIFY orders a land unit to dig",
			"in where it stands.",
			"",
			"A fortified unit defends with a",
			"bonus, at the cost of not moving",
			"until you wake it.",
			"",
			"Fortify garrisons and any unit",
			"holding key ground.",
		};

		private static readonly string[] _page2 =
		{
			"Combine FORTIFY with terrain and",
			"improvements: a fortified unit on",
			"hills, in a fortress, or inside a",
			"city with WALLS is very hard to",
			"dislodge.",
			"",
			"Only land units can fortify.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Fortify()
		{
			Name = "Fortify";
		}
	}
}