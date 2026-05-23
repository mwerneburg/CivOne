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
	internal class CollectiveMemory : BasePostContactAdvance
	{
		private static readonly string[] _page1 =
		{
			"COLLECTIVE MEMORY is the synthesis",
			"of neural interface and graviton",
			"engineering: a civilization-wide",
			"network of shared experience.",
			"No knowledge is lost. No discovery",
			"need be made twice. Humanity and",
			"its allies think as one mind.",
		};

		public override string[] GetPageText(byte pageNumber) => _page1;

		public override byte Id => (byte)Advance.CollectiveMemory;
		public override string Name => "Collective Memory";
		public CollectiveMemory() : base(Advance.NeuralInterface, Advance.GravitonEngineering) { }
	}
}
