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
	internal class SuperConductor : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE SUPERCONDUCTOR carries current",
			"with no loss at all.",
			"",
			"It grants no unit or building of",
			"its own.",
		};

		private static readonly string[] _page2 =
		{
			"A quiet advance that stands",
			"between the atom and the star:",
			"fusion cannot be contained without",
			"it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SuperConductor() : base(5, 2, 2, Advance.Plastics, Advance.MassProduction)
		{
			Name = "SuperConductor";
			Type = Advance.SuperConductor;
		}
	}
}