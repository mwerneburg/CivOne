#nullable enable
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
	internal class Gravitics : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"GRAVITICS is the applied mastery",
			"of gravitational fields, made",
			"possible by fusion power and a",
			"deep understanding of gravity's",
			"nature. It enables Aquatic",
			"Colonization and the Transit",
			"Conduit.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.Gravitics;
		public override string Name => "Gravitics";
		public Gravitics() : base(Advance.FusionPower, Advance.TheoryOfGravity) { }
	}
}
