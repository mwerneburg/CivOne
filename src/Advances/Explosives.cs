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
	internal class Explosives : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"EXPLOSIVES place enormous force",
			"wherever it is wanted, in war and",
			"in earthworks alike.",
		};

		private static readonly string[] _page2 =
		{
			"It grants no unit directly, but",
			"the two advances beyond it give",
			"you modern infantry and the",
			"internal combustion engine.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Explosives() : base(5, 1, 2, Advance.Gunpowder, Advance.Chemistry)
		{
			Name = "Explosives";
			Type = Advance.Explosives;
		}
	}
}