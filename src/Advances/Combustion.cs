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
	internal class Combustion : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"COMBUSTION burns fuel inside the",
			"engine itself, small enough to",
			"carry anywhere.",
			"",
			"Allows the CRUISER.",
		};

		private static readonly string[] _page2 =
		{
			"It retires the IRONCLAD. Within",
			"two advances the same engine will",
			"put armies on wheels and men in",
			"the air.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Combustion() : base(4, 2, 0, Advance.Refining, Advance.Explosives)
		{
			Name = "Combustion";
			Type = Advance.Combustion;
		}
	}
}