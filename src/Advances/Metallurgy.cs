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
	internal class Metallurgy : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"METALLURGY casts barrels strong",
			"enough for heavy guns.",
			"",
			"Allows the CANNON.",
		};

		private static readonly string[] _page2 =
		{
			"The cannon retires the CATAPULT",
			"and restores the attacker's",
			"advantage against walls that",
			"muskets could not shake.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Metallurgy() : base(7, 2, 1, Advance.Gunpowder, Advance.University)
		{
			Name = "Metallurgy";
			Type = Advance.Metallurgy;
		}
	}
}