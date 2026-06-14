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
	internal class Xenobiology : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"XENOBIOLOGY is the empirical study",
			"of life beyond Earth. Gifted to",
			"all civilizations at first contact",
			"with the Olvir, it unlocks the",
			"Xenolab, which boosts science by",
			"50%, and forms the basis of",
			"Transit Conduit research.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.Xenobiology;
		public override string Name => "Xenobiology";
		public Xenobiology() : base(Advance.GeneticEngineering, Advance.SpaceFlight) { }
	}
}
