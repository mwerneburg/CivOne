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
	internal class GravitonEngineering : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"GRAVITON ENGINEERING manipulates",
			"the fundamental carrier of",
			"gravity itself. Built on Aquatic",
			"Colonization and Transit Conduit",
			"research, it unlocks feats once",
			"considered impossible, and leads",
			"toward Collective Memory.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.GravitonEngineering;
		public override string Name => "Graviton Engineering";
		public GravitonEngineering() : base(Advance.AquaticColonization, Advance.TransitConduit) { }
	}
}
