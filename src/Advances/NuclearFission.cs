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
	internal class NuclearFission : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"NUCLEAR FISSION splits the atom",
			"and releases what holds it",
			"together.",
			"",
			"Allows THE MANHATTAN PROJECT.",
		};

		private static readonly string[] _page2 =
		{
			"The Manhattan Project lets EVERY",
			"civilization build NUCLEAR",
			"missiles, not only the one that",
			"completed it. Consider carefully",
			"before you finish it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public NuclearFission() : base(5, 0, 1, Advance.MassProduction, Advance.AtomicTheory)
		{
			Name = "Nuclear Fission";
			Type = Advance.NuclearFission;
		}
	}
}